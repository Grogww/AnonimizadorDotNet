using System.Diagnostics.CodeAnalysis;
using AnonimizadorDotNet.Detection;
using AnonimizadorDotNet.Tests.Transformation;
using AnonimizadorDotNet.Transformation;
using CsCheck;

namespace AnonimizadorDotNet.Tests;

public sealed class AnonymizerBuilderTests
{
    private const string Cpf = "12345678909";

    [Fact]
    public void SemChave_BuildFalha()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => new AnonymizerBuilder().Build());
        Assert.Contains("chave", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ChaveCurta_Falha()
    {
        var exception = Assert.Throws<ArgumentException>(() => new AnonymizerBuilder().UseKey(new byte[31]));
        Assert.Equal("key", exception.ParamName);
    }

    [Fact]
    public void ChaveDaVariavelDeAmbiente_EquivaleAChaveDireta()
    {
        WithVariable(Convert.ToBase64String(ReferenceSubstitutes.MasterKey), name =>
        {
            using Anonymizer fromEnvironment = new AnonymizerBuilder().UseKeyFromEnvironment(name).Build();
            using Anonymizer direct = new AnonymizerBuilder().UseKey(ReferenceSubstitutes.MasterKey).Build();

            Assert.True(fromEnvironment.TryAnonymize(Cpf, PersonalDataTypes.Cpf, out string? a));
            Assert.True(direct.TryAnonymize(Cpf, PersonalDataTypes.Cpf, out string? b));
            Assert.Equal(ReferenceSubstitutes.Cpf(Cpf), a);
            Assert.Equal(a, b);
        });
    }

    [Fact]
    public void VariavelDeAmbienteAusente_BuildFalha()
    {
        string name = $"ANONIMIZADOR_TESTE_{Guid.NewGuid():N}";
        Assert.Throws<InvalidOperationException>(() => new AnonymizerBuilder().UseKeyFromEnvironment(name).Build());
    }

    [Fact]
    public void VariavelDeAmbienteCurta_BuildFalhaSemMostrarOConteudo()
    {
        string value = Convert.ToBase64String(new byte[] { 0xCA, 0xFE, 0xBA, 0xBE });
        WithVariable(value, name =>
        {
            var exception = Assert.Throws<InvalidOperationException>(() => new AnonymizerBuilder().UseKeyFromEnvironment(name).Build());
            Assert.DoesNotContain(value, exception.Message, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void VariavelDeAmbienteForaDeBase64_NuncaMostraOConteudo()
    {
        Gen.String[Gen.Char['!', '~'], 1, 60].Where(s => s.Contains('!', StringComparison.Ordinal)).Sample(value =>
        {
            WithVariable(value, name =>
            {
                var exception = Assert.Throws<InvalidOperationException>(() => new AnonymizerBuilder().UseKeyFromEnvironment(name).Build());
                Assert.DoesNotContain(value, exception.Message, StringComparison.Ordinal);
            });
        }, iter: 50);
    }

    [Fact]
    public void UsoUnico_DepoisDoBuildTudoFalha()
    {
        AnonymizerBuilder builder = new AnonymizerBuilder().UseKey(ReferenceSubstitutes.MasterKey);
        using Anonymizer anonymizer = builder.Build();

        Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Throws<InvalidOperationException>(() => builder.UseKey(ReferenceSubstitutes.MasterKey));
        Assert.Throws<InvalidOperationException>(() => builder.TransformColumn("cpf", PersonalDataTypes.Cpf));
        Assert.Throws<InvalidOperationException>(() => builder.WithUnclassifiedPolicy(UnclassifiedPolicy.Block));
    }

    [Fact]
    public void BuildQueFalha_TambemEncerraOBuilder()
    {
        AnonymizerBuilder builder = new AnonymizerBuilder()
            .UseKey(ReferenceSubstitutes.MasterKey)
            .TransformColumn("matricula", "inexistente");

        var exception = Assert.Throws<InvalidOperationException>(() => builder.Build());
        Assert.Contains("inexistente", exception.Message, StringComparison.Ordinal);
        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Theory]
    [InlineData("a.b.c.d")]
    [InlineData("tabela.")]
    [InlineData("[coluna")]
    [InlineData("")]
    public void ColunaInvalida_Falha(string column)
    {
        var exception = Assert.Throws<ArgumentException>(() => new AnonymizerBuilder().IgnoreColumn(column));
        Assert.Equal("column", exception.ParamName);
    }

    [Fact]
    public void ColunaRepetida_Falha()
    {
        AnonymizerBuilder builder = new AnonymizerBuilder().IgnoreColumn("Clientes.Codigo");

        Assert.Throws<ArgumentException>(() => builder.TransformColumn("[clientes].[codigo]", PersonalDataTypes.Cpf));
        builder.ScanColumn("Codigo");
    }

    [Fact]
    public void TransformadorComTipoRepetido_Falha()
    {
        var builder = new AnonymizerBuilder();

        Assert.Throws<ArgumentException>(() => builder.AddTransformer(new UpperTransformer("CPF")));
        builder.AddTransformer(new UpperTransformer("matricula"));
        Assert.Throws<ArgumentException>(() => builder.AddTransformer(new UpperTransformer("Matricula")));
        Assert.Throws<ArgumentException>(() => builder.AddTransformer(new UpperTransformer(" ")));
    }

    [Fact]
    public void PoliticaDesconhecida_Falha() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new AnonymizerBuilder().WithUnclassifiedPolicy((UnclassifiedPolicy)7));

    [Fact]
    public void Detector_DeTipoSemTransformador_FalhaNoBuild()
    {
        AnonymizerBuilder builder = new AnonymizerBuilder()
            .UseKey(ReferenceSubstitutes.MasterKey)
            .AddDetector(new PrefixDetector("matricula", "ra-"));

        Assert.Throws<InvalidOperationException>(builder.Build);
    }

    [Fact]
    public void Detector_RepetidoOuSemTipo_Falha()
    {
        var detector = new PrefixDetector("cpf", "x");
        AnonymizerBuilder builder = new AnonymizerBuilder().AddDetector(detector);

        Assert.Throws<ArgumentException>(() => builder.AddDetector(detector));
        Assert.Throws<ArgumentException>(() => builder.AddDetector(new PrefixDetector(" ", "x")));
        Assert.Throws<ArgumentNullException>(() => builder.AddDetector(null!));
        builder.AddDetector(new PrefixDetector("CPF", "y"));
    }

    [Fact]
    public void ToString_NaoMostraAChave()
    {
        AnonymizerBuilder builder = new AnonymizerBuilder().UseKey(ReferenceSubstitutes.MasterKey);
        Assert.Equal(typeof(AnonymizerBuilder).FullName, builder.ToString());
        using Anonymizer anonymizer = builder.Build();
        Assert.Equal(typeof(Anonymizer).FullName, anonymizer.ToString());
    }

    private static void WithVariable(string value, Action<string> test)
    {
        string name = $"ANONIMIZADOR_TESTE_{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable(name, value);
        try
        {
            test(name);
        }
        finally
        {
            Environment.SetEnvironmentVariable(name, null);
        }
    }

    /// <summary>Encontra as palavras que começam com um prefixo.</summary>
    internal sealed class PrefixDetector(string type, string prefix) : ITextDetector
    {
        public string Type { get; } = type;

        public IEnumerable<TextMatch> Detect(string text)
        {
            int at = text.IndexOf(prefix, StringComparison.Ordinal);
            while (at >= 0)
            {
                int end = text.IndexOf(' ', at);
                end = end < 0 ? text.Length : end;
                yield return new TextMatch(at, end - at);
                at = text.IndexOf(prefix, end, StringComparison.Ordinal);
            }
        }
    }

    internal sealed class UpperTransformer(string type) : IValueTransformer
    {
        public string Type { get; } = type;

        public bool TryTransform(ReadOnlySpan<char> value, ValueKind kind, [NotNullWhen(true)] out string? substitute)
        {
            substitute = value.ToString().ToUpperInvariant();
            return true;
        }
    }
}
