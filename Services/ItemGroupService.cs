using System.Data;
using GtrackWeb.Data;
using GtrackWeb.Helpers;
using GtrackWeb.Models;

namespace GtrackWeb.Services;

/// <summary>
/// Port of <c>form.frmIteamGroup</c>. Item groups live in <c>new_Item_Type</c>
/// (soft-deleted via <c>sign</c>). A group referenced by <c>Challan_Sub.Ittype</c>
/// cannot be edited or deleted. The desktop derived <c>itemShortName</c> from a
/// 3-char substring of the name and stored the name in both itemType and itemGroup.
/// </summary>
public sealed class ItemGroupService
{
    private readonly ISqlDataAccess _db;

    public ItemGroupService(ISqlDataAccess db) => _db = db;

    public List<ItemGroupRow> List()
    {
        var dt = _db.Query(
            "SELECT itmid, itemType FROM new_Item_Type WHERE sign = 1 ORDER BY itemType");
        return dt.AsEnumerable()
            .Select(r => new ItemGroupRow(Convert.ToInt32(r["itmid"]), r["itemType"].ToString() ?? ""))
            .ToList();
    }

    public OpResult Create(string itemType)
    {
        itemType = (itemType ?? "").Trim();
        if (string.IsNullOrEmpty(itemType)) return new OpResult(false, "Type Item Group");

        if (_db.Scalar("SELECT 1 FROM new_Item_Type WHERE itemType = @t AND sign = 1",
                Params.New("t", itemType)) != null)
            return new OpResult(false, "Duplicate Item Group");

        var shortName = itemType.Length >= 4 ? itemType.Substring(1, 3) : itemType;

        _db.Execute(
            @"INSERT INTO new_Item_Type (itemType, itemGroup, itemShortName, sign)
              VALUES (@t, @g, @s, 1)",
            Params.New("t", itemType).Add("g", itemType).Add("s", shortName));

        return new OpResult(true, "Data Saved Successfully");
    }

    public OpResult Update(int id, string itemType)
    {
        itemType = (itemType ?? "").Trim();
        if (id <= 0) return new OpResult(false, "Select an item group");
        if (string.IsNullOrEmpty(itemType)) return new OpResult(false, "Type Item Group");

        if (InUse(id))
            return new OpResult(false, "Item Group Already Exist In Challan. Data can not Update");

        _db.Execute("UPDATE new_Item_Type SET itemType = @t, itemGroup = @t WHERE itmid = @id",
            Params.New("t", itemType).Add("id", id));

        return new OpResult(true, "Data Update Successfully");
    }

    public OpResult Delete(int id)
    {
        if (id <= 0) return new OpResult(false, "Select an item group");
        if (InUse(id))
            return new OpResult(false, "Item Group Already Exist In Challan. Data can not delete");

        _db.Execute("UPDATE new_Item_Type SET sign = 0 WHERE itmid = @id", Params.New("id", id));
        return new OpResult(true, "Data Delete Successfully");
    }

    private bool InUse(int id)
    {
        var name = _db.Scalar("SELECT itemType FROM new_Item_Type WHERE itmid = @id", Params.New("id", id));
        if (name == null) return false;
        return _db.Scalar("SELECT 1 FROM Challan_Sub WHERE Ittype = @n", Params.New("n", name)) != null;
    }
}
