using System.Data;
using GtrackWeb.Data;
using GtrackWeb.Helpers;
using GtrackWeb.Models;

namespace GtrackWeb.Services;

/// <summary>
/// Return-challan flow (frmReturnChallanSender / gates / receiver). Returnable
/// challans (Challan_Main.Returnable = 'YES') can be sent back in one or more
/// installments. Each installment is a <c>rowslno</c> across the parallel tables
/// Return_Challan_Main / Return_Challan_Sub (+ audit) and Return_Sender_Gate /
/// Return_Receiver_Gate, mirroring the forward Sender → Gate → Gate → Receiver
/// lifecycle.
///
/// Simplifications vs desktop (documented in README): the return header (parties,
/// driver, purpose, etc.) is copied from the original challan rather than
/// re-entered; gate/receive act on the latest <c>rowslno</c> for the challan.
/// </summary>
public sealed class ReturnChallanService
{
    private readonly ISqlDataAccess _db;
    private readonly ChallanService _forward;
    private readonly CurrentUser _user;

    public ReturnChallanService(ISqlDataAccess db, ChallanService forward, CurrentUser user)
    {
        _db = db;
        _forward = forward;
        _user = user;
    }

    /// <summary>Find a returnable challan by GP No (must be Returnable = 'YES').</summary>
    public ChallanHeaderInfo? FindReturnable(string gpNo)
    {
        var info = _forward.FindByGpNo(gpNo);
        if (info == null) return null;

        var returnable = _db.Scalar(
            "SELECT Returnable FROM Challan_Main WHERE GPID = @g AND DeleteRow = 0",
            Params.New("g", info.GpId))?.ToString();

        return string.Equals(returnable, "YES", StringComparison.OrdinalIgnoreCase) ? info : null;
    }

    /// <summary>Original lines of a challan, for choosing return quantities.</summary>
    public List<ChallanLineView> OriginalLines(int gpId) => _forward.Lines(gpId);

