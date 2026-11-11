namespace AnonimizadorDotNet.Transformation;

/// <summary>Os tipos de dado pessoal com transformador embutido.</summary>
internal enum DocumentType
{
    /// <summary>CPF (FF1 na base).</summary>
    Cpf,

    /// <summary>CNPJ numérico ou alfanumérico (FF1 na raiz).</summary>
    Cnpj,

    /// <summary>Telefone brasileiro (FF1 nos 7 dígitos finais).</summary>
    Phone,

    /// <summary>E-mail (HMAC + dicionários).</summary>
    Email,

    /// <summary>Nome de pessoa (HMAC + dicionários).</summary>
    Name,
}

/// <summary>Nomes de tipo dos transformadores embutidos (<see cref="IValueTransformer.Type"/>).</summary>
internal static class DocumentTypeNames
{
    /// <summary>CPF.</summary>
    public const string Cpf = "cpf";

    /// <summary>CNPJ.</summary>
    public const string Cnpj = "cnpj";

    /// <summary>Telefone.</summary>
    public const string Phone = "phone";

    /// <summary>E-mail.</summary>
    public const string Email = "email";

    /// <summary>Nome de pessoa.</summary>
    public const string Name = "name";
}
