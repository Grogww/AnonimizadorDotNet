using System.Text;

namespace AnonimizadorDotNet.Classification.Rules;

/// <summary>O alvo de uma regra explícita: <c>coluna</c>, <c>tabela.coluna</c> ou <c>schema.tabela.coluna</c>.</summary>
/// <remarks>
/// Cada parte pode vir entre <c>[ ]</c>, <c>" "</c> ou <c>` `</c>, o que permite pontos dentro do nome. A
/// comparação entre alvos ignora maiúsculas e minúsculas.
/// </remarks>
internal sealed class ColumnTarget
{
    /// <summary>Separador das partes na chave de comparação; não aparece em nomes de coluna.</summary>
    private const char KeySeparator = '\0';

    private ColumnTarget(string[] parts)
    {
        Parts = parts;
        Key = BuildKey(parts);
    }

    /// <summary>As partes do alvo, da mais geral (schema) para a coluna.</summary>
    public IReadOnlyList<string> Parts { get; }

    /// <summary>Chave de comparação, a mesma de <see cref="BuildKey"/>.</summary>
    internal string Key { get; }

    /// <summary>Interpreta o texto de um alvo.</summary>
    /// <exception cref="ArgumentException">Mais de três partes, parte vazia ou delimitador sem fechamento.</exception>
    public static ColumnTarget Parse(string target)
    {
        ArgumentNullException.ThrowIfNull(target);
        var parts = new List<string>();
        var current = new StringBuilder();
        char closing = '\0';

        foreach (char c in target)
        {
            if (closing != '\0')
            {
                if (c == closing)
                {
                    closing = '\0';
                }
                else
                {
                    current.Append(c);
                }

                continue;
            }

            switch (c)
            {
                case '[':
                    closing = ']';
                    break;
                case '"':
                case '`':
                    closing = c;
                    break;
                case '.':
                    parts.Add(current.ToString().Trim());
                    current.Clear();
                    break;
                default:
                    current.Append(c);
                    break;
            }
        }

        if (closing != '\0')
        {
            throw new ArgumentException("O alvo da regra tem um delimitador sem fechamento.", nameof(target));
        }

        parts.Add(current.ToString().Trim());
        if (parts.Count > 3 || parts.Exists(string.IsNullOrEmpty))
        {
            throw new ArgumentException(
                "O alvo da regra deve ser 'coluna', 'tabela.coluna' ou 'schema.tabela.coluna', sem partes vazias.",
                nameof(target));
        }

        return new ColumnTarget([.. parts]);
    }

    /// <summary>A chave de comparação de um alvo com as partes informadas.</summary>
    internal static string BuildKey(params string[] parts) => string.Join(KeySeparator, parts);

    /// <summary>O alvo, com as partes separadas por ponto.</summary>
    public override string ToString() => string.Join('.', Parts);
}
