using System.Collections.Concurrent;
using AnonimizadorDotNet.Cryptography.Keys;

namespace AnonimizadorDotNet.Cryptography.Fpe;

/// <summary>
/// Instâncias de <see cref="Ff1"/> com a mesma chave e o mesmo radix, reutilizadas entre chamadas e seguras
/// para uso por várias threads ao mesmo tempo.
/// </summary>
/// <remarks>
/// <para>
/// O <see cref="Ff1"/> guarda estado do AES e não pode ser usado por duas threads ao mesmo tempo. Cada
/// operação do pool pega uma instância livre (ou cria uma, se não houver), usa e devolve. Sem disputa, o
/// custo extra é o de uma retirada e uma devolução num <see cref="ConcurrentBag{T}"/>; com disputa, cada
/// thread trabalha na sua instância, sem esperar as outras.
/// </para>
/// <para>
/// O pool retém no máximo <see cref="MaxRetained"/> instâncias livres; as que sobram na devolução são
/// descartadas. A chave fica numa cópia interna (cada <see cref="Ff1"/> também guarda a sua), zerada em
/// <see cref="Dispose"/>.
/// </para>
/// <para>
/// O contrato de concorrência foi escolhido na etapa 4 e deve ser revisto com o uso real (sessão,
/// paralelismo em lotes).
/// </para>
/// </remarks>
internal sealed class Ff1Pool : IDisposable
{
    private readonly SecretKey _key;
    private readonly ConcurrentBag<Ff1> _free = [];
    private int _retained;
    private volatile bool _disposed;

    /// <summary>Cria o pool, validando a chave e o radix com uma primeira instância.</summary>
    /// <param name="key">Chave AES de 16, 24 ou 32 bytes. Uma cópia é mantida internamente.</param>
    /// <param name="radix">Tamanho do alfabeto, entre 2 e 36.</param>
    /// <param name="maxRetained">Instâncias livres guardadas no máximo; o padrão é 2 por processador.</param>
    /// <exception cref="ArgumentException">Chave com tamanho inválido.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Radix fora do intervalo ou <paramref name="maxRetained"/> menor que 1.</exception>
    public Ff1Pool(ReadOnlySpan<byte> key, int radix, int maxRetained = 0)
    {
        if (maxRetained == 0)
        {
            maxRetained = 2 * Environment.ProcessorCount;
        }

        ArgumentOutOfRangeException.ThrowIfLessThan(maxRetained, 1);
        var first = new Ff1(key, radix);
        _key = new SecretKey(key);
        Radix = radix;
        MinLength = first.MinLength;
        MaxRetained = maxRetained;
        Return(first);
    }

    /// <summary>Tamanho do alfabeto.</summary>
    public int Radix { get; }

    /// <summary>Menor quantidade de numerais aceita para este radix.</summary>
    public int MinLength { get; }

    /// <summary>Quantidade máxima de instâncias livres guardadas.</summary>
    public int MaxRetained { get; }

    /// <summary>Instâncias livres no momento (para testes).</summary>
    internal int RetainedCount => Volatile.Read(ref _retained);

    /// <summary>Cifra com FF1. Ver <see cref="Ff1.Encrypt"/>.</summary>
    /// <exception cref="ObjectDisposedException">O pool já foi descartado.</exception>
    public void Encrypt(ReadOnlySpan<byte> input, ReadOnlySpan<byte> tweak, Span<byte> output)
    {
        Ff1 ff1 = Rent();
        try
        {
            ff1.Encrypt(input, tweak, output);
        }
        finally
        {
            Return(ff1);
        }
    }

    /// <summary>Decifra com FF1. Ver <see cref="Ff1.Decrypt"/>.</summary>
    /// <exception cref="ObjectDisposedException">O pool já foi descartado.</exception>
    public void Decrypt(ReadOnlySpan<byte> input, ReadOnlySpan<byte> tweak, Span<byte> output)
    {
        Ff1 ff1 = Rent();
        try
        {
            ff1.Decrypt(input, tweak, output);
        }
        finally
        {
            Return(ff1);
        }
    }

    /// <summary>Cifra com <i>cycle-walking</i>. Ver <see cref="CycleWalking.Encrypt"/>.</summary>
    /// <exception cref="ObjectDisposedException">O pool já foi descartado.</exception>
    public void Encrypt(ReadOnlySpan<byte> input, ReadOnlySpan<byte> tweak, Span<byte> output, NumeralPredicate isAllowed)
    {
        Ff1 ff1 = Rent();
        try
        {
            CycleWalking.Encrypt(ff1, input, tweak, output, isAllowed);
        }
        finally
        {
            Return(ff1);
        }
    }

    /// <summary>Decifra com <i>cycle-walking</i>. Ver <see cref="CycleWalking.Decrypt"/>.</summary>
    /// <exception cref="ObjectDisposedException">O pool já foi descartado.</exception>
    public void Decrypt(ReadOnlySpan<byte> input, ReadOnlySpan<byte> tweak, Span<byte> output, NumeralPredicate isAllowed)
    {
        Ff1 ff1 = Rent();
        try
        {
            CycleWalking.Decrypt(ff1, input, tweak, output, isAllowed);
        }
        finally
        {
            Return(ff1);
        }
    }

    /// <summary>Descarta as instâncias livres e zera a cópia da chave.</summary>
    /// <remarks>Instâncias em uso são descartadas quando devolvidas.</remarks>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Drain();
        _key.Dispose();
    }

    private Ff1 Rent()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_free.TryTake(out Ff1? ff1))
        {
            Interlocked.Decrement(ref _retained);
            return ff1;
        }

        return new Ff1(_key.Span, Radix);
    }

    private void Return(Ff1 ff1)
    {
        if (_disposed)
        {
            ff1.Dispose();
            return;
        }

        if (Interlocked.Increment(ref _retained) > MaxRetained)
        {
            Interlocked.Decrement(ref _retained);
            ff1.Dispose();
            return;
        }

        _free.Add(ff1);

        // Um descarte concorrente pode ter esvaziado o pool antes da devolução: esvazia de novo.
        if (_disposed)
        {
            Drain();
        }
    }

    private void Drain()
    {
        while (_free.TryTake(out Ff1? ff1))
        {
            Interlocked.Decrement(ref _retained);
            ff1.Dispose();
        }
    }
}
