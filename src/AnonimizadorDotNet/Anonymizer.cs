using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using AnonimizadorDotNet.Data;
using AnonimizadorDotNet.Transformation;

namespace AnonimizadorDotNet;

/// <summary>Substitui dados pessoais por valores fictícios válidos e determinísticos, em valores soltos ou em readers.</summary>
/// <remarks>
/// <para>
/// Criado por <see cref="AnonymizerBuilder"/>. Com a mesma chave, o mesmo valor gera sempre o mesmo
/// substituto, entre tabelas, consultas e execuções. Quem tem a chave consegue reverter CPF, CNPJ e
/// telefone: o resultado é pseudonimização (LGPD, art. 13), não anonimização.
/// </para>
/// <para>
/// Pode ser usado por várias threads ao mesmo tempo. <see cref="Dispose"/> zera as subchaves; ele deve
/// acontecer depois de terminar o uso de todos os readers criados por <see cref="Wrap"/>, que a partir daí
/// lançam <see cref="ObjectDisposedException"/>.
/// </para>
/// </remarks>
public sealed class Anonymizer : IDisposable
{
    private readonly TransformerSet _transformers;
    private readonly ReaderContext _context;
    private int _disposed;

    internal Anonymizer(TransformerSet transformers, ReaderContext context)
    {
        _transformers = transformers;
        _context = context;
    }

    /// <summary>Gera o substituto de um valor recebido como texto.</summary>
    /// <param name="value">O valor, com ou sem máscara.</param>
    /// <param name="type">O tipo do valor: um de <see cref="PersonalDataTypes"/> ou o de um transformador próprio.</param>
    /// <param name="substitute">O substituto, na mesma representação do valor.</param>
    /// <returns><see langword="false"/> se o valor não for do tipo (por exemplo, um CPF com dígito verificador errado).</returns>
    /// <exception cref="ArgumentException">Não há transformador para o tipo.</exception>
    /// <exception cref="ObjectDisposedException">O anonimizador já foi descartado.</exception>
    public bool TryAnonymize(ReadOnlySpan<char> value, string type, [NotNullWhen(true)] out string? substitute) =>
        TryAnonymize(value, type, ValueKind.Text, out substitute);

    /// <summary>Gera o substituto de um valor recebido como texto ou como número.</summary>
    /// <param name="value">O valor como chegou.</param>
    /// <param name="type">O tipo do valor: um de <see cref="PersonalDataTypes"/> ou o de um transformador próprio.</param>
    /// <param name="kind">
    /// <see cref="ValueKind.Number"/> para um valor guardado em campo numérico (sem zeros à esquerda);
    /// <see cref="ValueKind.Text"/> para texto.
    /// </param>
    /// <param name="substitute">O substituto, na mesma representação do valor.</param>
    /// <returns><see langword="false"/> se o valor não for do tipo.</returns>
    /// <exception cref="ArgumentException">Não há transformador para o tipo.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="kind"/> desconhecido.</exception>
    /// <exception cref="ObjectDisposedException">O anonimizador já foi descartado.</exception>
    public bool TryAnonymize(ReadOnlySpan<char> value, string type, ValueKind kind, [NotNullWhen(true)] out string? substitute)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(type);
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), "Representação de valor desconhecida.");
        }

        if (!_transformers.TryGet(type, out IValueTransformer? transformer))
        {
            throw new ArgumentException($"Não há transformador para o tipo '{type}'.", nameof(type));
        }

        return transformer.TryTransform(value, kind, out substitute);
    }

    /// <summary>Envolve o reader de uma consulta: os valores lidos dele saem anonimizados.</summary>
    /// <remarks>
    /// <para>
    /// As colunas são classificadas uma vez por resultado, a partir do esquema: regras configuradas, chaves
    /// internas (que passam intactas), nome da coluna, varredura de texto e <see cref="UnclassifiedPolicy"/>.
    /// Metadados e tipos CLR passam intactos.
    /// </para>
    /// <para>
    /// O reader devolvido passa a ser dono do original: descartá-lo descarta os dois. Ele não é seguro para
    /// várias threads, como qualquer reader. Exceções lançadas pelo próprio provedor não passam pela
    /// biblioteca e podem conter valores.
    /// </para>
    /// <para>
    /// Enquanto a varredura de texto livre não tem detectores, colunas de texto não classificadas passam
    /// intactas. Valores que não são do tipo da coluna (um CPF com dígito verificador errado) também passam
    /// como estão. Já um valor do tipo cujo substituto não cabe no tipo CLR da coluna (um CPF numa coluna
    /// <see cref="int"/>) sai como <see cref="DBNull"/>, para que o original não saia intacto.
    /// </para>
    /// </remarks>
    /// <param name="reader">O reader da consulta, antes da primeira leitura.</param>
    /// <returns>Um reader com os mesmos resultados e os valores anonimizados.</returns>
    /// <exception cref="ObjectDisposedException">O anonimizador já foi descartado.</exception>
    public DbDataReader Wrap(DbDataReader reader)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(reader);
        return new AnonymizingDataReader(reader, _context);
    }

    /// <summary>Zera as subchaves; os readers criados por <see cref="Wrap"/> deixam de devolver valores.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _context.Close();
            _transformers.Dispose();
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
}
