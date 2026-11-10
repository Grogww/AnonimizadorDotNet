namespace AnonimizadorDotNet.Cryptography.Keys;

/// <summary>
/// Rótulos de derivação (o parâmetro <c>info</c> do HKDF): um por par finalidade + tipo de documento.
/// </summary>
/// <remarks>
/// <para>
/// Cada rótulo gera uma subchave independente da chave mestra. A finalidade (<c>ff1</c> ou <c>hmac</c>)
/// faz parte do rótulo para que um mesmo tipo nunca use a mesma chave em dois algoritmos.
/// </para>
/// <para>
/// Os rótulos fazem parte do determinismo da biblioteca: alterar qualquer um deles muda todos os
/// substitutos daquele tipo. Por isso são versionados (<c>v1</c>) e fixados por testes com valores
/// conhecidos. Um rótulo novo pode ser acrescentado; um existente não deve mudar.
/// </para>
/// </remarks>
internal static class KeyLabels
{
    /// <summary>FF1 sobre os 9 dígitos da base do CPF.</summary>
    public static ReadOnlySpan<byte> Ff1Cpf => "AnonimizadorDotNet/v1/ff1/cpf"u8;

    /// <summary>FF1 sobre a raiz numérica (8 dígitos) do CNPJ.</summary>
    public static ReadOnlySpan<byte> Ff1Cnpj => "AnonimizadorDotNet/v1/ff1/cnpj"u8;

    /// <summary>FF1 sobre a raiz alfanumérica (8 posições em base 36) do CNPJ.</summary>
    public static ReadOnlySpan<byte> Ff1CnpjAlphanumeric => "AnonimizadorDotNet/v1/ff1/cnpj-alfanumerico"u8;

    /// <summary>FF1 sobre os 8 dígitos finais do telefone.</summary>
    public static ReadOnlySpan<byte> Ff1Phone => "AnonimizadorDotNet/v1/ff1/telefone"u8;

    /// <summary>Seleção por HMAC das partes do nome.</summary>
    public static ReadOnlySpan<byte> HmacName => "AnonimizadorDotNet/v1/hmac/nome"u8;

    /// <summary>Seleção por HMAC da parte local e do domínio do e-mail.</summary>
    public static ReadOnlySpan<byte> HmacEmail => "AnonimizadorDotNet/v1/hmac/email"u8;
}
