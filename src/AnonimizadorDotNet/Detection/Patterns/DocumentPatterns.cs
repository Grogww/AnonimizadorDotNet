using System.Text.RegularExpressions;

namespace AnonimizadorDotNet.Detection.Patterns;

/// <summary>Os padrões dos detectores embutidos, gerados em compilação.</summary>
/// <remarks>
/// <para>
/// Os padrões só reconhecem a forma; a validação (dígito verificador, DDD, formato do e-mail) é do
/// <c>XxxFormat.TryParse</c> de cada tipo. Usam <c>[0-9]</c> e não <c>\d</c>, que no .NET aceita dígitos de
/// qualquer escrita.
/// </para>
/// <para>
/// Limites de palavra: um número não pode ter dígito colado antes nem depois, nem um separador
/// (<c>. / -</c>) seguido de dígito, para não pegar pedaço de um número ou de uma máscara maior. Letra
/// colada antes de um número é aceita (<c>CPF12345678909</c>). No CNPJ alfanumérico, nem letra nem dígito
/// podem estar colados. Os limites ficam em <i>lookarounds</i> de tamanho fixo, sem retrocesso caro.
/// </para>
/// </remarks>
internal static partial class DocumentPatterns
{
    private const RegexOptions Options = RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture;

    private const string NumberStart = "(?<![0-9])(?<![0-9][./-])";
    private const string NumberEnd = "(?![0-9])(?![./-][0-9])";
    private const string AlphanumericStart = "(?<![0-9A-Za-z])(?<![0-9A-Za-z][./-])";
    private const string AlphanumericEnd = "(?![0-9A-Za-z])(?![./-][0-9A-Za-z])";

    /// <summary>CPF com máscara (<c>123.456.789-09</c>), sem máscara ou com máscara parcial.</summary>
    [GeneratedRegex(NumberStart + @"[0-9]{3}\.?[0-9]{3}\.?[0-9]{3}-?[0-9]{2}" + NumberEnd, Options)]
    public static partial Regex Cpf();

    /// <summary>CNPJ só de dígitos (<c>12.345.678/0001-95</c>), com ou sem máscara.</summary>
    [GeneratedRegex(NumberStart + @"[0-9]{2}\.?[0-9]{3}\.?[0-9]{3}/?[0-9]{4}-?[0-9]{2}" + NumberEnd, Options)]
    public static partial Regex NumericCnpj();

    /// <summary>
    /// CNPJ alfanumérico (<c>12.ABC.345/01DE-35</c>), com ou sem máscara, em qualquer caixa; os dois
    /// verificadores são sempre dígitos. Também casa o CNPJ só de dígitos: o detector exige uma letra.
    /// </summary>
    [GeneratedRegex(
        AlphanumericStart + @"[0-9A-Za-z]{2}\.?[0-9A-Za-z]{3}\.?[0-9A-Za-z]{3}/?[0-9A-Za-z]{4}-?[0-9]{2}" + AlphanumericEnd,
        Options)]
    public static partial Regex AlphanumericCnpj();
}
