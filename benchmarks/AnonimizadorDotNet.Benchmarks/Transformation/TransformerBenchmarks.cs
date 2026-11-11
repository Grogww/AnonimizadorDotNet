using AnonimizadorDotNet.Transformation;
using BenchmarkDotNet.Attributes;

namespace AnonimizadorDotNet.Benchmarks.Transformation;

/// <summary>
/// Custo de ponta a ponta de um substituto por tipo (interpretar, cifrar ou sortear, formatar), e a vazão
/// do CPF com várias threads disputando o mesmo transformador (o pool de instâncias do FF1).
/// </summary>
[MemoryDiagnoser]
public class TransformerBenchmarks : IDisposable
{
    private const int ParallelBatch = 1_000;

    private static readonly byte[] MasterKey = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();

    private TransformerSet _set = null!;

    [GlobalSetup]
    public void Setup() => _set = new TransformerSet(MasterKey);

    [GlobalCleanup]
    public void Dispose()
    {
        _set?.Dispose();
        GC.SuppressFinalize(this);
    }

    [Benchmark(Baseline = true)]
    public string? Cpf() => Transform(DocumentType.Cpf, "529.982.247-25");

    [Benchmark]
    public string? CnpjNumerico() => Transform(DocumentType.Cnpj, "11.222.333/0001-81");

    [Benchmark]
    public string? CnpjAlfanumerico() => Transform(DocumentType.Cnpj, "12.ABC.345/01DE-35");

    [Benchmark]
    public string? Telefone() => Transform(DocumentType.Phone, "(11) 98765-4321");

    [Benchmark]
    public string? Email() => Transform(DocumentType.Email, "maria.silva87@empresa.com.br");

    [Benchmark]
    public string? Nome() => Transform(DocumentType.Name, "Maria da Conceição Silva");

    /// <summary>1.000 CPFs repartidos entre todas as threads; dividir pelo lote dá o custo por CPF.</summary>
    [Benchmark(OperationsPerInvoke = ParallelBatch)]
    public void CpfEmParalelo()
    {
        IValueTransformer cpf = _set[DocumentType.Cpf];
        Parallel.For(0, ParallelBatch, (_, _) => cpf.TryTransform("529.982.247-25", ValueKind.Text, out _));
    }

    private string? Transform(DocumentType type, string value)
    {
        _set[type].TryTransform(value, ValueKind.Text, out string? substitute);
        return substitute;
    }
}