    /// <summary>Create a return installment (frmReturnChallanSender).</summary>
    public OpResult CreateReturn(int gpId, string gpDate, string returnDate, List<ReceiveLineInput> lines)
    {
        var returned = lines.Where(l => l.RecQty > 0).ToList();
        if (returned.Count == 0) return new OpResult(false, "Enter at least one return quantity");

        // Header carried over from the original challan.
        var head = _db.Query(
            @"SELECT CompID, EMPID, MSID, SenderId, ReceiverId, CReceiverId, Driverid, authid, pid,
                     ISNULL(Vehicleno,'') AS Vehicleno, CType
              FROM Challan_Main WHERE GPID = @g AND DeleteRow = 0",
            Params.New("g", gpId));
        if (head.Rows.Count == 0) return new OpResult(false, "Original challan not found");
        var h = head.Rows[0];

        // Original line detail (item group / pf) to copy onto the return lines.
        var orig = _forward.Lines(gpId).ToDictionary(l => l.Sl);

        _db.Transaction(tx =>
        {
            var maxRow = tx.Scalar("SELECT MAX(rowslno) FROM Return_Challan_Main WHERE GPID = @g",
                Params.New("g", gpId));
            var rowSl = (maxRow == null ? 0 : Convert.ToInt32(maxRow)) + 1;

            tx.Execute(
                @"INSERT INTO Return_Challan_Main
                    (rowslno, CompID, GPID, Gpdate, EMPID, MSID, SenderId, ReceiverId, CReceiverId, Driverid,
                     authid, pid, Vehicleno, OutTime, CType, Returnable, ReturnDate, UserID, PCName,
                     EntryDate, EntryTime, DeleteRow)
                  VALUES
                    (@row, @cid, @g, @gpdate, @emp, @msid, @sender, @recv, @carrier, @driver,
                     @auth, @pid, @vehicle, CONVERT(VARCHAR(8), GETDATE(), 108), @ctype, 'YES', @rdate, @uid, @pc,
                     CONVERT(DATETIME, FLOOR(CONVERT(FLOAT, GETDATE()))), CONVERT(VARCHAR(8), GETDATE(), 108), 0)",
                Params.New("row", rowSl).Add("cid", h["CompID"]).Add("g", gpId).Add("gpdate", gpDate)
                      .Add("emp", h["EMPID"]).Add("msid", h["MSID"]).Add("sender", h["SenderId"])
                      .Add("recv", h["ReceiverId"]).Add("carrier", h["CReceiverId"]).Add("driver", h["Driverid"])
                      .Add("auth", h["authid"]).Add("pid", h["pid"]).Add("vehicle", h["Vehicleno"])
                      .Add("ctype", h["CType"]).Add("rdate", returnDate)
                      .Add("uid", _user.UserId).Add("pc", _user.PcName));

            foreach (var line in returned)
            {
                orig.TryGetValue(line.Sl, out var o);
                var itemId = ResolveItemId(tx, o?.ItemName ?? "");
                var pfId = ResolvePfId(tx, o?.PfNo ?? "");
                var buyerId = ResolveBuyerIdByPf(tx, pfId);

                var p = Params.New("row", rowSl).Add("g", gpId).Add("ittype", o?.ItemGroup ?? "")
                    .Add("item", itemId).Add("buyer", buyerId).Add("pf", pfId)
                    .Add("desc", o?.Description ?? "").Add("unit", o?.Unit ?? "").Add("qty", line.RecQty)
                    .Add("remarks", line.Remarks ?? "").Add("sl", line.Sl)
                    .Add("uid", _user.UserId).Add("pc", _user.PcName).Add("gpdate", gpDate);

                tx.Execute(
                    @"INSERT INTO Return_Challan_Sub
                        (rowslno, GPID, ittype, ItemID, BuyerID, PFID, ItDesc, QtyUnit, GPQty, Remarks, SLNO,
                         UserID, PCName, EntryDate, EntryTime, GPDate, DeleteRow)
                      VALUES (@row, @g, @ittype, @item, @buyer, @pf, @desc, @unit, @qty, @remarks, @sl,
                              @uid, @pc, CONVERT(DATETIME, FLOOR(CONVERT(FLOAT, GETDATE()))),
                              CONVERT(VARCHAR(8), GETDATE(), 108), @gpdate, 0)", p);

                tx.Execute(
                    @"INSERT INTO Return_Challan_Sub_Del_Edit
                        (rowslno, GPID, ittype, ItemID, BuyerID, PFID, ItDesc, QtyUnit, GPQty, Remarks,
                         DeleteRow, SLNO, UserID, PCName, EntryDate, EntryTime)
                      VALUES (@row, @g, @ittype, @item, @buyer, @pf, @desc, @unit, @qty, @remarks,
                              0, @sl, @uid, @pc, CONVERT(DATETIME, FLOOR(CONVERT(FLOAT, GETDATE()))),
                              CONVERT(VARCHAR(8), GETDATE(), 108))", p);
            }

            tx.Execute(
                "INSERT INTO Return_Sender_Gate (Rowslno, SGCompID, SGGPID, SGsign) VALUES (@row, @c, @g, 0)",
                Params.New("row", rowSl).Add("c", h["CompID"]).Add("g", gpId));
            tx.Execute(
                "INSERT INTO Return_Receiver_Gate (Rowslno, RGCompID, RGGPID, RGsign) VALUES (@row, @c, @g, 0)",
                Params.New("row", rowSl).Add("c", h["MSID"]).Add("g", gpId));
        });

        return new OpResult(true, "Return Challan Saved Successfully");
    }

    /// <summary>Latest return installment for a challan, with gate/receive status.</summary>
    public ReturnStatusInfo? FindLatestReturn(string gpNo)
    {
        var info = _forward.FindByGpNo(gpNo);
        if (info == null) return null;

        var dt = _db.Query(
            @"SELECT TOP 1 m.rowslno, m.Gpdate,
                     ISNULL(sg.SGsign,0) AS SGsign, ISNULL(rg.RGsign,0) AS RGsign, ISNULL(m.DeleteRowR,0) AS DeleteRowR
              FROM Return_Challan_Main m
              LEFT JOIN Return_Sender_Gate sg ON sg.SGGPID = m.GPID AND sg.Rowslno = m.rowslno
              LEFT JOIN Return_Receiver_Gate rg ON rg.RGGPID = m.GPID AND rg.Rowslno = m.rowslno
              WHERE m.GPID = @g AND m.DeleteRow = 0
              ORDER BY m.rowslno DESC",
            Params.New("g", info.GpId));
        if (dt.Rows.Count == 0) return null;
        var r = dt.Rows[0];

        return new ReturnStatusInfo(
            info.GpId, info.GpNo, Convert.ToInt32(r["rowslno"]),
            r["Gpdate"] == DBNull.Value ? "" : Convert.ToDateTime(r["Gpdate"]).ToString("dd-MMM-yyyy"),
            Convert.ToInt32(r["SGsign"]) == 1, Convert.ToInt32(r["RGsign"]) == 1,
            Convert.ToInt32(r["DeleteRowR"]) == 1);
    }

    public List<ChallanLineView> ReturnLines(int gpId, int rowSl)
    {
        var dt = _db.Query(
            @"SELECT s.SLNO, s.ittype, ISNULL(i.itemName,'') AS itemName, ISNULL(p.PFNo,'') AS PFNo,
                     ISNULL(s.ItDesc,'') AS ItDesc, ISNULL(s.QtyUnit,'') AS QtyUnit,
                     ISNULL(s.GPQty,0) AS GPQty, ISNULL(s.RecQty,0) AS RecQty, ISNULL(s.Remarks,'') AS Remarks
              FROM Return_Challan_Sub s
              LEFT JOIN New_Item_Name i ON i.itemID = s.ItemID
              LEFT JOIN New_PFNo p ON p.PFID = s.PFID
              WHERE s.GPID = @g AND s.rowslno = @row AND s.DeleteRow = 0
              ORDER BY s.SLNO",
            Params.New("g", gpId).Add("row", rowSl));

        return dt.AsEnumerable().Select(r => new ChallanLineView
        {
            Sl = Convert.ToInt32(r["SLNO"]),
            ItemGroup = r["ittype"].ToString() ?? "",
            ItemName = r["itemName"].ToString() ?? "",
            PfNo = r["PFNo"].ToString() ?? "",
            Description = r["ItDesc"].ToString() ?? "",
            Unit = r["QtyUnit"].ToString() ?? "",
            GpQty = Convert.ToDecimal(r["GPQty"]),
            RecQty = Convert.ToDecimal(r["RecQty"]),
            Remarks = r["Remarks"].ToString() ?? "",
        }).ToList();
    }

    public OpResult SenderGate(string gpNo, string date, string time, string remark)
    {
        var st = FindLatestReturn(gpNo);
        if (st == null) return new OpResult(false, "Please Type Correct Challan-No");
        if (st.SenderGateOk) return new OpResult(false, "Challan Already Received BY Sender Gate");
        if (string.IsNullOrWhiteSpace(date)) return new OpResult(false, "Type Sender Gate Date");
        if (string.IsNullOrWhiteSpace(time)) return new OpResult(false, "Type Sender Gate Time");

        _db.Execute(
            @"UPDATE Return_Sender_Gate SET SGateDate=@d, SGateTime=@t, SGateremark=@r, UserID=@uid, PCName=@pc,
                     EntryDate=@d, EntryTime=CONVERT(VARCHAR(8), GETDATE(), 108), SGsign=1
              WHERE SGGPID=@g AND Rowslno=@row AND SGsign=0",
            Params.New("d", date).Add("t", time).Add("r", remark ?? "").Add("uid", _user.UserId)
                  .Add("pc", _user.PcName).Add("g", st.GpId).Add("row", st.RowSl));

        return new OpResult(true, "Data Successfully Save");
    }

    public OpResult ReceiverGate(string gpNo, string date, string time, string remark)
    {
        var st = FindLatestReturn(gpNo);
        if (st == null) return new OpResult(false, "Please Type Correct Challan-No");
        if (st.ReceiverGateOk) return new OpResult(false, "Receiver Gate Already Ok");
        if (!st.SenderGateOk) return new OpResult(false, "Sender Gate Not Ok");
        if (string.IsNullOrWhiteSpace(date)) return new OpResult(false, "Type Receiver Gate Date");
        if (string.IsNullOrWhiteSpace(time)) return new OpResult(false, "Type Receiver Gate Time");

        _db.Execute(
            @"UPDATE Return_Receiver_Gate SET RGateDate=@d, RGateTime=@t, RGateremark=@r, UserID=@uid, PCName=@pc,
                     EntryDate=@d, EntryTime=CONVERT(VARCHAR(8), GETDATE(), 108), RGsign=1
              WHERE RGGPID=@g AND Rowslno=@row AND RGsign=0",
            Params.New("d", date).Add("t", time).Add("r", remark ?? "").Add("uid", _user.UserId)
                  .Add("pc", _user.PcName).Add("g", st.GpId).Add("row", st.RowSl));

        return new OpResult(true, "Data Successfully Save");
    }

    public OpResult Receive(int gpId, int rowSl, string date, string time, List<ReceiveLineInput> lines)
    {
        if (string.IsNullOrWhiteSpace(date)) return new OpResult(false, "Type Received Date");
        if (string.IsNullOrWhiteSpace(time)) return new OpResult(false, "Type Received Time");

        var sgPending = _db.Scalar(
            "SELECT 1 FROM Return_Sender_Gate WHERE SGGPID=@g AND Rowslno=@row AND SGsign=0",
            Params.New("g", gpId).Add("row", rowSl));
        if (sgPending != null) return new OpResult(false, "Sender Gate Not Ok");

        var rgPending = _db.Scalar(
            "SELECT 1 FROM Return_Receiver_Gate WHERE RGGPID=@g AND Rowslno=@row AND RGsign=0",
            Params.New("g", gpId).Add("row", rowSl));
        if (rgPending != null) return new OpResult(false, "Receiver Gate Not Ok");

        _db.Transaction(tx =>
        {
            tx.Execute(
                @"UPDATE Return_Challan_Main SET RcvDate=@d, RcvTime=@t, DeleteRowR=1, RUserID=@uid, RPCName=@pc,
                         REntryDate=CONVERT(DATETIME, FLOOR(CONVERT(FLOAT, GETDATE()))),
                         REntryTime=CONVERT(VARCHAR(8), GETDATE(), 108)
                  WHERE DeleteRow=0 AND GPID=@g AND rowslno=@row",
                Params.New("d", date).Add("t", time).Add("uid", _user.UserId).Add("pc", _user.PcName)
                      .Add("g", gpId).Add("row", rowSl));

            foreach (var line in lines)
            {
                tx.Execute(
                    @"UPDATE Return_Challan_Sub SET RecQty=@q, Remarks=@r, DeleteRowR=1, RUserID=@uid, RPCName=@pc,
                             REntryDate=CONVERT(DATETIME, FLOOR(CONVERT(FLOAT, GETDATE()))),
                             REntryTime=CONVERT(VARCHAR(8), GETDATE(), 108)
                      WHERE DeleteRow=0 AND GPID=@g AND rowslno=@row AND SLNO=@sl",
                    Params.New("q", line.RecQty).Add("r", line.Remarks ?? "").Add("uid", _user.UserId)
                          .Add("pc", _user.PcName).Add("g", gpId).Add("row", rowSl).Add("sl", line.Sl));
            }
        });

        return new OpResult(true, "Data Successfully Save");
    }

    // ---- Edit / Delete (per return installment / rowslno) ------------------

    private bool IsReceived(int gpId, int rowSl) =>
        _db.Scalar(@"SELECT 1 FROM Return_Challan_Main
                     WHERE GPID=@g AND rowslno=@row AND DeleteRow=0 AND ISNULL(DeleteRowR,0)=1",
            Params.New("g", gpId).Add("row", rowSl)) != null;

    public OpResult UpdateLines(int gpId, int rowSl, List<ChallanLineView> lines)
    {
        if (IsReceived(gpId, rowSl)) return new OpResult(false, "Return already received; cannot edit");

        _db.Transaction(tx =>
        {
            foreach (var l in lines)
            {
                tx.Execute(
                    @"UPDATE Return_Challan_Sub SET GPQty=@qty, QtyUnit=@unit, ItDesc=@desc, Remarks=@remarks,
                             UserID=@uid, PCName=@pc, EntryDate=@date, EntryTime=@time
                      WHERE GPID=@g AND rowslno=@row AND SLNO=@sl AND DeleteRow=0",
                    Params.New("qty", l.GpQty).Add("unit", l.Unit).Add("desc", l.Description)
                          .Add("remarks", l.Remarks).Add("uid", _user.UserId).Add("pc", _user.PcName)
                          .Add("date", DateTime.Now.ToString("dd-MMM-yyyy")).Add("time", DateTime.Now.ToString("hh:mm:ss"))
                          .Add("g", gpId).Add("row", rowSl).Add("sl", l.Sl));
            }
        });
        return new OpResult(true, "Data Update Successfully");
    }

    public OpResult DeleteLine(int gpId, int rowSl, int sl)
    {
        if (IsReceived(gpId, rowSl)) return new OpResult(false, "Return already received; cannot edit");

        var remaining = _db.Scalar(
            "SELECT COUNT(*) FROM Return_Challan_Sub WHERE GPID=@g AND rowslno=@row AND DeleteRow=0",
            Params.New("g", gpId).Add("row", rowSl));
        if (remaining != null && Convert.ToInt32(remaining) <= 1)
            return new OpResult(false, "Sorry You Can not Delete This Single Item");

        _db.Execute(
            @"UPDATE Return_Challan_Sub SET DeleteRow=1, UserID=@uid, PCName=@pc, EntryDate=@date, EntryTime=@time
              WHERE GPID=@g AND rowslno=@row AND SLNO=@sl AND DeleteRow=0",
            Params.New("uid", _user.UserId).Add("pc", _user.PcName)
                  .Add("date", DateTime.Now.ToString("dd-MMM-yyyy")).Add("time", DateTime.Now.ToString("hh:mm:ss"))
                  .Add("g", gpId).Add("row", rowSl).Add("sl", sl));
        return new OpResult(true, "Data Successfully Deleted");
    }

    /// <summary>Soft-delete a whole return installment (main + subs + both return gates for that rowslno).</summary>
    public OpResult DeleteReturn(int gpId, int rowSl)
    {
        if (IsReceived(gpId, rowSl)) return new OpResult(false, "Return already received; cannot delete");

        var exists = _db.Scalar(
            "SELECT 1 FROM Return_Challan_Main WHERE GPID=@g AND rowslno=@row AND DeleteRow=0",
            Params.New("g", gpId).Add("row", rowSl));
        if (exists == null) return new OpResult(false, "Return installment not found");

        _db.Transaction(tx =>
        {
            var meta = Params.New("uid", _user.UserId).Add("pc", _user.PcName)
                .Add("date", DateTime.Now.ToString("dd-MMM-yyyy")).Add("time", DateTime.Now.ToString("hh:mm:ss"))
                .Add("g", gpId).Add("row", rowSl);
            // Soft-deleting the main row hides the whole installment (every return query
            // filters Return_Challan_Main.DeleteRow=0). The Return_*_Gate rows are left as
            // harmless orphans — those tables may not carry a DeleteRow column.
            tx.Execute("UPDATE Return_Challan_Main SET DeleteRow=1, UserID=@uid, PCName=@pc, EntryDate=@date, EntryTime=@time WHERE GPID=@g AND rowslno=@row AND DeleteRow=0", meta);
            tx.Execute("UPDATE Return_Challan_Sub SET DeleteRow=1, UserID=@uid, PCName=@pc, EntryDate=@date, EntryTime=@time WHERE GPID=@g AND rowslno=@row AND DeleteRow=0", meta);
        });
        return new OpResult(true, "Data Successfully Deleted");
    }

    private static int ResolveItemId(ISqlTransaction tx, string itemName)
    {
        var v = tx.Scalar("SELECT itemID FROM New_Item_Name WHERE itemName = @n", Params.New("n", itemName));
        return v == null ? 0 : Convert.ToInt32(v);
    }

    private static int ResolvePfId(ISqlTransaction tx, string pfNo)
    {
        var v = tx.Scalar("SELECT PFID FROM New_PFNo WHERE PFNo = @n", Params.New("n", pfNo));
        return v == null ? 0 : Convert.ToInt32(v);
    }

    private static int ResolveBuyerIdByPf(ISqlTransaction tx, int pfId)
    {
        var v = tx.Scalar("SELECT BuyerID FROM New_PFNo WHERE PFID = @id", Params.New("id", pfId));
        return v == null ? 0 : Convert.ToInt32(v);
    }
}
