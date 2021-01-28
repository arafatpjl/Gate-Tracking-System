using System.Data;
using GtrackWeb.Data;
using GtrackWeb.Helpers;
using GtrackWeb.Models;

namespace GtrackWeb.Services;

/// <summary>
/// Port of <c>form.frmNewpfnoauto</c>. PF (file) numbers live in <c>New_PFNo</c>
/// (soft-deleted via <c>SIGN</c>) and are auto-numbered <c>PF#{n}</c> where n is
/// the next id from <c>New_PFNO_Auto</c>. Each PF is linked to a buyer (so the
/// challan line grid can resolve BuyerID from the PF number) and a merchandiser.
/// Save writes both the auto table and the PF table in one transaction.
/// </summary>
public sealed class PfNoService
{
    private readonly ISqlDataAccess _db;
    private readonly LookupService _lookups;
    private readonly CurrentUser _user;

    public PfNoService(ISqlDataAccess db, LookupService lookups, CurrentUser user)
    {
        _db = db;
        _lookups = lookups;
        _user = user;
    }

    public List<PfNoRow> List()
    {
        var dt = _db.Query(
            @"SELECT PFNo, ISNULL(Cr_Date,'') AS Cr_Date, ISNULL(CONVERT(varchar,Qty),'') AS Qty,
                     ISNULL(BuyerName,'') AS BuyerName, ISNULL(MainBuyerName,'') AS MainBuyerName,
                     ISNULL(Ref,'') AS Ref, ISNULL(Merchandiser,'') AS Merchandiser,
                     ISNULL(Description,'') AS Description
              FROM New_PFNo WHERE SIGN = 1
              ORDER BY CAST(SUBSTRING(PFNo, 4, 8) AS INT) DESC");
        return dt.AsEnumerable().Select(r => new PfNoRow(
            r["PFNo"].ToString() ?? "", r["Cr_Date"].ToString() ?? "", r["Qty"].ToString() ?? "",
            r["BuyerName"].ToString() ?? "", r["MainBuyerName"].ToString() ?? "", r["Ref"].ToString() ?? "",
            r["Merchandiser"].ToString() ?? "", r["Description"].ToString() ?? "")).ToList();
    }

    public OpResult Create(string date, string qty, string buyerName, string reference,
                           string merchandiser, string description)
    {
        buyerName = (buyerName ?? "").Trim();
        if (string.IsNullOrEmpty(date)) return new OpResult(false, "Type Date");
        if (string.IsNullOrEmpty(qty)) return new OpResult(false, "Type PF Quantity");
        if (string.IsNullOrEmpty(buyerName)) return new OpResult(false, "Select Buyer");
        if (string.IsNullOrEmpty(merchandiser)) return new OpResult(false, "Select Merchandiser");

        var buyerId = _lookups.BuyerId(buyerName);
        var mainBuyer = _lookups.MainBuyerOf(buyerName);

        var gpNo = _db.Transaction(tx =>
        {
            var maxId = tx.Scalar("SELECT MAX(PFID) FROM New_PFNO_Auto");
            var nextId = (maxId == null ? 0 : Convert.ToInt32(maxId)) + 1;
            var pfNo = "PF#" + nextId;

            tx.Execute(
                "INSERT INTO New_PFNO_Auto VALUES (@id, @pf, @date, @time, @pc)",
                Params.New("id", nextId).Add("pf", pfNo)
                      .Add("date", DateTime.Now.ToString("dd-MMM-yyyy"))
                      .Add("time", DateTime.Now.ToString("hh:mm:ss")).Add("pc", _user.PcName));

            tx.Execute(
                @"INSERT INTO New_PFNo
                    (PFNo, Cr_Date, Qty, BuyerName, MainBuyerName, Ref, BuyerID, Merchandiser,
                     Description, UserID, PCName, EntryDate, EntryTime, SIGN)
                  VALUES (@pf, @crdate, @qty, @buyer, @main, @ref, @buyerid, @merch,
                          @desc, @uid, @pc, @edate, @etime, 1)",
                Params.New("pf", pfNo).Add("crdate", date).Add("qty", qty).Add("buyer", buyerName)
                      .Add("main", mainBuyer).Add("ref", reference ?? "").Add("buyerid", buyerId)
                      .Add("merch", merchandiser).Add("desc", description ?? "")
                      .Add("uid", _user.UserId).Add("pc", _user.PcName)
                      .Add("edate", DateTime.Now.ToString("dd-MMM-yyyy"))
                      .Add("etime", DateTime.Now.ToString("hh:mm:ss")));

            return pfNo;
        });

        return new OpResult(true, $"Data Saved Successfully ({gpNo})");
    }

    public OpResult Update(string pfNo, string qty, string buyerName, string reference,
                           string merchandiser, string description)
    {
        pfNo = (pfNo ?? "").Trim();
        buyerName = (buyerName ?? "").Trim();
        if (string.IsNullOrEmpty(pfNo)) return new OpResult(false, "Select a PF No");
        if (string.IsNullOrEmpty(buyerName)) return new OpResult(false, "Select Buyer");

        var buyerId = _lookups.BuyerId(buyerName);
        var mainBuyer = _lookups.MainBuyerOf(buyerName);

        _db.Execute(
            @"UPDATE New_PFNo SET Ref = @ref, BuyerName = @buyer, MainBuyerName = @main, BuyerID = @buyerid,
                     Qty = @qty, Merchandiser = @merch, Description = @desc, UserID = @uid, PCName = @pc,
                     EntryDate = @edate, EntryTime = @etime
              WHERE PFNo = @pf",
            Params.New("ref", reference ?? "").Add("buyer", buyerName).Add("main", mainBuyer)
                  .Add("buyerid", buyerId).Add("qty", qty).Add("merch", merchandiser)
                  .Add("desc", description ?? "").Add("uid", _user.UserId).Add("pc", _user.PcName)
                  .Add("edate", DateTime.Now.ToString("dd-MMM-yyyy"))
                  .Add("etime", DateTime.Now.ToString("hh:mm:ss")).Add("pf", pfNo));

        return new OpResult(true, "Data Update Successfully");
    }
}
