using System.Buffers;
using System.Text;
using AnonimizadorDotNet.Detection.Detectors;
using AnonimizadorDotNet.Transformation;

namespace AnonimizadorDotNet.Detection;

/// <summary>
/// A varredura de texto livre: os detectores apontam os trechos e o transformador de cada tipo gera o
/// substituto, o mesmo que o valor teria numa coluna classificada.
/// </summary>
/// <remarks>
/// <para>
/// Sobreposição: vence o trecho que começa antes; no empate, o mais longo; no empate, a ordem fixa CNPJ →
/// CPF → e-mail → telefone → detectores próprios (na ordem de registro). Um trecho que o transformador
/// recusa fica como está e libera a posição para o próximo candidato.
/// </para>
/// <para>
/// Texto sem dígito e sem <c>@</c> não passa pelos detectores embutidos; sem nenhuma troca, devolve a mesma
/// instância. Não muda depois de criada e pode ser usada por várias threads ao mesmo tempo.
/// </para>
/// </remarks>
internal sealed class TextScanner : ITextScanner
{
    private static readonly SearchValues<char> BuiltInHints = SearchValues.Create("0123456789@");

    private readonly Entry<PatternDetector>[] _builtIn;
    private readonly Entry<ITextDetector>[] _extensions;

    /// <summary>Liga os detectores embutidos e os próprios aos transformadores dos seus tipos.</summary>
    /// <param name="transformers">Os transformadores do anonimizador.</param>
    /// <param name="extensions">Detectores próprios; o tipo de cada um precisa ter transformador.</param>
    /// <exception cref="ArgumentException">Detector nulo ou de um tipo sem transformador.</exception>
    public TextScanner(TransformerSet transformers, IEnumerable<ITextDetector>? extensions = null)
    {
        ArgumentNullException.ThrowIfNull(transformers);
        _builtIn =
        [
            new(CnpjDetector.Instance, transformers[DocumentType.Cnpj]),
            new(CpfDetector.Instance, transformers[DocumentType.Cpf]),
            new(EmailDetector.Instance, transformers[DocumentType.Email]),
            new(PhoneDetector.Instance, transformers[DocumentType.Phone]),
        ];

        var bound = new List<Entry<ITextDetector>>();
        foreach (ITextDetector detector in extensions ?? [])
        {
            if (detector is null)
            {
                throw new ArgumentException("A lista de detectores contém um item nulo.", nameof(extensions));
            }

            string type = detector.Type;
            if (string.IsNullOrWhiteSpace(type) || !transformers.TryGet(type, out IValueTransformer? transformer))
            {
                throw new ArgumentException($"Não há transformador para o tipo '{type}' do detector.", nameof(extensions));
            }

            bound.Add(new(detector, transformer));
        }

        _extensions = [.. bound];
    }

    /// <inheritdoc/>
    /// <exception cref="InvalidOperationException">Um detector próprio devolveu um trecho fora do texto.</exception>
    public string Scan(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        List<Candidate>? candidates = null;
        if (text.AsSpan().ContainsAny(BuiltInHints))
        {
            var matches = new List<TextMatch>();
            for (int priority = 0; priority < _builtIn.Length; priority++)
            {
                matches.Clear();
                _builtIn[priority].Detector.Collect(text, matches);
                Add(ref candidates, matches, priority, _builtIn[priority].Transformer);
            }
        }

        for (int i = 0; i < _extensions.Length; i++)
        {
            Entry<ITextDetector> extension = _extensions[i];
            TextMatch[] matches =
            [
                .. extension.Detector.Detect(text)
                    ?? throw new InvalidOperationException($"O detector do tipo '{extension.Detector.Type}' devolveu uma lista nula."),
            ];
            foreach (TextMatch match in matches)
            {
                if (match.Length <= 0 || match.Index > text.Length - match.Length)
                {
                    throw new InvalidOperationException(
                        $"O detector do tipo '{extension.Detector.Type}' devolveu um trecho fora do texto.");
                }
            }

            Add(ref candidates, matches, _builtIn.Length + i, extension.Transformer);
        }

        return candidates is null ? text : Replace(text, candidates);
    }

    private static void Add(ref List<Candidate>? candidates, IEnumerable<TextMatch> matches, int priority, IValueTransformer transformer)
    {
        foreach (TextMatch match in matches)
        {
            (candidates ??= []).Add(new Candidate(match.Index, match.Length, priority, transformer));
        }
    }

    private static string Replace(string text, List<Candidate> candidates)
    {
        candidates.Sort(static (a, b) =>
            a.Index != b.Index ? a.Index.CompareTo(b.Index)
            : a.Length != b.Length ? b.Length.CompareTo(a.Length)
            : a.Priority.CompareTo(b.Priority));

        StringBuilder? output = null;
        int copied = 0;
        foreach (Candidate candidate in candidates)
        {
            if (candidate.Index < copied
                || !candidate.Transformer.TryTransform(
                    text.AsSpan(candidate.Index, candidate.Length), ValueKind.Text, out string? substitute))
            {
                continue;
            }

            output ??= new StringBuilder(text.Length + 16);
            output.Append(text, copied, candidate.Index - copied).Append(substitute);
            copied = candidate.Index + candidate.Length;
        }

        return output is null ? text : output.Append(text, copied, text.Length - copied).ToString();
    }

    private readonly record struct Entry<TDetector>(TDetector Detector, IValueTransformer Transformer);

    private readonly record struct Candidate(int Index, int Length, int Priority, IValueTransformer Transformer);
}
