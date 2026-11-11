using System.Text;

namespace AnonimizadorDotNet.Transformation;

/// <summary>Contextos da seleção por HMAC (<c>HmacSelector</c>): um por sorteio independente.</summary>
/// <remarks>
/// <para>
/// Cada tipo já tem subchave própria (<c>KeyLabels.HmacName</c>, <c>KeyLabels.HmacEmail</c>); o contexto
/// separa os sorteios dentro do mesmo valor, para que o prenome, cada sobrenome e cada dígito saiam de forma
/// independente.
/// </para>
/// <para>
/// Como os rótulos de chave, os contextos fazem parte do determinismo e são fixados por valores dourados:
/// mudar um deles altera todos os substitutos daquele tipo.
/// </para>
/// </remarks>
internal static class HmacContexts
{
    /// <summary>Nome: escolha entre prenomes masculinos (0) e femininos (1) quando o sexo é desconhecido.</summary>
    public static ReadOnlySpan<byte> NameGender => "sexo"u8;

    /// <summary>E-mail: escolha entre prenomes masculinos (0) e femininos (1) para a parte local.</summary>
    public static ReadOnlySpan<byte> EmailGender => "local/sexo"u8;

    /// <summary>E-mail: prenome da parte local.</summary>
    public static ReadOnlySpan<byte> EmailGivenName => "local/prenome"u8;

    /// <summary>E-mail: sobrenome da parte local.</summary>
    public static ReadOnlySpan<byte> EmailSurname => "local/sobrenome"u8;

    /// <summary>E-mail: começo do domínio fictício.</summary>
    public static ReadOnlySpan<byte> EmailDomainStart => "dominio/inicio"u8;

    /// <summary>E-mail: final do domínio fictício.</summary>
    public static ReadOnlySpan<byte> EmailDomainEnding => "dominio/fim"u8;

    /// <summary>Nome: a parte na posição <paramref name="position"/> (contando partículas e sufixos).</summary>
    public static byte[] NamePart(int position) => Encoding.ASCII.GetBytes($"parte/{position}");

    /// <summary>E-mail: o dígito na posição <paramref name="position"/> do fim da parte local.</summary>
    public static byte[] EmailDigit(int position) => Encoding.ASCII.GetBytes($"local/digito/{position}");
}
