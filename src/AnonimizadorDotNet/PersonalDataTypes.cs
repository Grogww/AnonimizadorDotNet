using AnonimizadorDotNet.Transformation;

namespace AnonimizadorDotNet;

/// <summary>Os nomes dos tipos de dado pessoal com transformador embutido.</summary>
/// <remarks>
/// Usados em <see cref="AnonymizerBuilder.TransformColumn"/> e <see cref="Anonymizer.TryAnonymize(ReadOnlySpan{char}, string, out string?)"/>.
/// A comparação entre nomes de tipo ignora maiúsculas e minúsculas.
/// </remarks>
public static class PersonalDataTypes
{
    /// <summary>CPF: a base é cifrada com FF1 e os dígitos verificadores, recalculados.</summary>
    public const string Cpf = DocumentTypeNames.Cpf;

    /// <summary>CNPJ numérico ou alfanumérico: a raiz é cifrada com FF1 e a ordem (<c>0001</c>), preservada.</summary>
    public const string Cnpj = DocumentTypeNames.Cnpj;

    /// <summary>Telefone brasileiro: código do país, DDD e início do assinante preservados; o final, cifrado.</summary>
    public const string Phone = DocumentTypeNames.Phone;

    /// <summary>E-mail: parte local e domínio corporativo substituídos; provedores públicos preservados.</summary>
    public const string Email = DocumentTypeNames.Email;

    /// <summary>Nome de pessoa: prenome e sobrenomes substituídos, mantendo partes, caixa e acentos.</summary>
    public const string Name = DocumentTypeNames.Name;
}
