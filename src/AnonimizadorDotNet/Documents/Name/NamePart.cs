namespace AnonimizadorDotNet.Documents.Name;

/// <summary>Uma parte de um nome de pessoa.</summary>
/// <remarks>Não sobrescreve <see cref="object.ToString"/>: o valor nunca vai parar em logs.</remarks>
internal sealed class NamePart
{
    internal NamePart(NamePartKind kind, string key, string? literal, bool hasPeriod)
    {
        Kind = kind;
        Key = key;
        Literal = literal;
        HasPeriod = hasPeriod;
    }

    /// <summary>O papel da parte.</summary>
    public NamePartKind Kind { get; }

    /// <summary>
    /// A parte normalizada (sem acentos, em maiúsculas): a mesma para "João", "JOAO" e "joão". É a entrada da
    /// seleção por HMAC, para que a mesma pessoa receba o mesmo substituto em fontes com grafias diferentes.
    /// </summary>
    public string Key { get; }

    /// <summary>O texto original, só para partículas e sufixos (que são preservados); nulo nas demais.</summary>
    public string? Literal { get; }

    /// <summary>Verdadeiro quando a parte terminava em ponto (inicial abreviada).</summary>
    public bool HasPeriod { get; }

    /// <summary>Verdadeiro para as partes que recebem substituto (prenome, sobrenome e inicial).</summary>
    public bool IsReplaceable => Kind is NamePartKind.GivenName or NamePartKind.Surname or NamePartKind.Initial;
}
