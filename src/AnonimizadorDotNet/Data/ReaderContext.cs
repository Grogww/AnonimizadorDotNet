using AnonimizadorDotNet.Classification;

namespace AnonimizadorDotNet.Data;

/// <summary>O que os readers de um anonimizador compartilham: o classificador, a varredura e o estado de vida.</summary>
/// <remarks>
/// Depois de <see cref="Close"/> (o anonimizador foi descartado e as subchaves zeradas), todo reader criado
/// a partir deste contexto lança <see cref="ObjectDisposedException"/> em vez de devolver valores.
/// </remarks>
internal sealed class ReaderContext
{
    private volatile bool _closed;

    /// <summary>Cria o contexto.</summary>
    public ReaderContext(ColumnClassifier classifier, ITextScanner scanner)
    {
        ArgumentNullException.ThrowIfNull(classifier);
        ArgumentNullException.ThrowIfNull(scanner);
        Classifier = classifier;
        Scanner = scanner;
    }

    /// <summary>O classificador de colunas.</summary>
    public ColumnClassifier Classifier { get; }

    /// <summary>A varredura de texto livre.</summary>
    public ITextScanner Scanner { get; }

    /// <summary>Encerra o contexto: os readers deixam de devolver valores.</summary>
    public void Close() => _closed = true;

    /// <summary>Lança se o contexto já foi encerrado.</summary>
    /// <exception cref="ObjectDisposedException">O anonimizador já foi descartado.</exception>
    public void ThrowIfClosed()
    {
        if (_closed)
        {
            throw new ObjectDisposedException("Anonymizer", "O anonimizador já foi descartado.");
        }
    }
}
