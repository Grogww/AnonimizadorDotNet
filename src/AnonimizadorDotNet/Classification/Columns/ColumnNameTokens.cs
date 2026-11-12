using AnonimizadorDotNet.Documents.Formatting;

namespace AnonimizadorDotNet.Classification.Columns;

/// <summary>Quebra um nome de coluna em partes comparáveis.</summary>
/// <remarks>
/// Separa por qualquer caractere que não seja letra ou dígito (<c>nr_cpf</c>, <c>e-mail</c>), por mudança
/// de minúscula para maiúscula (<c>NrCpf</c>), pelo fim de uma sigla (<c>CPFCliente</c> ↦ <c>cpf</c>,
/// <c>cliente</c>) e entre letras e dígitos (<c>telefone2</c>). As partes saem sem acentos e em minúsculas.
/// </remarks>
internal static class ColumnNameTokens
{
    /// <summary>As partes do nome, na ordem; vazio se o nome não tiver letras nem dígitos.</summary>
    public static string[] Split(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        string plain = TextNormalization.RemoveDiacritics(name);
        var tokens = new List<string>();
        int start = -1;

        for (int i = 0; i < plain.Length; i++)
        {
            if (!char.IsLetterOrDigit(plain[i]))
            {
                Flush(i);
                continue;
            }

            if (start >= 0 && IsBoundary(plain, i))
            {
                Flush(i);
            }

            if (start < 0)
            {
                start = i;
            }
        }

        Flush(plain.Length);
        return [.. tokens];

        void Flush(int end)
        {
            if (start >= 0)
            {
                tokens.Add(plain[start..end].ToLowerInvariant());
                start = -1;
            }
        }
    }

    private static bool IsBoundary(string text, int index)
    {
        char previous = text[index - 1];
        char current = text[index];

        if (char.IsDigit(previous) != char.IsDigit(current))
        {
            return true;
        }

        if (char.IsLower(previous) && char.IsUpper(current))
        {
            return true;
        }

        // Fim de sigla: a última maiúscula de "CPFCliente" já começa a palavra seguinte.
        return char.IsUpper(previous) && char.IsUpper(current)
            && index + 1 < text.Length && char.IsLower(text[index + 1]);
    }
}
