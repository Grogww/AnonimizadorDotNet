using System.Collections;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.Common;
using AnonimizadorDotNet.Classification;
using AnonimizadorDotNet.Transformation;

namespace AnonimizadorDotNet.Data;

/// <summary>Envolve o reader de uma consulta e devolve os valores já anonimizados, coluna a coluna.</summary>
/// <remarks>
/// <para>
/// As colunas são classificadas uma vez por resultado (na construção e a cada <see cref="NextResult"/>), só
/// a partir do esquema. Os metadados (<see cref="GetSchemaTable"/>, nomes, tipos) passam intactos, e o tipo
/// CLR de cada valor é preservado.
/// </para>
/// <para>Por coluna, conforme a decisão:</para>
/// <list type="bullet">
/// <item><description>passar: todos os métodos vão direto ao reader original;</description></item>
/// <item><description>bloquear: o valor é <see cref="DBNull"/>, sem ler o original;</description></item>
/// <item><description>
/// transformar: o substituto do primeiro transformador que aceitar o valor. Um valor que nenhum aceita passa
/// como está (decisão provisória); um aceito cujo substituto não cabe no tipo da coluna vira
/// <see cref="DBNull"/>, para não deixar sair o original (ver <see cref="Resolve"/>);
/// </description></item>
/// <item><description>varrer: o texto passa pelo <see cref="ITextScanner"/>.</description></item>
/// </list>
/// <para>
/// Fechado por padrão: todo membro de <see cref="DbDataReader"/> que lê valor é sobrescrito, e um teste de
/// reflexão falha se o .NET acrescentar um novo. Cada valor é calculado uma vez por linha. Como qualquer
/// reader, não é seguro para várias threads. Descartá-lo descarta o reader original.
/// </para>
/// </remarks>
internal sealed class AnonymizingDataReader : DbDataReader, IDbColumnSchemaGenerator
{
    private readonly DbDataReader _inner;
    private readonly ReaderContext _context;
    private ColumnDecision[] _decisions = [];
    private object?[] _row = [];
    private bool _disposed;

    /// <summary>Envolve o reader e classifica as colunas do primeiro resultado.</summary>
    /// <exception cref="ObjectDisposedException">O anonimizador já foi descartado.</exception>
    public AnonymizingDataReader(DbDataReader inner, ReaderContext context)
    {
        ArgumentNullException.ThrowIfNull(inner);
        ArgumentNullException.ThrowIfNull(context);
        _inner = inner;
        _context = context;
        Classify();
    }

    /// <summary>As decisões do resultado atual (para testes).</summary>
    internal IReadOnlyList<ColumnDecision> Decisions => _decisions;

    // ---- Metadados e navegação: repassados ao reader original ----

    /// <inheritdoc/>
    public override int Depth => _inner.Depth;

    /// <inheritdoc/>
    public override int FieldCount => _inner.FieldCount;

    /// <inheritdoc/>
    public override int VisibleFieldCount => _inner.VisibleFieldCount;

    /// <inheritdoc/>
    public override bool HasRows => _inner.HasRows;

    /// <inheritdoc/>
    public override bool IsClosed => _inner.IsClosed;

    /// <inheritdoc/>
    public override int RecordsAffected => _inner.RecordsAffected;

    /// <inheritdoc/>
    public override string GetDataTypeName(int ordinal) => _inner.GetDataTypeName(ordinal);

    /// <inheritdoc/>
    public override Type GetFieldType(int ordinal) => _inner.GetFieldType(ordinal);

    /// <inheritdoc/>
    public override Type GetProviderSpecificFieldType(int ordinal) =>
        IsPass(ordinal) ? _inner.GetProviderSpecificFieldType(ordinal) : _inner.GetFieldType(ordinal);

    /// <inheritdoc/>
    public override string GetName(int ordinal) => _inner.GetName(ordinal);

    /// <inheritdoc/>
    public override int GetOrdinal(string name) => _inner.GetOrdinal(name);

    /// <inheritdoc/>
    public override DataTable? GetSchemaTable() => _inner.GetSchemaTable();

