using System.Diagnostics.CodeAnalysis;
using System.Text;
using AnonimizadorDotNet.Detection;
using AnonimizadorDotNet.Tests.Transformation;
using AnonimizadorDotNet.Transformation;
using CsCheck;

namespace AnonimizadorDotNet.Tests.Detection;

public sealed class TextScannerTests : IDisposable
{
    private const string Cpf = "12345678909";
    private const string MaskedCpf = "123.456.789-09";
    private const string MaskedCnpj = "11.222.333/0001-81";
    private const string AlphanumericCnpj = "12ABC34501DE35";

    private readonly TransformerSet _transformers = new(ReferenceSubstitutes.MasterKey, [new MatriculaTransformer()]);
    private readonly TextScanner _scanner;

    public TextScannerTests() => _scanner = new TextScanner(_transformers);

    public void Dispose() => _transformers.Dispose();

    [Fact]
    public void SubstituiCadaTrecho_PeloMesmoSubstitutoDaColuna_ERestoIntacto()
    {
        string text = $"Cliente CPF {MaskedCpf}, empresa {MaskedCnpj} ({AlphanumericCnpj}), "
            + "e-mail joao.silva@empresa.com.br, tel (11) 98765-4321. Fim.";

        string expected = $"Cliente CPF {Sub(DocumentType.Cpf, MaskedCpf)}, empresa {Sub(DocumentType.Cnpj, MaskedCnpj)} "
            + $"({Sub(DocumentType.Cnpj, AlphanumericCnpj)}), e-mail {Sub(DocumentType.Email, "joao.silva@empresa.com.br")}, "
            + $"tel {Sub(DocumentType.Phone, "(11) 98765-4321")}. Fim.";

        Assert.Equal(expected, _scanner.Scan(text));
    }

    [Fact]
    public void OMesmoCpf_ComESemMascara_GeraOMesmoSubstituto()
    {
        string result = _scanner.Scan($"{MaskedCpf} e {Cpf}");
        string substitute = Sub(DocumentType.Cpf, Cpf);
        Assert.Equal($"{DocumentSamples.MaskCpf(substitute)} e {substitute}", result);
    }

    [Theory]
    [InlineData("texto sem documento nenhum")]
    [InlineData("pedido 42 entregue em 12/03/2024 por R$ 1.234,56")]
    [InlineData("CPF 123.456.789-00 com verificador errado")]
    [InlineData("")]
    public void SemTrechoValido_DevolveAMesmaInstancia(string text) => Assert.Same(text, _scanner.Scan(text));

    [Fact]
    public void Sobreposicao_CpfValidoComFormaDeCelular_VenceOCpf()
    {
        // Base escolhida para que o CPF também seja um celular válido: DDD 11, 9 e mais 8 dígitos.
        string cpf = DocumentSamples.Cpf("119876543");
        Assert.True(_transformers[DocumentType.Phone].TryTransform(cpf, ValueKind.Text, out _));

        Assert.Equal($"x {Sub(DocumentType.Cpf, cpf)} x", _scanner.Scan($"x {cpf} x"));
    }

    [Fact]
    public void Sobreposicao_CpfInvalidoComFormaDeCelular_ViraTelefone()
    {
        string phone = DocumentSamples.BreakCheckDigit(DocumentSamples.Cpf("119876543"));
        Assert.Equal($"x {Sub(DocumentType.Phone, phone)} x", _scanner.Scan($"x {phone} x"));
    }

    [Fact]
    public void Sobreposicao_TelefoneDentroDoEmail_VenceOEmail()
    {
        const string email = "joao11987654321@gmail.com";
        Assert.Equal($"para {Sub(DocumentType.Email, email)}", _scanner.Scan($"para {email}"));
    }

    [Fact]
    public void DetectorProprio_SomaAosEmbutidos_ETrechoRecusadoFicaComoEsta()
    {
        var scanner = new TextScanner(_transformers, [new MatriculaDetector()]);

        string result = scanner.Scan($"MAT-123456 do aluno {Cpf}; MAT-000000 é reservada");

        Assert.Equal($"MAT-654321 do aluno {Sub(DocumentType.Cpf, Cpf)}; MAT-000000 é reservada", result);
    }

    [Fact]
    public void DetectorProprio_MesmaPosicaoDeUmEmbutido_VenceOEmbutido()
    {
        var scanner = new TextScanner(_transformers, [new FixedDetector(DocumentTypeNames.Cpf, new TextMatch(2, Cpf.Length))]);
        Assert.Equal($"x {Sub(DocumentType.Cpf, Cpf)} x", scanner.Scan($"x {Cpf} x"));
    }

