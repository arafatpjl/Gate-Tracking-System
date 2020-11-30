using System.Data;
using Microsoft.Data.SqlClient;

namespace GtrackWeb.Data;

/// <summary>
/// ADO.NET implementation of <see cref="ISqlDataAccess"/> over MSSQL.
/// Opens a fresh connection per operation (connection pooling handles reuse),
/// unlike the desktop app which shared one static open connection.
/// </summary>
public sealed class SqlDataAccess : ISqlDataAccess
{
    private readonly string _connectionString;

    public SqlDataAccess(string connectionString) => _connectionString = connectionString;

    public DataTable Query(string sql, IDictionary<string, object?>? parameters = null)
    {
        using var conn = new SqlConnection(_connectionString);
        using var cmd = CreateCommand(conn, null, sql, parameters);
        conn.Open();
        var dt = new DataTable();
        using var reader = cmd.ExecuteReader();
        dt.Load(reader);
        return dt;
    }

    public object? Scalar(string sql, IDictionary<string, object?>? parameters = null)
    {
        using var conn = new SqlConnection(_connectionString);
        using var cmd = CreateCommand(conn, null, sql, parameters);
        conn.Open();
        var result = cmd.ExecuteScalar();
        return result == DBNull.Value ? null : result;
    }

    public int Execute(string sql, IDictionary<string, object?>? parameters = null)
    {
        using var conn = new SqlConnection(_connectionString);
        using var cmd = CreateCommand(conn, null, sql, parameters);
        conn.Open();
        return cmd.ExecuteNonQuery();
    }

    public T Transaction<T>(Func<ISqlTransaction, T> work)
    {
        using var conn = new SqlConnection(_connectionString);
        conn.Open();
        using var tx = conn.BeginTransaction();
        try
        {
            var result = work(new TxScope(conn, tx));
            tx.Commit();
            return result;
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    public void Transaction(Action<ISqlTransaction> work) =>
        Transaction<object?>(scope => { work(scope); return null; });

    internal static SqlCommand CreateCommand(
        SqlConnection conn, SqlTransaction? tx, string sql, IDictionary<string, object?>? parameters)
    {
        var cmd = new SqlCommand(sql, conn) { CommandTimeout = 120 };
        if (tx != null) cmd.Transaction = tx;
        if (parameters != null)
        {
            foreach (var kvp in parameters)
            {
                var name = kvp.Key.StartsWith('@') ? kvp.Key : "@" + kvp.Key;
                cmd.Parameters.AddWithValue(name, kvp.Value ?? DBNull.Value);
            }
        }
        return cmd;
    }

    private sealed class TxScope : ISqlTransaction
    {
        private readonly SqlConnection _conn;
        private readonly SqlTransaction _tx;

        public TxScope(SqlConnection conn, SqlTransaction tx)
        {
            _conn = conn;
            _tx = tx;
        }

        public DataTable Query(string sql, IDictionary<string, object?>? parameters = null)
        {
            using var cmd = CreateCommand(_conn, _tx, sql, parameters);
            var dt = new DataTable();
            using var reader = cmd.ExecuteReader();
            dt.Load(reader);
            return dt;
        }

        public object? Scalar(string sql, IDictionary<string, object?>? parameters = null)
        {
            using var cmd = CreateCommand(_conn, _tx, sql, parameters);
            var result = cmd.ExecuteScalar();
            return result == DBNull.Value ? null : result;
        }

        public int Execute(string sql, IDictionary<string, object?>? parameters = null)
        {
            using var cmd = CreateCommand(_conn, _tx, sql, parameters);
            return cmd.ExecuteNonQuery();
        }
    }
}