    /// <inheritdoc/>
    public override Task<DataTable?> GetSchemaTableAsync(CancellationToken cancellationToken = default) =>
        _inner.GetSchemaTableAsync(cancellationToken);

    /// <inheritdoc/>
    public override Task<ReadOnlyCollection<DbColumn>> GetColumnSchemaAsync(CancellationToken cancellationToken = default) =>
        _inner.GetColumnSchemaAsync(cancellationToken);

    /// <summary>O esquema do reader original.</summary>
    public ReadOnlyCollection<DbColumn> GetColumnSchema() => _inner.GetColumnSchema();

    /// <inheritdoc/>
    public override bool Read()
    {
        _context.ThrowIfClosed();
        bool hasRow = _inner.Read();
        Array.Clear(_row);
        return hasRow;
    }

    /// <inheritdoc/>
    public override async Task<bool> ReadAsync(CancellationToken cancellationToken)
    {
        _context.ThrowIfClosed();
        bool hasRow = await _inner.ReadAsync(cancellationToken).ConfigureAwait(false);
        Array.Clear(_row);
        return hasRow;
    }

    /// <inheritdoc/>
    public override bool NextResult()
    {
        _context.ThrowIfClosed();
        bool hasResult = _inner.NextResult();
        OnNextResult(hasResult);
        return hasResult;
    }

    /// <inheritdoc/>
    public override async Task<bool> NextResultAsync(CancellationToken cancellationToken)
    {
        _context.ThrowIfClosed();
        bool hasResult = await _inner.NextResultAsync(cancellationToken).ConfigureAwait(false);
        OnNextResult(hasResult);
        return hasResult;
    }

    /// <inheritdoc/>
    public override IEnumerator GetEnumerator() => new DbEnumerator(this, closeReader: false);

    // ---- Valores ----

    /// <inheritdoc/>
    public override object this[int ordinal] => GetValue(ordinal);

    /// <inheritdoc/>
    public override object this[string name] => GetValue(GetOrdinal(name));

    /// <inheritdoc/>
    public override object GetValue(int ordinal) => IsPass(ordinal) ? _inner.GetValue(ordinal) : Resolve(ordinal);

