using AnonimizadorDotNet.Cryptography.Fpe;
using BenchmarkDotNet.Attributes;

namespace AnonimizadorDotNet.Benchmarks.Cryptography;

/// <summary>
/// Custo do FF1 nos tamanhos usados pelos documentos da v1: base do CPF (9 dígitos), raiz do CNPJ
/// alfanumérico (8 posições em base 36) e sufixo do telefone (8 dígitos).
/// </summary>
[MemoryDiagnoser]
public class Ff1Benchmarks : IDisposable
{
    private static readonly byte[] Key = Convert.FromHexString("2B7E151628AED2A6ABF7158809CF4F3CEF4359D8D580AA4F7F036D6F04FC6A94");

    private readonly byte[] _cpfBase = [1, 2, 3, 4, 5, 6, 7, 8, 9];
    private readonly byte[] _cnpjRoot = [1, 2, 10, 11, 20, 21, 34, 35];
    private readonly byte[] _phoneSuffix = [8, 7, 6, 5, 4, 3, 2, 1];
    private readonly byte[] _output9 = new byte[9];
    private readonly byte[] _output8 = new byte[8];

    private Ff1 _radix10 = null!;
    private Ff1 _radix36 = null!;

    [GlobalSetup]
    public void Setup()
    {
        _radix10 = new Ff1(Key, 10);
        _radix36 = new Ff1(Key, 36);
    }

    [GlobalCleanup]
    public void Dispose()
    {
        _radix10?.Dispose();
        _radix36?.Dispose();
        GC.SuppressFinalize(this);
    }

    [Benchmark(Baseline = true)]
    public void EncryptCpfBase() => _radix10.Encrypt(_cpfBase, [], _output9);

    [Benchmark]
    public void DecryptCpfBase() => _radix10.Decrypt(_cpfBase, [], _output9);

    [Benchmark]
    public void EncryptCnpjAlphanumericRoot() => _radix36.Encrypt(_cnpjRoot, [], _output8);

    [Benchmark]
    public void EncryptPhoneSuffix() => _radix10.Encrypt(_phoneSuffix, [], _output8);
}
