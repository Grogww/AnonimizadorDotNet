namespace AnonimizadorDotNet.Documents.Name;

/// <summary>O papel de cada parte de um nome de pessoa.</summary>
internal enum NamePartKind
{
    /// <summary>Prenome (o primeiro nome, ou os primeiros num prenome composto como "João Pedro"). Substituído.</summary>
    GivenName,

    /// <summary>Sobrenome. Substituído.</summary>
    Surname,

    /// <summary>Inicial abreviada ("J." ou "J"). Substituída por outra inicial.</summary>
    Initial,

    /// <summary>Partícula de ligação ("da", "de", "do", "das", "dos", "e"). Preservada como está.</summary>
    Particle,

    /// <summary>Sufixo de parentesco ("Filho", "Júnior", "Neto", "Sobrinho"…). Preservado como está.</summary>
    Suffix,
}

/// <summary>Sexo associado ao prenome, pelos dicionários embutidos.</summary>
internal enum Gender
{
    /// <summary>Prenome fora dos dicionários.</summary>
    Unknown,

    /// <summary>Prenome masculino.</summary>
    Male,

    /// <summary>Prenome feminino.</summary>
    Female,
}

/// <summary>Se o nome é escrito com ou sem acentos.</summary>
internal enum AccentStyle
{
    /// <summary>Não dá para saber (nenhuma parte acentuada nem uma que normalmente teria acento).</summary>
    Unknown,

    /// <summary>Alguma parte tem acento.</summary>
    Accented,

    /// <summary>Nenhuma parte tem acento, mas alguma normalmente teria ("JOAO"): a fonte remove acentos.</summary>
    Unaccented,
}
