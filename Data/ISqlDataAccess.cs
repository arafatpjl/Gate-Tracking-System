using System.Data;

namespace GtrackWeb.Data;

/// <summary>
/// Parameterized data-access abstraction. This is the web replacement for the
/// desktop <c>Gtrack.conn.Mssqlconnect</c> class, except every value is passed
/// as a SqlParameter instead of being concatenated into the SQL string
/// (closing the SQL-injection holes that riddled the WinForms version).
/// </summary>
public interface ISqlDataAccess
{
    /// <summary>Runs a SELECT and returns the result as a <see cref="DataTable"/>.</summary>
    DataTable Query(string sql, IDictionary<string, object?>? parameters = null);

    /// <summary>Runs a SELECT and returns the first column of the first row (or null).</summary>
    object? Scalar(string sql, IDictionary<string, object?>? parameters = null);

    /// <summary>Runs an INSERT/UPDATE/DELETE and returns affected rows.</summary>
    int Execute(string sql, IDictionary<string, object?>? parameters = null);

    /// <summary>
    /// Runs a unit of work inside a single transaction. Use for master-detail
    /// saves (e.g. NEW_GP + Challan_Main + Challan_Sub + gate rows) so the whole
    /// challan either commits or rolls back together.
    /// </summary>
    T Transaction<T>(Func<ISqlTransaction, T> work);

    void Transaction(Action<ISqlTransaction> work);
}

/// <summary>Scoped command surface available inside <see cref="ISqlDataAccess.Transaction{T}"/>.</summary>
public interface ISqlTransaction
{
    DataTable Query(string sql, IDictionary<string, object?>? parameters = null);
    object? Scalar(string sql, IDictionary<string, object?>? parameters = null);
    int Execute(string sql, IDictionary<string, object?>? parameters = null);
}