    /// <inheritdoc/>
    public override int GetValues(object[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        int count = Math.Min(values.Length, FieldCount);
        for (int i = 0; i < count; i++)
        {
            values[i] = GetValue(i);
        }

        return count;
    }

    /// <inheritdoc/>
    public override object GetProviderSpecificValue(int ordinal) =>
        IsPass(ordinal) ? _inner.GetProviderSpecificValue(ordinal) : Resolve(ordinal);

    /// <inheritdoc/>
    public override int GetProviderSpecificValues(object[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        int count = Math.Min(values.Length, FieldCount);
        for (int i = 0; i < count; i++)
        {
            values[i] = GetProviderSpecificValue(i);
        }

        return count;
    }

    /// <inheritdoc/>
    public override bool IsDBNull(int ordinal)
    {
        if (IsPass(ordinal))
        {
            return _inner.IsDBNull(ordinal);
        }

        return _decisions[ordinal].Action == ColumnAction.Block || Resolve(ordinal) is DBNull;
    }

    /// <inheritdoc/>
    public override Task<bool> IsDBNullAsync(int ordinal, CancellationToken cancellationToken) =>
        IsPass(ordinal) ? _inner.IsDBNullAsync(ordinal, cancellationToken) : FromResult(() => IsDBNull(ordinal), cancellationToken);

    /// <inheritdoc/>
    public override T GetFieldValue<T>(int ordinal) =>
        IsPass(ordinal) ? _inner.GetFieldValue<T>(ordinal) : Typed<T>(ordinal);

    /// <inheritdoc/>
    public override Task<T> GetFieldValueAsync<T>(int ordinal, CancellationToken cancellationToken) =>
        IsPass(ordinal) ? _inner.GetFieldValueAsync<T>(ordinal, cancellationToken) : FromResult(() => Typed<T>(ordinal), cancellationToken);

    /// <inheritdoc/>
    public override bool GetBoolean(int ordinal) => IsPass(ordinal) ? _inner.GetBoolean(ordinal) : Typed<bool>(ordinal);

    /// <inheritdoc/>
    public override byte GetByte(int ordinal) => IsPass(ordinal) ? _inner.GetByte(ordinal) : Typed<byte>(ordinal);

    /// <inheritdoc/>
    public override char GetChar(int ordinal) => IsPass(ordinal) ? _inner.GetChar(ordinal) : Typed<char>(ordinal);

    /// <inheritdoc/>
    public override DateTime GetDateTime(int ordinal) => IsPass(ordinal) ? _inner.GetDateTime(ordinal) : Typed<DateTime>(ordinal);

    /// <inheritdoc/>
    public override decimal GetDecimal(int ordinal) => IsPass(ordinal) ? _inner.GetDecimal(ordinal) : Typed<decimal>(ordinal);

    /// <inheritdoc/>
    public override double GetDouble(int ordinal) => IsPass(ordinal) ? _inner.GetDouble(ordinal) : Typed<double>(ordinal);

    /// <inheritdoc/>
    public override float GetFloat(int ordinal) => IsPass(ordinal) ? _inner.GetFloat(ordinal) : Typed<float>(ordinal);

    /// <inheritdoc/>
    public override Guid GetGuid(int ordinal) => IsPass(ordinal) ? _inner.GetGuid(ordinal) : Typed<Guid>(ordinal);

    /// <inheritdoc/>
    public override short GetInt16(int ordinal) => IsPass(ordinal) ? _inner.GetInt16(ordinal) : Typed<short>(ordinal);

    /// <inheritdoc/>
    public override int GetInt32(int ordinal) => IsPass(ordinal) ? _inner.GetInt32(ordinal) : Typed<int>(ordinal);

    /// <inheritdoc/>
    public override long GetInt64(int ordinal) => IsPass(ordinal) ? _inner.GetInt64(ordinal) : Typed<long>(ordinal);

    /// <inheritdoc/>
    public override string GetString(int ordinal)
    {
        if (IsPass(ordinal))
        {
            return _inner.GetString(ordinal);
        }

        return Resolve(ordinal) is char[] chars ? new string(chars) : Typed<string>(ordinal);
    }

    /// <inheritdoc/>
    public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length)
    {
        if (IsPass(ordinal))
        {
            return _inner.GetBytes(ordinal, dataOffset, buffer, bufferOffset, length);
        }

        return CopyTo(Typed<byte[]>(ordinal), dataOffset, buffer, bufferOffset, length);
    }

    /// <inheritdoc/>
    public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length)
    {
        if (IsPass(ordinal))
        {
            return _inner.GetChars(ordinal, dataOffset, buffer, bufferOffset, length);
        }

        char[] chars = Resolve(ordinal) is string text ? text.ToCharArray() : Typed<char[]>(ordinal);
        return CopyTo(chars, dataOffset, buffer, bufferOffset, length);
    }

    /// <inheritdoc/>
    public override Stream GetStream(int ordinal) =>
        IsPass(ordinal) ? _inner.GetStream(ordinal) : new MemoryStream(Typed<byte[]>(ordinal), writable: false);

    /// <inheritdoc/>
    public override TextReader GetTextReader(int ordinal)
    {
        if (IsPass(ordinal))
        {
            return _inner.GetTextReader(ordinal);
        }

        return Resolve(ordinal) is DBNull ? new StringReader(string.Empty) : new StringReader(GetString(ordinal));
    }

    /// <inheritdoc/>
    protected override DbDataReader GetDbDataReader(int ordinal) =>
        IsPass(ordinal)
            ? _inner.GetData(ordinal)
            : throw new NotSupportedException($"A coluna #{ordinal} não pode ser lida como um reader aninhado.");

    // ---- Ciclo de vida ----

    /// <inheritdoc/>
    public override void Close() => _inner.Close();

