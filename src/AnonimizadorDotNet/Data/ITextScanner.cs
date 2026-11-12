namespace AnonimizadorDotNet.Data;

/// <summary>Substitui os dados pessoais encontrados num texto livre (colunas com a ação de varredura).</summary>
/// <remarks>
/// Os detectores chegam na etapa 7; até lá a varredura usa <see cref="PassThroughScanner"/>, que devolve o
/// texto intacto. Uma implementação deve ser determinística, segura para várias threads e nunca incluir o
/// texto em exceções.
/// </remarks>
internal interface ITextScanner
{
    /// <summary>O texto com os dados pessoais substituídos.</summary>
    string Scan(string text);
}

/// <summary>Varredura provisória, sem detectores: devolve o texto como está.</summary>
internal sealed class PassThroughScanner : ITextScanner
{
    private PassThroughScanner()
    {
    }

    /// <summary>A instância única.</summary>
    public static PassThroughScanner Instance { get; } = new();

    /// <inheritdoc/>
    public string Scan(string text) => text;
}
