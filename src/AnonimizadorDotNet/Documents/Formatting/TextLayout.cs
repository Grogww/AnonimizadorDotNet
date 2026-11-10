using System.Buffers;
using System.Diagnostics.CodeAnalysis;

namespace AnonimizadorDotNet.Documents.Formatting;

/// <summary>
/// Representação original de um valor: onde ficam os caracteres significativos (dígitos, letras) e quais
/// separadores os cercam, para reaplicar o mesmo formato ao substituto.
/// </summary>
/// <remarks>
/// Só os separadores são guardados; cada posição significativa vira um marcador. Assim o valor original
/// nunca fica retido na representação, que pode viver mais que o próprio valor.
/// </remarks>
internal sealed class TextLayout
{
    private const char Slot = '\0';

    private readonly string _template;

    private TextLayout(string template, int slotCount, ValueKind kind, bool leadingZerosOmitted)
    {
        _template = template;
        SlotCount = slotCount;
        Kind = kind;
        LeadingZerosOmitted = leadingZerosOmitted;
    }

    /// <summary>Quantidade de caracteres significativos na entrada.</summary>
    public int SlotCount { get; }

    /// <summary>Se o valor chegou como texto ou como número.</summary>
    public ValueKind Kind { get; }

    /// <summary>
    /// Verdadeiro quando o valor foi escrito sem os zeros à esquerda (um número, ou um texto sem
    /// preenchimento): a saída também sai sem eles.
    /// </summary>
    public bool LeadingZerosOmitted { get; }

    /// <summary>Verdadeiro quando a entrada tinha algum separador (máscara, espaços).</summary>
    public bool HasSeparators => _template.Length != SlotCount;

    /// <summary>
    /// Separa os caracteres significativos da entrada e guarda a posição dos separadores.
    /// </summary>
    /// <param name="input">Valor de entrada.</param>
    /// <param name="significant">Caracteres que fazem parte do valor (copiados para o destino).</param>
    /// <param name="separators">Caracteres aceitos como máscara. Um número não aceita nenhum.</param>
    /// <param name="kind">Se o valor chegou como texto ou número.</param>
    /// <param name="destination">Recebe os caracteres significativos, na ordem.</param>
    /// <param name="written">Quantidade de caracteres escritos no destino.</param>
    /// <param name="layout">A representação capturada.</param>
    /// <returns>
    /// Falso se a entrada for vazia, tiver um caractere que não é significativo nem separador, não tiver
    /// nenhum caractere significativo ou tiver mais deles do que cabe no destino.
    /// </returns>
    public static bool TryCapture(
        ReadOnlySpan<char> input,
        SearchValues<char> significant,
        SearchValues<char> separators,
        ValueKind kind,
        Span<char> destination,
        out int written,
        [NotNullWhen(true)] out TextLayout? layout)
    {
        ArgumentNullException.ThrowIfNull(significant);
        ArgumentNullException.ThrowIfNull(separators);

        written = 0;
        layout = null;
        if (input.IsEmpty)
        {
            return false;
        }

        char[] template = new char[input.Length];
        for (int i = 0; i < input.Length; i++)
        {
            char c = input[i];
            if (significant.Contains(c))
            {
                if (written == destination.Length)
                {
                    written = 0;
                    return false;
                }

                destination[written++] = c;
                template[i] = Slot;
            }
            else if (kind == ValueKind.Text && separators.Contains(c))
            {
                template[i] = c;
            }
            else
            {
                written = 0;
                return false;
            }
        }

        if (written == 0)
        {
            return false;
        }

        layout = new TextLayout(new string(template), written, kind, leadingZerosOmitted: kind == ValueKind.Number);
        return true;
    }

    /// <summary>
    /// Devolve uma cópia marcada como escrita sem zeros à esquerda. Só faz sentido sem separadores.
    /// </summary>
    /// <exception cref="InvalidOperationException">A representação tem separadores.</exception>
    public TextLayout OmittingLeadingZeros()
    {
        if (HasSeparators)
        {
            throw new InvalidOperationException("Um valor com máscara não pode omitir zeros à esquerda.");
        }

        return new TextLayout(_template, SlotCount, Kind, leadingZerosOmitted: true);
    }

    /// <summary>Reaplica a representação a novos caracteres significativos.</summary>
    /// <param name="significant">
    /// Caracteres do substituto. Devem ser tantos quantos na entrada, exceto quando os zeros à esquerda
    /// foram omitidos: aí o tamanho é livre e os zeros iniciais são removidos (fica ao menos um caractere).
    /// </param>
    /// <exception cref="ArgumentException">A quantidade de caracteres não confere.</exception>
    public string Apply(ReadOnlySpan<char> significant)
    {
        if (LeadingZerosOmitted)
        {
            ReadOnlySpan<char> trimmed = significant.TrimStart('0');
            if (trimmed.IsEmpty)
            {
                return significant.IsEmpty ? string.Empty : "0";
            }

            return new string(trimmed);
        }

        if (significant.Length != SlotCount)
        {
            throw new ArgumentException(
                $"A representação espera {SlotCount} caracteres, mas recebeu {significant.Length}.",
                nameof(significant));
        }

        Span<char> output = _template.Length <= 128 ? stackalloc char[_template.Length] : new char[_template.Length];
        int next = 0;
        for (int i = 0; i < _template.Length; i++)
        {
            output[i] = _template[i] == Slot ? significant[next++] : _template[i];
        }

        return new string(output);
    }
}
