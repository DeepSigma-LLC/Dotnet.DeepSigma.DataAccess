using System.Data;
using System.Reflection;

namespace DeepSigma.DataAccess.SqlServer;

/// <summary>
/// Adapts an <see cref="IEnumerable{T}"/> to <see cref="IDataReader"/> so that <see cref="Microsoft.Data.SqlClient.SqlBulkCopy"/>
/// can stream rows directly from POCOs without materializing them into a <see cref="DataTable"/> first.
/// </summary>
/// <remarks>
/// Only implements the subset of <see cref="IDataReader"/> that <c>SqlBulkCopy</c> actually calls:
/// <see cref="FieldCount"/>, <see cref="GetName"/>, <see cref="GetFieldType"/>, <see cref="GetValue"/>,
/// <see cref="IsDBNull"/>, and <see cref="Read"/>. The typed <c>GetXxx</c> overloads are forwarded to
/// <see cref="GetValue"/> with a cast, and the stream-style methods (<see cref="GetBytes"/>, <see cref="GetChars"/>,
/// <see cref="GetData"/>) throw <see cref="NotSupportedException"/>.
/// </remarks>
internal sealed class ObjectDataReader<T> : IDataReader
{
    private readonly IEnumerator<T> _enumerator;
    private readonly PropertyInfo[] _properties;
    private bool _closed;

    public ObjectDataReader(IEnumerable<T> source, PropertyInfo[] properties)
    {
        _enumerator = source.GetEnumerator();
        _properties = properties;
    }

    public int FieldCount => _properties.Length;
    public string GetName(int i) => _properties[i].Name;
    public Type GetFieldType(int i) => Nullable.GetUnderlyingType(_properties[i].PropertyType) ?? _properties[i].PropertyType;

    public object GetValue(int i)
    {
        object? value = _properties[i].GetValue(_enumerator.Current);
        return value ?? DBNull.Value;
    }

    public bool IsDBNull(int i) => _properties[i].GetValue(_enumerator.Current) is null;
    public bool Read() => _enumerator.MoveNext();

    public void Close()
    {
        if (_closed) return;
        _enumerator.Dispose();
        _closed = true;
    }

    public bool IsClosed => _closed;
    public void Dispose() => Close();

    public int Depth => 0;
    public int RecordsAffected => -1;
    public bool NextResult() => false;
    public DataTable? GetSchemaTable() => null;

    public object this[int i] => GetValue(i);
    public object this[string name] => GetValue(GetOrdinal(name));

    public int GetOrdinal(string name)
    {
        for (int i = 0; i < _properties.Length; i++)
        {
            if (_properties[i].Name == name) return i;
        }
        throw new IndexOutOfRangeException(name);
    }

    public string GetDataTypeName(int i) => GetFieldType(i).Name;
    public bool GetBoolean(int i) => (bool)GetValue(i);
    public byte GetByte(int i) => (byte)GetValue(i);
    public char GetChar(int i) => (char)GetValue(i);
    public DateTime GetDateTime(int i) => (DateTime)GetValue(i);
    public decimal GetDecimal(int i) => (decimal)GetValue(i);
    public double GetDouble(int i) => (double)GetValue(i);
    public float GetFloat(int i) => (float)GetValue(i);
    public Guid GetGuid(int i) => (Guid)GetValue(i);
    public short GetInt16(int i) => (short)GetValue(i);
    public int GetInt32(int i) => (int)GetValue(i);
    public long GetInt64(int i) => (long)GetValue(i);
    public string GetString(int i) => (string)GetValue(i);

    public long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferoffset, int length) => throw new NotSupportedException();
    public long GetChars(int i, long fieldoffset, char[]? buffer, int bufferoffset, int length) => throw new NotSupportedException();
    public IDataReader GetData(int i) => throw new NotSupportedException();

    public int GetValues(object[] values)
    {
        int count = Math.Min(values.Length, _properties.Length);
        for (int i = 0; i < count; i++)
        {
            values[i] = GetValue(i);
        }
        return count;
    }
}