    /// <inheritdoc/>
    public override Task CloseAsync() => _inner.CloseAsync();

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            _inner.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc/>
    public override async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true;
            await _inner.DisposeAsync().ConfigureAwait(false);
        }

        await base.DisposeAsync().ConfigureAwait(false);
    }

    // ---- Decisão e resolução ----

    private void Classify()
    {
        _context.ThrowIfClosed();
        _decisions = _context.Classifier.Classify(ColumnSchema.Read(_inner));
        _row = new object?[_decisions.Length];
    }

    private void OnNextResult(bool hasResult)
    {
        if (hasResult)
        {
            Classify();
        }
        else
        {
            _decisions = [];
            _row = [];
        }
    }

    private bool IsPass(int ordinal)
    {
        _context.ThrowIfClosed();
        if ((uint)ordinal >= (uint)_decisions.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(ordinal), $"Não há coluna na posição {ordinal}.");
        }

        return _decisions[ordinal].Action == ColumnAction.Pass;
    }

    /// <summary>O valor anonimizado de uma coluna que não é passada, calculado uma vez por linha.</summary>
    /// <remarks>
    /// <para>
    /// Valor que nenhum transformador aceita (CPF com DV errado, texto que não é do tipo): passa como está
    /// (decisão provisória D2 do plano da etapa 6; <c>NULL</c> quebraria os dados).
    /// </para>
    /// <para>
    /// Valor aceito cujo substituto não cabe no tipo da coluna (um CPF válido numa coluna <see cref="int"/>):
    /// vira <see cref="DBNull"/> (D6). Devolver o original entregaria um documento válido intacto.
    /// </para>
    /// <para>As duas regras ficam em <see cref="Transform"/>, o único ponto a mudar se a decisão for outra.</para>
    /// </remarks>
    private object Resolve(int ordinal)
    {
        if (_row[ordinal] is { } cached)
        {
            return cached;
        }

        ColumnDecision decision = _decisions[ordinal];
        object value;
        if (decision.Action == ColumnAction.Block)
        {
            value = DBNull.Value;
        }
        else
        {
            object raw = _inner.GetValue(ordinal);
            value = raw is DBNull ? raw : decision.Action switch
            {
                ColumnAction.Transform => Transform(raw, decision.Transformers),
                ColumnAction.Scan => Scan(raw),
                _ => throw new InvalidOperationException($"Ação desconhecida para a coluna #{ordinal}."),
            };
        }

        _row[ordinal] = value;
        return value;
    }

    private static object Transform(object raw, IReadOnlyList<IValueTransformer> transformers)
    {
        if (!ValueCodec.TryEncode(raw, out string text, out ValueKind kind))
        {
            return raw;
        }

        foreach (IValueTransformer transformer in transformers)
        {
            if (transformer.TryTransform(text, kind, out string? substitute))
            {
                return ValueCodec.TryDecode(substitute, raw, out object decoded) ? decoded : DBNull.Value;
            }
        }

        return raw;
    }

    private object Scan(object raw) => raw switch
    {
        string text => _context.Scanner.Scan(text),
        char[] chars => _context.Scanner.Scan(new string(chars)).ToCharArray(),
        _ => raw,
    };

    private T Typed<T>(int ordinal) => Resolve(ordinal) switch
    {
        T value => value,
        DBNull => throw new InvalidCastException($"A coluna #{ordinal} é nula."),
        _ => throw new InvalidCastException($"A coluna #{ordinal} não pode ser lida como {typeof(T).Name}."),
    };

    private static long CopyTo<T>(T[] source, long dataOffset, T[]? buffer, int bufferOffset, int length)
    {
        if (buffer is null)
        {
            return source.Length;
        }

        ArgumentOutOfRangeException.ThrowIfNegative(dataOffset);
        ArgumentOutOfRangeException.ThrowIfNegative(bufferOffset);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        if (dataOffset >= source.Length)
        {
            return 0;
        }

        int count = (int)Math.Min(Math.Min(length, source.Length - dataOffset), buffer.Length - bufferOffset);
        if (count <= 0)
        {
            return 0;
        }

        Array.Copy(source, dataOffset, buffer, bufferOffset, count);
        return count;
    }

    private static Task<T> FromResult<T>(Func<T> read, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromCanceled<T>(cancellationToken);
        }

        try
        {
            return Task.FromResult(read());
        }
        catch (Exception ex)
        {
            return Task.FromException<T>(ex);
        }
    }
}
