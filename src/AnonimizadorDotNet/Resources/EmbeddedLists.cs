using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Text;

namespace AnonimizadorDotNet.Resources;

/// <summary>Os dicionários embutidos no assembly, carregados uma única vez e só quando usados.</summary>
/// <remarks>
/// <para>
/// Cada arquivo em <c>Resources/*.txt</c> tem um item por linha, em UTF-8; linhas vazias e iniciadas por
/// <c>#</c> são ignoradas. A ordem dos itens faz parte do contrato: a seleção por HMAC escolhe um índice,
/// então reordenar, inserir no meio ou remover itens muda os substitutos. Itens novos vão no fim.
/// </para>
/// <para>Os arquivos de nomes seguem a frequência publicada pelo IBGE; ver o <c>NOTICE</c>.</para>
/// </remarks>
internal static class EmbeddedLists
{
    private static readonly Lazy<ImmutableArray<string>> _maleGivenNames = new(() => Load("prenomes-masculinos.txt"));
    private static readonly Lazy<ImmutableArray<string>> _femaleGivenNames = new(() => Load("prenomes-femininos.txt"));
    private static readonly Lazy<ImmutableArray<string>> _surnames = new(() => Load("sobrenomes.txt"));
    private static readonly Lazy<FrozenSet<string>> _publicEmailProviders = new(() => LoadSet("provedores-email.txt"));
    private static readonly Lazy<FrozenSet<string>> _domainSuffixes = new(() => LoadSet("sufixos-dominio.txt"));
    private static readonly Lazy<ImmutableArray<string>> _domainStarts = new(() => Load("dominio-inicios.txt"));
    private static readonly Lazy<ImmutableArray<string>> _domainEndings = new(() => Load("dominio-finais.txt"));

    /// <summary>Prenomes masculinos, com acentos.</summary>
    public static ImmutableArray<string> MaleGivenNames => _maleGivenNames.Value;

    /// <summary>Prenomes femininos, com acentos.</summary>
    public static ImmutableArray<string> FemaleGivenNames => _femaleGivenNames.Value;

    /// <summary>Sobrenomes, com acentos.</summary>
    public static ImmutableArray<string> Surnames => _surnames.Value;

    /// <summary>Domínios de provedores públicos de e-mail, em minúsculas (preservados no substituto).</summary>
    public static FrozenSet<string> PublicEmailProviders => _publicEmailProviders.Value;

    /// <summary>Sufixos de domínio com mais de um rótulo (<c>com.br</c>, <c>gov.br</c>…), em minúsculas.</summary>
    public static FrozenSet<string> DomainSuffixes => _domainSuffixes.Value;

    /// <summary>Começos de nomes de domínio fictícios.</summary>
    public static ImmutableArray<string> DomainStarts => _domainStarts.Value;

    /// <summary>Finais de nomes de domínio fictícios.</summary>
    public static ImmutableArray<string> DomainEndings => _domainEndings.Value;

    /// <summary>Lê um dicionário embutido pelo nome do arquivo.</summary>
    /// <exception cref="InvalidOperationException">O recurso não existe no assembly.</exception>
    internal static ImmutableArray<string> Load(string fileName)
    {
        string resourceName = "AnonimizadorDotNet.Resources." + fileName;
        using Stream stream = typeof(EmbeddedLists).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Recurso embutido não encontrado: {resourceName}.");
        using var reader = new StreamReader(stream, Encoding.UTF8);

        ImmutableArray<string>.Builder items = ImmutableArray.CreateBuilder<string>();
        while (reader.ReadLine() is { } line)
        {
            string item = line.Trim();
            if (item.Length > 0 && item[0] != '#')
            {
                items.Add(item.Normalize(NormalizationForm.FormC));
            }
        }

        return items.ToImmutable();
    }

    private static FrozenSet<string> LoadSet(string fileName) =>
        Load(fileName).ToFrozenSet(StringComparer.Ordinal);
}
