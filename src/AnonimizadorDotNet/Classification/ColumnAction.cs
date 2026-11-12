namespace AnonimizadorDotNet.Classification;

/// <summary>O que fazer com os valores de uma coluna.</summary>
internal enum ColumnAction
{
    /// <summary>Substituir cada valor com os transformadores da decisão.</summary>
    Transform,

    /// <summary>Varrer cada valor como texto livre, substituindo os dados pessoais encontrados.</summary>
    Scan,

    /// <summary>Passar os valores intactos.</summary>
    Pass,

    /// <summary>Não deixar os valores saírem.</summary>
    Block,
}

/// <summary>Qual etapa da precedência decidiu a coluna.</summary>
internal enum ClassificationReason
{
    /// <summary>Uma regra de configuração explícita.</summary>
    Explicit,

    /// <summary>A proteção das chaves internas do sistema.</summary>
    InternalKey,

    /// <summary>A heurística por nome de coluna.</summary>
    ColumnName,

    /// <summary>Coluna de texto não classificada, mandada para a varredura.</summary>
    TextScan,

    /// <summary>A política para colunas não classificadas.</summary>
    Policy,
}

/// <summary>O que fazer com as colunas que nenhuma regra classificou.</summary>
/// <remarks>Vira a política pública <c>UnclassifiedPolicy</c> na API do anonimizador.</remarks>
internal enum UnclassifiedBehavior
{
    /// <summary>Colunas de texto vão para a varredura; as demais passam intactas.</summary>
    Pass,

    /// <summary>
    /// Nada que não tenha sido classificado como seguro sai: só passam as chaves internas e as colunas
    /// ignoradas por configuração, e só são varridas as configuradas para varredura.
    /// </summary>
    Block,
}
