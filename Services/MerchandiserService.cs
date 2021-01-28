using System.Data;
using GtrackWeb.Data;
using GtrackWeb.Helpers;
using GtrackWeb.Models;

namespace GtrackWeb.Services;

/// <summary>
/// Port of <c>form.frmMerchandiser</c>. Merchandisers live in
/// <c>New_Merchandiser</c>; the name is the key and — unlike most tables here —
/// <b>active rows use <c>SIGN = 0</c></b> (delete sets SIGN = 1).
/// </summary>
public sealed class MerchandiserService
{
    private readonly ISqlDataAccess _db;

    public MerchandiserService(ISqlDataAccess db) => _db = db;

    public List<MerchandiserRow> List()
    {
        var dt = _db.Query(
            "SELECT Merchandiser FROM New_Merchandiser WHERE SIGN = 0 ORDER BY Merchandiser");
        return dt.AsEnumerable().Select(r => new MerchandiserRow(r["Merchandiser"].ToString() ?? "")).ToList();
    }

    public OpResult Create(string name)
    {
        name = (name ?? "").Trim();
        if (string.IsNullOrEmpty(name)) return new OpResult(false, "Type Merchandiser Name");

        if (_db.Scalar("SELECT 1 FROM New_Merchandiser WHERE Merchandiser = @n AND SIGN = 0",
                Params.New("n", name)) != null)
            return new OpResult(false, "Duplicate Type Data");

        _db.Execute("INSERT INTO New_Merchandiser (Merchandiser, SIGN) VALUES (@n, 0)",
            Params.New("n", name));

        return new OpResult(true, "Data Saved Successfully");
    }

    public OpResult Update(string oldName, string newName)
    {
        oldName = (oldName ?? "").Trim();
        newName = (newName ?? "").Trim();
        if (string.IsNullOrEmpty(oldName)) return new OpResult(false, "Select a merchandiser");
        if (string.IsNullOrEmpty(newName)) return new OpResult(false, "Type Merchandiser Name");

        _db.Execute("UPDATE New_Merchandiser SET Merchandiser = @new WHERE Merchandiser = @old AND SIGN = 0",
            Params.New("new", newName).Add("old", oldName));

        return new OpResult(true, "Data Update Successfully");
    }

    public OpResult Delete(string name)
    {
        name = (name ?? "").Trim();
        if (string.IsNullOrEmpty(name)) return new OpResult(false, "Select a merchandiser");

        _db.Execute("UPDATE New_Merchandiser SET SIGN = 1 WHERE Merchandiser = @n", Params.New("n", name));
        return new OpResult(true, "Data Delete Successfully");
    }
}
