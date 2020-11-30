using System.Data;
using System.Text;

namespace GtrackWeb.Helpers;

/// <summary>Converts a <see cref="DataTable"/> to CSV for report downloads.</summary>
public static class CsvExport
{
    public static byte[] ToCsv(DataTable table)
    {
        var sb = new StringBuilder();

        sb.AppendLine(string.Join(",", table.Columns.Cast<DataColumn>().Select(c => Escape(c.ColumnName))));

        foreach (DataRow row in table.Rows)
            sb.AppendLine(string.Join(",", row.ItemArray.Select(v => Escape(v?.ToString() ?? ""))));

        // UTF-8 BOM so Excel opens it with the right encoding.
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }

    private static string Escape(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }
}
