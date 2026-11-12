namespace AnonimizadorDotNet;

/// <summary>O que fazer com as colunas que nenhuma regra nem a heurística por nome classificou.</summary>
public enum UnclassifiedPolicy
{
    /// <summary>
    /// Passar (padrão): as colunas de texto são varridas em busca de dados pessoais; as demais passam
    /// intactas.
    /// </summary>
    Pass,

    /// <summary>
    /// Bloquear: nada que não tenha sido classificado como seguro sai. Só passam as chaves internas e as
    /// colunas ignoradas por configuração; as demais saem como <see cref="DBNull"/>, e só são varridas as
    /// colunas configuradas para varredura.
    /// </summary>
    Block,
}
