using System.Buffers.Binary;
using System.Security.Cryptography;

namespace AnonimizadorDotNet.Cryptography.Selection;

/// <summary>
/// Escolhe de forma determinística um índice em <c>[0, count)</c> a partir de HMAC-SHA256 sobre um valor,
/// para selecionar substitutos em dicionários (nomes, domínios de e-mail).
/// </summary>
/// <remarks>
/// <para>
/// Mesma chave + mesmo contexto + mesmo valor ⇒ mesmo índice. Sem a chave, o índice é imprevisível: não
/// dá para montar uma tabela "valor real → substituto" a partir de uma lista de valores comuns. Colisões
/// (dois valores com o mesmo índice) são esperadas e aceitas: a seleção não é usada em identificadores
/// que relacionam registros.
/// </para>
/// <para>
/// O contexto separa usos com a mesma chave (por exemplo, "prenome" e "sobrenome 2"), para que cada parte
/// seja sorteada de forma independente. A mensagem autenticada é
/// <c>[contador]⁴ || [tamanho do contexto]⁴ || contexto || valor</c>, com inteiros big-endian; o prefixo
/// de tamanho impede que contextos e valores diferentes formem a mesma mensagem.
/// </para>
/// <para>
/// O índice não tem viés de módulo: cada bloco de 8 bytes do HMAC é aceito só se cair na faixa que é
/// múltiplo exato de <c>count</c>; senão usa-se o bloco seguinte e, esgotados os 4 blocos, o HMAC é
/// recalculado com o próximo contador.
/// </para>
/// <para>Os métodos não guardam estado e podem ser chamados por várias threads ao mesmo tempo.</para>
/// </remarks>
internal static class HmacSelector
{
    private const int DigestSize = HMACSHA256.HashSizeInBytes;
    private const int ChunkSize = sizeof(ulong);
    private const int HeaderSize = 2 * sizeof(uint);

    /// <summary>Limite para usar a pilha na mensagem autenticada.</summary>
    private const int StackAllocThreshold = 256;

    /// <summary>Seleciona um índice em <c>[0, count)</c>.</summary>
    /// <param name="key">Chave HMAC (normalmente a subchave do tipo); não pode ser vazia.</param>
    /// <param name="context">Contexto do uso, que separa sorteios independentes com a mesma chave.</param>
    /// <param name="value">Valor já normalizado (por exemplo, em UTF-8).</param>
    /// <param name="count">Quantidade de opções; ao menos 1.</param>
    /// <exception cref="ArgumentException">Chave vazia.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> menor que 1.</exception>
    public static int SelectIndex(ReadOnlySpan<byte> key, ReadOnlySpan<byte> context, ReadOnlySpan<byte> value, int count)
    {
        if (key.IsEmpty)
        {
            throw new ArgumentException("A chave HMAC não pode ser vazia.", nameof(key));
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);

        int messageLength = HeaderSize + context.Length + value.Length;
        Span<byte> message = messageLength <= StackAllocThreshold
            ? stackalloc byte[messageLength]
            : new byte[messageLength];
        Span<byte> digest = stackalloc byte[DigestSize];
        try
        {
            BinaryPrimitives.WriteUInt32BigEndian(message[sizeof(uint)..HeaderSize], (uint)context.Length);
            context.CopyTo(message[HeaderSize..]);
            value.CopyTo(message[(HeaderSize + context.Length)..]);

            for (uint counter = 0; ; counter++)
            {
                BinaryPrimitives.WriteUInt32BigEndian(message[..sizeof(uint)], counter);
                HMACSHA256.HashData(key, message, digest);

                if (TryReduce(digest, count, out int index))
                {
                    return index;
                }
            }
        }
        finally
        {
            // A mensagem contém o valor original, que pode ser um dado pessoal.
            CryptographicOperations.ZeroMemory(message);
            CryptographicOperations.ZeroMemory(digest);
        }
    }

    /// <summary>
    /// Tenta extrair um índice sem viés dos blocos de 8 bytes de <paramref name="digest"/>, em ordem.
    /// </summary>
    /// <remarks>
    /// Um bloco x (inteiro de 64 bits) é aceito se x ≥ 2⁶⁴ mod count: a faixa aceita tem
    /// 2⁶⁴ − (2⁶⁴ mod count) valores, múltiplo exato de count, e x mod count fica uniforme.
    /// </remarks>
    /// <returns><see langword="false"/> se todos os blocos foram rejeitados.</returns>
    internal static bool TryReduce(ReadOnlySpan<byte> digest, int count, out int index)
    {
        ulong modulus = (ulong)count;
        ulong threshold = unchecked(0UL - modulus) % modulus;

        for (int offset = 0; offset + ChunkSize <= digest.Length; offset += ChunkSize)
        {
            ulong chunk = BinaryPrimitives.ReadUInt64BigEndian(digest.Slice(offset, ChunkSize));
            if (chunk >= threshold)
            {
                index = (int)(chunk % modulus);
                return true;
            }
        }

        index = 0;
        return false;
    }
}