    [Fact]
    public void DetectorProprio_TrechoMaisLongoQueComecaAntes_Vence()
    {
        const string text = "ref 12345678909";
        var scanner = new TextScanner(_transformers, [new FixedDetector("matricula", new TextMatch(0, text.Length))]);
        Assert.Equal(MatriculaTransformer.Reverse(text), scanner.Scan(text));
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 100)]
    [InlineData(10, 6)]
    public void DetectorProprio_TrechoForaDoTexto_LancaSemExporOTexto(int index, int length)
    {
        const string text = "CPF 12345678909";
        TextMatch match = index < 0 ? default : new TextMatch(index, length);
        var scanner = new TextScanner(_transformers, [new FixedDetector("matricula", match)]);

        var exception = Assert.Throws<InvalidOperationException>(() => scanner.Scan(text));
        Assert.DoesNotContain(Cpf, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void DetectorProprio_DeTipoSemTransformador_Falha()
    {
        Assert.Throws<ArgumentException>(() => new TextScanner(_transformers, [new FixedDetector("inexistente", default)]));
        Assert.Throws<ArgumentException>(() => new TextScanner(_transformers, [null!]));
    }

    [Fact]
    public void Propriedade_NenhumOriginalSobra_ETextoForaDosTrechosFicaIdentico()
    {
        Gen<string> filler = Gen.String[Gen.Char["abcdefghij ,;:()"], 1, 12].Select(s => $" {s} ");
        Gen<(string Value, DocumentType Type)> document = Gen.OneOf(
            DocumentSamples.Cpfs.Select(c => (c, DocumentType.Cpf)),
            DocumentSamples.Cpfs.Select(c => (DocumentSamples.MaskCpf(c), DocumentType.Cpf)),
            DocumentSamples.NumericCnpjs.Select(c => (DocumentSamples.MaskCnpj(c), DocumentType.Cnpj)),
            DocumentSamples.AlphanumericCnpjs.Select(c => (c, DocumentType.Cnpj)),
            Gen.Char["abcdefghijklmnopqrstuvwxyz"].Array[3, 10].Select(c => ($"{new string(c)}@gmail.com", DocumentType.Email)));

        Gen.Select(filler.Array[1, 6], document.Array[0, 5]).Sample((fillers, documents) =>
        {
            var text = new StringBuilder(fillers[0]);
            var expected = new StringBuilder(fillers[0]);
            for (int i = 0; i < documents.Length; i++)
            {
                string after = fillers[(i + 1) % fillers.Length];
                text.Append(documents[i].Value).Append(after);
                expected.Append(Sub(documents[i].Type, documents[i].Value)).Append(after);
            }

            string result = _scanner.Scan(text.ToString());
            Assert.Equal(expected.ToString(), result);
            // O e-mail fica de fora: a parte local substituta pode conter a original (ana → joana).
            foreach ((string value, _) in documents.Where(d => d.Type != DocumentType.Email))
            {
                Assert.DoesNotContain(value, result, StringComparison.Ordinal);
            }
        });
    }

    [Fact]
    public void Propriedade_NuncaLanca()
    {
        Gen.String.Sample(text =>
        {
            _ = _scanner.Scan(text);
        });
    }

    private string Sub(DocumentType type, string value) =>
        _transformers[type].TryTransform(value, ValueKind.Text, out string? substitute)
            ? substitute
            : throw new InvalidOperationException("O valor de teste deveria ser aceito.");

    /// <summary>Inverte o texto; recusa os trechos só de zeros depois do prefixo.</summary>
    private sealed class MatriculaTransformer : IValueTransformer
    {
        public string Type => "matricula";

        public static string Reverse(string value) => new(value.Reverse().ToArray());

        public bool TryTransform(ReadOnlySpan<char> value, ValueKind kind, [NotNullWhen(true)] out string? substitute)
        {
            string text = value.ToString();
            if (text.StartsWith("MAT-", StringComparison.Ordinal))
            {
                substitute = text[4..].All(c => c == '0') ? null : $"MAT-{Reverse(text[4..])}";
                return substitute is not null;
            }

            substitute = Reverse(text);
            return true;
        }
    }

    private sealed class MatriculaDetector : ITextDetector
    {
        public string Type => "matricula";

        public IEnumerable<TextMatch> Detect(string text)
        {
            for (int at = text.IndexOf("MAT-", StringComparison.Ordinal); at >= 0; at = text.IndexOf("MAT-", at + 1, StringComparison.Ordinal))
            {
                if (at + 10 <= text.Length)
                {
                    yield return new TextMatch(at, 10);
                }
            }
        }
    }

    private sealed class FixedDetector(string type, TextMatch match) : ITextDetector
    {
        public string Type { get; } = type;

        public IEnumerable<TextMatch> Detect(string text) => [match];
    }
}
