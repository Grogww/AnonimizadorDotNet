namespace AnonimizadorDotNet.Detection;

/// <summary>Substitui os dados pessoais encontrados num texto livre (colunas com a ação de varredura).</summary>
/// <remarks>
/// Uma implementação deve ser determinística, segura para várias threads e nunca incluir o texto em
/// exceções. A da biblioteca é o <see cref="TextScanner"/>.
/// </remarks>
internal interface ITextScanner
{
    /// <summary>O texto com os dados pessoais substituídos; o próprio texto, se não houver nenhum.</summary>
    string Scan(string text);
}
