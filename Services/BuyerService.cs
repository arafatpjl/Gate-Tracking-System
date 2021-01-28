using System.Data;
using GtrackWeb.Data;
using GtrackWeb.Helpers;
using GtrackWeb.Models;

namespace GtrackWeb.Services;

/// <summary>
/// Port of <c>form.frmBuyer</c>. Buyers live in <c>New_Buyer</c> with a
/// <c>Sign</c> soft-delete flag; a buyer already referenced by <c>Challan_Sub</c>
/// cannot be edited or deleted.
/// </summary>
public sealed class BuyerService
{
    private readonly ISqlDataAccess _db;

    public BuyerService(ISqlDataAccess db) => _db = db;

    public sealed record Result(bool Ok, string Message);

    public List<BuyerRow> List()
    {
        var dt = _db.Query(
            @"SELECT BuyerID, BuyerName, MainBuyerName
              FROM New_Buyer WHERE Sign = 1 ORDER BY MainBuyerName, BuyerName");
        return dt.AsEnumerable().Select(r => new BuyerRow(
            Convert.ToInt32(r["BuyerID"]),
            r["BuyerName"].ToString() ?? "",
            r["MainBuyerName"].ToString() ?? "")).ToList();
    }

    /// <summary>Insert a new buyer (frmBuyer save branch).</summary>
    public Result Create(string mainBuyerName, string buyerName)
    {
        mainBuyerName = mainBuyerName.Trim();
        buyerName = buyerName.Trim().ToUpperInvariant();

        if (string.IsNullOrEmpty(mainBuyerName)) return new Result(false, "Type Main Buyer Name");
        if (string.IsNullOrEmpty(buyerName)) return new Result(false, "Type Buyer Name");

        var dup = _db.Scalar(
            "SELECT 1 FROM New_Buyer WHERE BuyerName = @b AND MainBuyerName = @m AND Sign = 1",
            Params.New("b", buyerName).Add("m", mainBuyerName));
        if (dup != null) return new Result(false, "Duplicate Buyer");

        // Desktop pattern: next id = Max(BuyerID) + 1.
        var maxId = _db.Scalar("SELECT MAX(BuyerID) FROM New_Buyer");
        var nextId = (maxId == null ? 0 : Convert.ToInt32(maxId)) + 1;

        _db.Execute(
            @"INSERT INTO New_Buyer (BuyerID, BuyerName, MainBuyerName, Sign)
              VALUES (@id, @b, @m, 1)",
            Params.New("id", nextId).Add("b", buyerName).Add("m", mainBuyerName));

        return new Result(true, "Data Saved Successfully");
    }

    /// <summary>Update an existing buyer (frmBuyer update branch).</summary>
    public Result Update(int buyerId, string mainBuyerName, string buyerName)
    {
        buyerName = buyerName.Trim().ToUpperInvariant();
        mainBuyerName = mainBuyerName.Trim();

        if (buyerId <= 0) return new Result(false, "Select a buyer");
        if (string.IsNullOrEmpty(buyerName)) return new Result(false, "Type Buyer Name");

        if (InUse(buyerId))
            return new Result(false, "Buyer Exist In Challan. Data can not Update");

        _db.Execute(
            @"UPDATE New_Buyer SET BuyerName = @b, MainBuyerName = @m WHERE BuyerID = @id",
            Params.New("b", buyerName).Add("m", mainBuyerName).Add("id", buyerId));

        return new Result(true, "Data Update Successfully");
    }

    /// <summary>Soft-delete (Sign = 0) unless referenced by a challan.</summary>
    public Result Delete(int buyerId)
    {
        if (buyerId <= 0) return new Result(false, "Select a buyer");

        if (InUse(buyerId))
            return new Result(false, "Buyer Already Exist In Challan. Data can not delete");

        _db.Execute("UPDATE New_Buyer SET Sign = 0 WHERE BuyerID = @id", Params.New("id", buyerId));
        return new Result(true, "Data Delete Successfully");
    }

    private bool InUse(int buyerId) =>
        _db.Scalar("SELECT 1 FROM Challan_Sub WHERE BuyerID = @id", Params.New("id", buyerId)) != null;
}
