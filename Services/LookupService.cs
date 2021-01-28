using System.Data;
using GtrackWeb.Data;
using GtrackWeb.Helpers;
using GtrackWeb.Models;

namespace GtrackWeb.Services;

/// <summary>
/// Reference-data lookups. Web port of the desktop <c>form.Find</c> helper plus
/// the various combo-box population routines scattered across the forms.
/// </summary>
public sealed class LookupService
{
    private readonly ISqlDataAccess _db;
    private readonly CurrentUser _user;

    public LookupService(ISqlDataAccess db, CurrentUser user)
    {
        _db = db;
        _user = user;
    }

    private static List<LookupItem> Map(DataTable dt, string valueCol, string textCol) =>
        dt.AsEnumerable()
          .Select(r => new LookupItem(r[valueCol].ToString() ?? "", r[textCol].ToString() ?? ""))
          .ToList();

    // ---- Buyers (New_Buyer) -------------------------------------------------

    public List<LookupItem> MainBuyers()
    {
        var dt = _db.Query(
            "SELECT DISTINCT MainBuyerName FROM New_Buyer WHERE Sign = 1 ORDER BY MainBuyerName");
        return Map(dt, "MainBuyerName", "MainBuyerName");
    }

    public List<LookupItem> BuyersByMain(string mainBuyerName)
    {
        var dt = _db.Query(
            "SELECT BuyerName FROM New_Buyer WHERE MainBuyerName = @m AND Sign = 1 ORDER BY BuyerName",
            Params.New("m", mainBuyerName));
        return Map(dt, "BuyerName", "BuyerName");
    }

    public int BuyerId(string buyerName)
    {
        var v = _db.Scalar(
            "SELECT BuyerID FROM New_Buyer WHERE BuyerName = @n AND Sign = 1",
            Params.New("n", buyerName));
        return v == null ? 0 : Convert.ToInt32(v);
    }

    public string MainBuyerOf(string buyerName)
    {
        var v = _db.Scalar(
            "SELECT MainBuyerName FROM New_Buyer WHERE BuyerName = @n AND Sign = 1",
            Params.New("n", buyerName));
        return v?.ToString() ?? string.Empty;
    }

    public List<LookupItem> AllBuyers()
    {
        var dt = _db.Query(
            "SELECT DISTINCT BuyerName FROM New_Buyer WHERE Sign = 1 ORDER BY BuyerName");
        return Map(dt, "BuyerName", "BuyerName");
    }

    public List<LookupItem> Merchandisers()
    {
        var dt = _db.Query(
            "SELECT Merchandiser FROM New_Merchandiser WHERE SIGN = 0 ORDER BY Merchandiser");
        return Map(dt, "Merchandiser", "Merchandiser");
    }

    // ---- Companies ----------------------------------------------------------

    public List<LookupItem> OwnCompanies()
    {
        var dt = _db.Query("SELECT CompID, CompName FROM Company_Information ORDER BY CompName");
        return Map(dt, "CompID", "CompName");
    }

    public List<LookupItem> OutCompanies()
    {
        var dt = _db.Query(
            "SELECT CompID, CompName FROM Out_Company_Information WHERE DeleteRow = 0 ORDER BY CompName");
        return Map(dt, "CompID", "CompName");
    }

    // ---- Challan reference data --------------------------------------------

    public List<LookupItem> ItemTypes()
    {
        var dt = _db.Query("SELECT itmid, itemType FROM new_Item_Type WHERE sign = 1 ORDER BY itemType");
        return Map(dt, "itemType", "itemType");
    }

    public List<LookupItem> Items()
    {
        var dt = _db.Query("SELECT itemID, itemName FROM New_Item_Name WHERE sign = 1 ORDER BY itemName");
        return Map(dt, "itemID", "itemName");
    }

    public List<LookupItem> PfNos()
    {
        var dt = _db.Query("SELECT PFID, PFNo FROM New_PFNo ORDER BY PFNo");
        return Map(dt, "PFID", "PFNo");
    }

    public List<LookupItem> Purposes()
    {
        var dt = _db.Query(
            "SELECT pid, purpose FROM new_purpose WHERE DeleteRow = 0 ORDER BY purpose");
        return Map(dt, "pid", "purpose");
    }

    public List<LookupItem> Drivers()
    {
        var dt = _db.Query("SELECT DID, Dname FROM InfoDriver WHERE sign = 1 ORDER BY Dname");
        return Map(dt, "DID", "Dname");
    }

    public List<LookupItem> Users()
    {
        var dt = _db.Query("SELECT UserID, UserName FROM Sys_User_Name_UP ORDER BY UserName");
        return Map(dt, "UserID", "UserName");
    }

    public List<LookupItem> Departments()
    {
        var dt = _db.Query(
            "SELECT DISTINCT DeptName FROM Dept_Name WHERE CompID = @cid ORDER BY DeptName",
            Params.New("cid", _user.CompId));
        return Map(dt, "DeptName", "DeptName");
    }

    public List<LookupItem> Employees()
    {
        // InfoEmp: active employees are mleft = 0; the display name column is EMPName.
        var dt = _db.Query(
            "SELECT EMPID, EMPName FROM InfoEmp WHERE mleft = 0 AND ComPID = @cid ORDER BY EMPName",
            Params.New("cid", _user.CompId));
        return Map(dt, "EMPID", "EMPName");
    }
}
