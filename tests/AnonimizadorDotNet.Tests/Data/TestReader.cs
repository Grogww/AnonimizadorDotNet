using System.Collections;
using System.Data;
using System.Data.Common;

namespace AnonimizadorDotNet.Tests.Data;

/// <summary>Como o <see cref="TestReader"/> responde a <see cref="DbDataReader.GetSchemaTable"/>.</summary>
public enum SchemaMode
{
    /// <summary>O esquema do <see cref="DataTableReader"/>, com origem (tabela e coluna).</summary>
    Native,

    /// <summary>Nenhum esquema (<see langword="null"/>).</summary>
    Missing,

    /// <summary>O provedor não suporta esquema (<see cref="NotSupportedException"/>).</summary>
    Unsupported,
}

/// <summary>Um provedor em memória: um <see cref="DataTableReader"/> com o esquema controlável.</summary>
/// <remarks>Conta as leituras de valor, para conferir que as colunas passadas vão direto ao original.</remarks>
internal sealed class TestReader(SchemaMode schema, params DataTable[] tables) : DbDataReader
{
    private readonly DataTableReader _inner = new(tables);

    public int ValueReads { get; private set; }

    public bool Disposed { get; private set; }

    public override int Depth => _inner.Depth;

    public override int FieldCount => _inner.FieldCount;

    public override bool HasRows => _inner.HasRows;

    public override bool IsClosed => _inner.IsClosed;

    public override int RecordsAffected => _inner.RecordsAffected;

    public override object this[int ordinal] => GetValue(ordinal);

    public override object this[string name] => GetValue(GetOrdinal(name));

    public override DataTable? GetSchemaTable() => schema switch
    {
        SchemaMode.Native => _inner.GetSchemaTable(),
        SchemaMode.Missing => null,
        _ => throw new NotSupportedException(),
    };

    public override bool GetBoolean(int ordinal) => Count(_inner.GetBoolean(ordinal));

    public override byte GetByte(int ordinal) => Count(_inner.GetByte(ordinal));

    public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) =>
        Count(_inner.GetBytes(ordinal, dataOffset, buffer, bufferOffset, length));

    public override char GetChar(int ordinal) => Count(_inner.GetChar(ordinal));

    public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) =>
        Count(_inner.GetChars(ordinal, dataOffset, buffer, bufferOffset, length));

    public override string GetDataTypeName(int ordinal) => _inner.GetDataTypeName(ordinal);

    public override DateTime GetDateTime(int ordinal) => Count(_inner.GetDateTime(ordinal));

    public override decimal GetDecimal(int ordinal) => Count(_inner.GetDecimal(ordinal));

    public override double GetDouble(int ordinal) => Count(_inner.GetDouble(ordinal));

    public override IEnumerator GetEnumerator() => new DbEnumerator(this, closeReader: false);

    public override Type GetFieldType(int ordinal) => _inner.GetFieldType(ordinal);

    public override float GetFloat(int ordinal) => Count(_inner.GetFloat(ordinal));

    public override Guid GetGuid(int ordinal) => Count(_inner.GetGuid(ordinal));

    public override short GetInt16(int ordinal) => Count(_inner.GetInt16(ordinal));

    public override int GetInt32(int ordinal) => Count(_inner.GetInt32(ordinal));

    public override long GetInt64(int ordinal) => Count(_inner.GetInt64(ordinal));

    public override string GetName(int ordinal) => _inner.GetName(ordinal);

    public override int GetOrdinal(string name) => _inner.GetOrdinal(name);

    public override string GetString(int ordinal) => Count(_inner.GetString(ordinal));

    public override object GetValue(int ordinal) => Count(_inner.GetValue(ordinal));

    public override int GetValues(object[] values)
    {
        ValueReads++;
        return _inner.GetValues(values);
    }

    public override bool IsDBNull(int ordinal) => _inner.IsDBNull(ordinal);

    public override bool NextResult() => _inner.NextResult();

    public override bool Read() => _inner.Read();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Disposed = true;
            _inner.Dispose();
        }

        base.Dispose(disposing);
    }

    private T Count<T>(T value)
    {
        ValueReads++;
        return value;
    }
}
