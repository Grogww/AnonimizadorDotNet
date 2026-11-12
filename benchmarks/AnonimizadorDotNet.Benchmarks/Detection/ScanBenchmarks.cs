using System.Text;
using AnonimizadorDotNet.Detection;
using AnonimizadorDotNet.Transformation;
using BenchmarkDotNet.Attributes;

namespace AnonimizadorDotNet.Benchmarks.Detection;

/// <summary>
/// Custo da varredura de texto livre: texto sem dígito nem <c>@</c> (caminho rápido), texto com números que
/// não são documentos e textos de 1 KB, 64 KB e 1 MB com três documentos por KB.
/// </summary>
/// <remarks>Textos grandes (<c>nvarchar(max)</c>) serão comuns: esta medição entra nas que se repetem.</remarks>
[MemoryDiagnoser]
public class ScanBenchmarks : IDisposable
{
    private const string Prose = "O cliente ligou pedindo a segunda via do boleto e a revisão do cadastro. ";
    private const string Numbers = "Pedido 4521 de 12/03/2024, valor R$ 1.234,56, CEP 01310-100, protocolo 2024123456. ";
    private const string Documents = "CPF 529.982.247-25, e-mail maria.silva87@empresa.com.br, tel (11) 98765-4321. ";

    private static readonly byte[] MasterKey = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();

    private TransformerSet _set = null!;
    private TextScanner _scanner = null!;
    private string _prose = null!;
    private string _numbers = null!;
    private string _oneKb = null!;
    private string _sixtyFourKb = null!;
    private string _oneMb = null!;

    [GlobalSetup]
    public void Setup()
    {
        _set = new TransformerSet(MasterKey);
        _scanner = new TextScanner(_set);
        _prose = Repeat([Prose], 1024);
        _numbers = Repeat([Numbers, Prose], 1024);
        _oneKb = Repeat([Documents, Prose, Numbers, Prose], 1024);
        _sixtyFourKb = Repeat([Documents, Prose, Numbers, Prose], 64 * 1024);
        _oneMb = Repeat([Documents, Prose, Numbers, Prose], 1024 * 1024);
    }

    [GlobalCleanup]
    public void Dispose()
    {
        _set?.Dispose();
        GC.SuppressFinalize(this);
    }

    [Benchmark(Baseline = true)]
    public string SemDigito1Kb() => _scanner.Scan(_prose);

    [Benchmark]
    public string NumerosQueNaoSaoDocumento1Kb() => _scanner.Scan(_numbers);

    [Benchmark]
    public string ComDocumentos1Kb() => _scanner.Scan(_oneKb);

    [Benchmark]
    public string ComDocumentos64Kb() => _scanner.Scan(_sixtyFourKb);

    [Benchmark]
    public string ComDocumentos1Mb() => _scanner.Scan(_oneMb);

    private static string Repeat(string[] parts, int length)
    {
        var builder = new StringBuilder(length + 256);
        for (int i = 0; builder.Length < length; i++)
        {
            builder.Append(parts[i % parts.Length]);
        }

        return builder.ToString(0, length);
    }
}
