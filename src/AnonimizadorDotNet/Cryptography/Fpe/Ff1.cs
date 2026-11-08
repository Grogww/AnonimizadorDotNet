using System.Buffers.Binary;
using System.Numerics;
using System.Security.Cryptography;
using AnonimizadorDotNet.Cryptography.Primitives;

namespace AnonimizadorDotNet.Cryptography.Fpe;

/// <summary>
/// Cifra com preservação de formato FF1 (NIST SP 800-38G Rev. 1, seção 5.1) sobre AES.
/// </summary>
/// <remarks>
/// <para>
/// Para uma chave, um radix e um tweak, o FF1 é uma permutação das cadeias de n numerais: a saída tem o
/// mesmo tamanho e o mesmo alfabeto da entrada, é determinística e nunca colide. É uma rede de Feistel
/// de 10 rodadas cuja função de rodada é o CBC-MAC do AES.
/// </para>
/// <para>
/// Internamente, as metades A e B são mantidas como inteiros, pois a norma só as usa por meio de
/// NUM<sub>radix</sub>; a conversão para numerais acontece uma vez na entrada e outra na saída.
/// </para>
/// <para>Instâncias não são seguras para uso simultâneo por várias threads.</para>
/// </remarks>
internal sealed class Ff1 : IDisposable
{
    private const int Rounds = 10;
    private const int BlockSize = AesBlockCipher.BlockSize;

    /// <summary>Limite para usar a pilha nos buffers temporários.</summary>
    private const int StackAllocThreshold = 512;

    private readonly AesBlockCipher _cipher;

    /// <summary>Cria uma instância do FF1.</summary>
    /// <param name="key">Chave AES de 16, 24 ou 32 bytes. Uma cópia é mantida internamente.</param>
    /// <param name="radix">Tamanho do alfabeto, entre 2 e 36.</param>
    /// <exception cref="ArgumentException">Chave com tamanho inválido.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Radix fora do intervalo.</exception>
    public Ff1(ReadOnlySpan<byte> key, int radix)
    {
        Ff1Constraints.ValidateRadix(radix, nameof(radix));
        MinLength = Ff1Constraints.GetMinLength(radix);
        Radix = radix;
        _cipher = new AesBlockCipher(key);
    }

    /// <summary>Tamanho do alfabeto: cada numeral vale de 0 a <c>Radix − 1</c>.</summary>
    public int Radix { get; }

    /// <summary>Menor quantidade de numerais aceita para este radix (domínio de ao menos 10⁶ valores).</summary>
    public int MinLength { get; }

    /// <summary>Cifra uma cadeia de numerais (algoritmo FF1.Encrypt).</summary>
    /// <param name="input">Numerais da entrada, cada um menor que <see cref="Radix"/>.</param>
    /// <param name="tweak">Tweak público; pode ser vazio.</param>
    /// <param name="output">Destino, com o mesmo tamanho da entrada. Pode ser o mesmo buffer da entrada.</param>
    /// <exception cref="ArgumentException">Tamanho, numeral, tweak ou destino inválido.</exception>
    /// <exception cref="ObjectDisposedException">A instância já foi descartada.</exception>
    public void Encrypt(ReadOnlySpan<byte> input, ReadOnlySpan<byte> tweak, Span<byte> output) =>
        Transform(input, tweak, output, encrypt: true);

    /// <summary>Decifra uma cadeia de numerais (algoritmo FF1.Decrypt).</summary>
    /// <param name="input">Numerais cifrados, cada um menor que <see cref="Radix"/>.</param>
    /// <param name="tweak">O mesmo tweak usado na cifra.</param>
    /// <param name="output">Destino, com o mesmo tamanho da entrada. Pode ser o mesmo buffer da entrada.</param>
    /// <exception cref="ArgumentException">Tamanho, numeral, tweak ou destino inválido.</exception>
    /// <exception cref="ObjectDisposedException">A instância já foi descartada.</exception>
    public void Decrypt(ReadOnlySpan<byte> input, ReadOnlySpan<byte> tweak, Span<byte> output) =>
        Transform(input, tweak, output, encrypt: false);

    /// <summary>Descarta a cifra AES e o material de chave.</summary>
    public void Dispose() => _cipher.Dispose();

