using System.Data;
using GtrackWeb.Data;
using GtrackWeb.Helpers;
using GtrackWeb.Models;

namespace GtrackWeb.Services;

/// <summary>
/// Port of <c>form.frmNewIteam</c>. Items live in <c>New_Item_Name</c>
/// (soft-deleted via <c>sign</c>), each classified under an item type (group).
/// An item referenced by <c>Challan_Sub.itemID</c> cannot be edited or deleted.
/// </summary>
public sealed class ItemService
{
    private readonly ISqlDataAccess _db;

    public ItemService(ISqlDataAccess db) => _db = db;

    public List<ItemRow> List()
    {
        var dt = _db.Query(
            "SELECT itemID, itemName, itemType FROM New_Item_Name WHERE sign = 1 ORDER BY itemName");
        return dt.AsEnumerable().Select(r => new ItemRow(
            Convert.ToInt32(r["itemID"]),
            r["itemName"].ToString() ?? "",
            r["itemType"].ToString() ?? "")).ToList();
    }

    public OpResult Create(string itemName, string itemType)
    {
        itemName = (itemName ?? "").Trim();
        itemType = (itemType ?? "").Trim();
        if (string.IsNullOrEmpty(itemName)) return new OpResult(false, "Type Item Name");
        if (string.IsNullOrEmpty(itemType)) return new OpResult(false, "Select Item Group");

        if (_db.Scalar("SELECT 1 FROM New_Item_Name WHERE itemName = @n AND sign = 1",
                Params.New("n", itemName)) != null)
            return new OpResult(false, "Duplicate Item");

        var maxId = _db.Scalar("SELECT MAX(itemID) FROM New_Item_Name");
        var nextId = (maxId == null ? 0 : Convert.ToInt32(maxId)) + 1;

        _db.Execute("INSERT INTO New_Item_Name (itemID, itemName, itemType, sign) VALUES (@id, @n, @t, 1)",
            Params.New("id", nextId).Add("n", itemName).Add("t", itemType));

        return new OpResult(true, "Data Saved Successfully");
    }

    public OpResult Update(int itemId, string itemName, string itemType)
    {
        itemName = (itemName ?? "").Trim();
        itemType = (itemType ?? "").Trim();
        if (itemId <= 0) return new OpResult(false, "Select an item");
        if (string.IsNullOrEmpty(itemName)) return new OpResult(false, "Type Item Name");

        if (InUse(itemId))
            return new OpResult(false, "Item Already Exist In Challan. Data can not Update");

        _db.Execute("UPDATE New_Item_Name SET itemName = @n, itemType = @t WHERE itemID = @id",
            Params.New("n", itemName).Add("t", itemType).Add("id", itemId));

        return new OpResult(true, "Data Update Successfully");
    }

    public OpResult Delete(int itemId)
    {
        if (itemId <= 0) return new OpResult(false, "Select an item");
        if (InUse(itemId))
            return new OpResult(false, "Item Already Exist In Challan. Data can not delete");

        _db.Execute("UPDATE New_Item_Name SET sign = 0 WHERE itemID = @id", Params.New("id", itemId));
        return new OpResult(true, "Data Delete Successfully");
    }

    private bool InUse(int itemId) =>
        _db.Scalar("SELECT 1 FROM Challan_Sub WHERE itemID = @id", Params.New("id", itemId)) != null;
}