    private void Transform(ReadOnlySpan<byte> input, ReadOnlySpan<byte> tweak, Span<byte> output, bool encrypt)
    {
        ValidateArguments(input, tweak, output);

        // Passos 1 e 2: divisão em A (u numerais) e B (v numerais).
        int n = input.Length;
        int u = n / 2;
        int v = n - u;
        BigInteger a = NumeralString.ToInteger(input[..u], Radix);
        BigInteger b = NumeralString.ToInteger(input[u..], Radix);

        BigInteger modulusU = BigInteger.Pow(Radix, u);
        BigInteger modulusV = u == v ? modulusU : modulusU * Radix;

        // Passo 3: b = ⌈⌈v·log₂(radix)⌉ / 8⌉. ⌈v·log₂(radix)⌉ é o tamanho em bits de radixᵛ − 1,
        // calculado com inteiros para evitar erro de ponto flutuante.
        int numberLength = (int)(((modulusV - 1).GetBitLength() + 7) / 8);

        // Passo 4: d = 4·⌈b/4⌉ + 4.
        int d = (4 * ((numberLength + 3) / 4)) + 4;

        // P (16 bytes) || Q, em que Q = T || 0^((−t−b−1) mod 16) || [i]¹ || [NUM(B)]^b.
        int padding = Mod(-tweak.Length - numberLength - 1, BlockSize);
        int roundOffset = BlockSize + tweak.Length + padding;
        int pqLength = roundOffset + 1 + numberLength;

        // S tem ⌈d/16⌉ blocos; só os d primeiros bytes são usados.
        int sLength = ((d + BlockSize - 1) / BlockSize) * BlockSize;

        Span<byte> pq = pqLength <= StackAllocThreshold ? stackalloc byte[pqLength] : new byte[pqLength];
        Span<byte> s = sLength <= StackAllocThreshold ? stackalloc byte[sLength] : new byte[sLength];
        try
        {
            pq.Clear();
            WriteP(pq[..BlockSize], u, n, tweak.Length);
            tweak.CopyTo(pq[BlockSize..]);
            var round = new RoundFunction(_cipher, pq, roundOffset, numberLength, s, d);

            if (encrypt)
            {
                for (int i = 0; i < Rounds; i++)
                {
                    // Passo 6: c = (NUM(A) + y) mod radixᵐ; A ← B; B ← C.
                    BigInteger modulus = i % 2 == 0 ? modulusU : modulusV;
                    BigInteger c = (a + round.Compute(i, b)) % modulus;
                    a = b;
                    b = c;
                }
            }
            else
            {
                for (int i = Rounds - 1; i >= 0; i--)
                {
                    // Inverso: c = (NUM(B) − y) mod radixᵐ; B ← A; A ← C.
                    BigInteger modulus = i % 2 == 0 ? modulusU : modulusV;
                    BigInteger c = (b - round.Compute(i, a)) % modulus;
                    if (c.Sign < 0)
                    {
                        c += modulus;
                    }

                    b = a;
                    a = c;
                }
            }

            // Passo 7: A || B.
            NumeralString.FromInteger(a, Radix, output[..u]);
            NumeralString.FromInteger(b, Radix, output[u..]);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(pq);
            CryptographicOperations.ZeroMemory(s);
        }
    }

    private void ValidateArguments(ReadOnlySpan<byte> input, ReadOnlySpan<byte> tweak, Span<byte> output)
    {
        Ff1Constraints.ValidateLength(input.Length, MinLength, nameof(input));
        Ff1Constraints.ValidateTweak(tweak.Length, nameof(tweak));

        if (output.Length != input.Length)
        {
            throw new ArgumentException("O destino deve ter o mesmo tamanho da entrada.", nameof(output));
        }

        foreach (byte numeral in input)
        {
            if (numeral >= Radix)
            {
                // A mensagem não traz o numeral nem a posição: a entrada pode ser um dado pessoal.
                throw new ArgumentException("A entrada contém um numeral fora do alfabeto do radix.", nameof(input));
            }
        }
    }

    /// <summary>
    /// Passo 5: P = [1]¹ || [2]¹ || [1]¹ || [radix]³ || [10]¹ || [u mod 256]¹ || [n]⁴ || [t]⁴.
    /// </summary>
    private void WriteP(Span<byte> p, int u, int n, int tweakLength)
    {
        p[0] = 1;
        p[1] = 2;
        p[2] = 1;
        p[3] = (byte)(Radix >> 16);
        p[4] = (byte)(Radix >> 8);
        p[5] = (byte)Radix;
        p[6] = Rounds;
        p[7] = (byte)(u % 256);
        BinaryPrimitives.WriteUInt32BigEndian(p[8..12], (uint)n);
        BinaryPrimitives.WriteUInt32BigEndian(p[12..16], (uint)tweakLength);
    }

    private static int Mod(int value, int modulus)
    {
        int result = value % modulus;
        return result < 0 ? result + modulus : result;
    }

    /// <summary>
    /// Função de rodada do FF1 (passos 6.i a 6.iv): monta Q, calcula R = PRF(P || Q), expande R em S e
    /// devolve y = NUM(S).
    /// </summary>
    private readonly ref struct RoundFunction
    {
        private readonly AesBlockCipher _cipher;
        private readonly Span<byte> _pq;
        private readonly int _roundOffset;
        private readonly int _numberLength;
        private readonly Span<byte> _s;
        private readonly int _d;

        public RoundFunction(AesBlockCipher cipher, Span<byte> pq, int roundOffset, int numberLength, Span<byte> s, int d)
        {
            _cipher = cipher;
            _pq = pq;
            _roundOffset = roundOffset;
            _numberLength = numberLength;
            _s = s;
            _d = d;
        }

        public BigInteger Compute(int round, BigInteger half)
        {
            // 6.i: Q termina com [i]¹ || [NUM(metade)]^b.
            _pq[_roundOffset] = (byte)round;
            ByteString.FromInteger(half, _pq.Slice(_roundOffset + 1, _numberLength));

            // 6.ii: R = PRF(P || Q).
            Span<byte> r = _s[..BlockSize];
            _cipher.ComputeCbcMac(_pq, r);

            // 6.iii: S = R || CIPH(R ⊕ [1]¹⁶) || CIPH(R ⊕ [2]¹⁶) || …, truncado em d bytes.
            Span<byte> block = stackalloc byte[BlockSize];
            for (int j = 1; j * BlockSize < _s.Length; j++)
            {
                r.CopyTo(block);
                uint last = BinaryPrimitives.ReadUInt32BigEndian(block[^4..]);
                BinaryPrimitives.WriteUInt32BigEndian(block[^4..], last ^ (uint)j);
                _cipher.EncryptBlock(block, _s.Slice(j * BlockSize, BlockSize));
            }

            CryptographicOperations.ZeroMemory(block);

            // 6.iv: y = NUM(S).
            return ByteString.ToInteger(_s[.._d]);
        }
    }
}
