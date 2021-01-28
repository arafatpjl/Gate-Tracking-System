using System.Data;
using GtrackWeb.Data;
using GtrackWeb.Helpers;
using GtrackWeb.Models;

namespace GtrackWeb.Services;

/// <summary>
/// Port of <c>form.frmChallanSender</c> (outgoing / in-company challan entry).
/// Saving a challan is a master-detail transaction across NEW_GP, Challan_Main,
/// Challan_Sub (+ audit table Challan_Sub_Del_Edit) and the Sender/Receiver gate
/// tables. GP numbering mirrors the desktop:
///   GPNo = ComShortName + CompID + {5-digit sequence} + "-" + Year.
///
/// Deferred to a later phase (documented in README): EMBOYDARY/PACKEGING pallet
/// numbering and the MailSender notification rows.
/// </summary>
public sealed class ChallanService
{
    private readonly ISqlDataAccess _db;
    private readonly CurrentUser _user;

    public ChallanService(ISqlDataAccess db, CurrentUser user)
    {
        _db = db;
        _user = user;
    }

    public sealed record SaveResult(bool Ok, string Message, string? GpNo = null);

    /// <summary>Recent challans for the current operating company (for the list view).</summary>
    public List<ChallanListRow> Recent(int top = 100)
    {
        var dt = _db.Query(
            $@"SELECT TOP {top} g.GPID, g.GPNo, g.GroupName, m.GPDate
               FROM NEW_GP g
               INNER JOIN Challan_Main m ON m.GPID = g.GPID
               WHERE g.compID = @cid AND m.DeleteRow = 0
               ORDER BY g.GPID DESC",
            Params.New("cid", _user.CompId));

        return dt.AsEnumerable().Select(r => new ChallanListRow(
            Convert.ToInt32(r["GPID"]),
            r["GPNo"].ToString() ?? "",
            Convert.ToDateTime(r["GPDate"]).ToString("dd-MMM-yyyy"),
            r["GroupName"].ToString() ?? "")).ToList();
    }

    public SaveResult CreateSender(ChallanSenderInput input)
    {
        if (_user.CompId <= 0) return new SaveResult(false, "Select a company first");
        if (input.Lines.Count == 0) return new SaveResult(false, "Add at least one item");
        if (input.ReceiverCompId <= 0) return new SaveResult(false, "Select receiving company");

        var compId = _user.CompId;
        var year = _user.Year;

        return _db.Transaction(tx =>
        {
            // ---- Company short code (for the GP number prefix) ------------------
            var shortName = tx.Scalar(
                "SELECT ComShortName FROM Company_Information WHERE CompID = @c",
                Params.New("c", compId))?.ToString();
            if (string.IsNullOrEmpty(shortName)) shortName = "NF";

            // ---- Next GP_No (per company + year) --------------------------------
            var maxGpNo = tx.Scalar(
                "SELECT MAX(GP_No) FROM NEW_GP WHERE CompID = @c AND GP_Year = @y",
                Params.New("c", compId).Add("y", year));
            var gpNoSeq = (maxGpNo == null ? 0 : Convert.ToInt32(maxGpNo)) + 1;
            var seqStr = gpNoSeq.ToString("D5");                       // findGPNO padding
            var gpNo = $"{shortName}{compId}{seqStr}-{year}";

            // ---- Next GPID (global) ---------------------------------------------
            var maxGpId = tx.Scalar("SELECT MAX(GPID) FROM NEW_GP");
            var gpId = (maxGpId == null ? 0 : Convert.ToInt32(maxGpId)) + 1;

            var returnable = input.Returnable ? "YES" : "NO";

            // ---- NEW_GP ---------------------------------------------------------
            tx.Execute(
                @"INSERT INTO NEW_GP (GPID, compID, GroupName, GP_No, GP_Year, GPNo, UserID, PCName, EntryDate, EntryTime)
                  VALUES (@gpid, @cid, @group, @gpno, @year, @gpnostr, @uid, @pc,
                          CONVERT(DATETIME, FLOOR(CONVERT(FLOAT, GETDATE()))), CONVERT(VARCHAR(8), GETDATE(), 108))",
                Params.New("gpid", gpId).Add("cid", compId).Add("group", input.ItemGroupName)
                      .Add("gpno", seqStr).Add("year", year).Add("gpnostr", gpNo)
                      .Add("uid", _user.UserId).Add("pc", _user.PcName));

            // ---- Challan_Main ---------------------------------------------------
            tx.Execute(
                @"INSERT INTO Challan_Main
                    (CompID, GPID, Gpdate, EMPID, MSID, SenderId, ReceiverId, CReceiverId, Driverid,
                     authid, pid, Vehicleno, OutTime, CType, Returnable, ReturnDate, UserID, PCName,
                     EntryDate, EntryTime, DeleteRow)
                  VALUES
                    (@cid, @gpid, @gpdate, @emp, @msid, @emp, @recv, @carrier, @driver,
                     @auth, @pid, @vehicle, CONVERT(VARCHAR(8), GETDATE(), 108), @ctype, @returnable, @rdate,
                     @uid, @pc, CONVERT(DATETIME, FLOOR(CONVERT(FLOAT, GETDATE()))), CONVERT(VARCHAR(8), GETDATE(), 108), 0)",
                Params.New("cid", compId).Add("gpid", gpId).Add("gpdate", input.GpDate)
                      .Add("emp", input.SenderEmpId).Add("msid", input.ReceiverCompId)
                      .Add("recv", input.ReceiverEmpId).Add("carrier", input.CarrierEmpId)
                      .Add("driver", input.DriverId).Add("auth", input.AuthId).Add("pid", input.PurposeId)
                      .Add("vehicle", input.VehicleNo).Add("ctype", input.ChallanType)
                      .Add("returnable", returnable).Add("rdate", input.ReturnDate)
                      .Add("uid", _user.UserId).Add("pc", _user.PcName));

            // ---- Challan_Sub (+ audit copy) -------------------------------------
            var sl = 0;
            foreach (var line in input.Lines)
            {
                sl++;
                var itemId = ResolveItemId(tx, line.ItemName);
                var pfId = ResolvePfId(tx, line.PfNo);
                var buyerId = ResolveBuyerIdByPf(tx, pfId);

                var lineParams = Params.New("gpid", gpId).Add("ittype", line.ItemGroup)
                    .Add("item", itemId).Add("buyer", buyerId).Add("pf", pfId)
                    .Add("desc", line.Description).Add("unit", line.Unit).Add("qty", line.Quantity)
                    .Add("remarks", line.Remarks).Add("sl", sl)
                    .Add("uid", _user.UserId).Add("pc", _user.PcName);

                tx.Execute(
                    @"INSERT INTO Challan_Sub
                        (GPID, ittype, ItemID, BuyerID, PFID, ItDesc, QtyUnit, GPQty, Remarks, SLNO,
                         UserID, PCName, EntryDate, EntryTime, DeleteRow)
                      VALUES (@gpid, @ittype, @item, @buyer, @pf, @desc, @unit, @qty, @remarks, @sl,
                              @uid, @pc, CONVERT(DATETIME, FLOOR(CONVERT(FLOAT, GETDATE()))),
                              CONVERT(VARCHAR(8), GETDATE(), 108), 0)",
                    lineParams);

                tx.Execute(
                    @"INSERT INTO Challan_Sub_Del_Edit
                        (GPID, ittype, ItemID, BuyerID, PFID, ItDesc, QtyUnit, GPQty, Remarks, SLNO,
                         UserID, PCName, EntryDate, EntryTime)
                      VALUES (@gpid, @ittype, @item, @buyer, @pf, @desc, @unit, @qty, @remarks, @sl,
                              @uid, @pc, CONVERT(DATETIME, FLOOR(CONVERT(FLOAT, GETDATE()))),
                              CONVERT(VARCHAR(8), GETDATE(), 108))",
                    lineParams);
            }

            // ---- Gate rows ------------------------------------------------------
            tx.Execute(
                "INSERT INTO Sender_Gate (SGCompID, SGGPID, SGsign) VALUES (@c, @g, 0)",
                Params.New("c", compId).Add("g", gpId));
            tx.Execute(
                "INSERT INTO Receiver_Gate (RGCompID, RGGPID, RGsign) VALUES (@c, @g, 0)",
                Params.New("c", input.ReceiverCompId).Add("g", gpId));

            return new SaveResult(true, "Challan Saved Successfully", gpNo);
        });
    }

    // ========================================================================
    //  Challan lifecycle: Sender Gate -> Receiver Gate -> Receiver
    //  Signs live in Sender_Gate.SGsign / Receiver_Gate.RGsign (0 = pending,
    //  1 = passed). Receipt sets Challan_Main/Sub.DeleteRowR = 1.
    // ========================================================================

    /// <summary>Look up an existing challan by its GP number, with gate/receive status.</summary>
    public ChallanHeaderInfo? FindByGpNo(string gpNo)
    {
        gpNo = (gpNo ?? "").Trim();
        if (gpNo.Length == 0) return null;

        var dt = _db.Query(
            @"SELECT g.GPID, g.GPNo, g.GroupName, m.GPDate,
                     ISNULL(sg.SGsign, 0) AS SGsign, ISNULL(rg.RGsign, 0) AS RGsign,
                     ISNULL(m.DeleteRowR, 0) AS DeleteRowR
              FROM NEW_GP g
              INNER JOIN Challan_Main m ON m.GPID = g.GPID AND m.DeleteRow = 0
              LEFT JOIN Sender_Gate sg ON sg.SGGPID = g.GPID
              LEFT JOIN Receiver_Gate rg ON rg.RGGPID = g.GPID
              WHERE g.GPNo = @gpno",
            Params.New("gpno", gpNo));

        if (dt.Rows.Count == 0) return null;
        var r = dt.Rows[0];
        return new ChallanHeaderInfo(
            Convert.ToInt32(r["GPID"]),
            r["GPNo"].ToString() ?? "",
            Convert.ToDateTime(r["GPDate"]).ToString("dd-MMM-yyyy"),
            r["GroupName"].ToString() ?? "",
            Convert.ToInt32(r["SGsign"]) == 1,
            Convert.ToInt32(r["RGsign"]) == 1,
            Convert.ToInt32(r["DeleteRowR"]) == 1);
    }

    /// <summary>Line items of a challan for the receiver screen.</summary>
    public List<ChallanLineView> Lines(int gpId)
    {
        var dt = _db.Query(
            @"SELECT s.SLNO, s.ittype, ISNULL(i.itemName,'') AS itemName, ISNULL(p.PFNo,'') AS PFNo,
                     ISNULL(s.ItDesc,'') AS ItDesc, ISNULL(s.QtyUnit,'') AS QtyUnit,
                     ISNULL(s.GPQty,0) AS GPQty, ISNULL(s.RecQty,0) AS RecQty, ISNULL(s.Remarks,'') AS Remarks
              FROM Challan_Sub s
              LEFT JOIN New_Item_Name i ON i.itemID = s.ItemID
              LEFT JOIN New_PFNo p ON p.PFID = s.PFID
              WHERE s.GPID = @g AND s.DeleteRow = 0
              ORDER BY s.SLNO",
            Params.New("g", gpId));

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

    /// <summary>Sender-gate check-out (frmChallan_Sender_Gate): sets SGsign = 1.</summary>
    public OpResult SenderGate(string gpNo, string date, string time, string remark)
    {
        var info = FindByGpNo(gpNo);
        if (info == null) return new OpResult(false, "Please Type Correct Challan-No");
        if (info.SenderGateOk) return new OpResult(false, "Sender Gate Already Ok");
        if (string.IsNullOrWhiteSpace(date)) return new OpResult(false, "Type Sender Gate Date");
        if (string.IsNullOrWhiteSpace(time)) return new OpResult(false, "Type Sender Gate Time");

        _db.Transaction(tx =>
        {
            // If the receiving company is an out-company, its receiver gate is auto-passed here too.
            var msid = tx.Scalar("SELECT MSID FROM Challan_Main WHERE GPID = @g", Params.New("g", info.GpId));
            if (msid != null && IsOutCompany(tx, Convert.ToInt32(msid)))
            {
                tx.Execute(
                    @"UPDATE Receiver_Gate SET RGateDate=@d, RGateTime=@t, RGateremark=@r, UserID=@uid,
                             PCName=@pc, EntryDate=@d, EntryTime=CONVERT(VARCHAR(8), GETDATE(), 108), RGsign=1
                      WHERE RGCompID=@c AND RGGPID=@g AND RGsign=0",
                    GateParams(date, time, remark, Convert.ToInt32(msid), info.GpId));
            }

            tx.Execute(
                @"UPDATE Sender_Gate SET SGateDate=@d, SGateTime=@t, SGateremark=@r, UserID=@uid,
                         PCName=@pc, EntryDate=@d, EntryTime=CONVERT(VARCHAR(8), GETDATE(), 108), SGsign=1
                  WHERE SGCompID=@c AND SGGPID=@g AND SGsign=0",
                GateParams(date, time, remark, _user.CompId, info.GpId));
        });

        return new OpResult(true, "Data Successfully Save");
    }

    /// <summary>Receiver-gate check-in (frmChallan_Receiver_Gate): sets RGsign = 1.</summary>
    public OpResult ReceiverGate(string gpNo, string date, string time, string remark)
    {
        var info = FindByGpNo(gpNo);
        if (info == null) return new OpResult(false, "Please Type Correct Challan No");
        if (info.ReceiverGateOk) return new OpResult(false, "Receiver Gate Already Ok");
        if (string.IsNullOrWhiteSpace(date)) return new OpResult(false, "Type Receiver Gate Date");
        if (string.IsNullOrWhiteSpace(time)) return new OpResult(false, "Type Receiver Gate Time");

        _db.Transaction(tx =>
        {
            // If the sending company is an out-company, its sender gate is auto-passed here.
            var scompid = tx.Scalar("SELECT CompID FROM NEW_GP WHERE GPID = @g", Params.New("g", info.GpId));
            if (scompid != null && IsOutCompany(tx, Convert.ToInt32(scompid)))
            {
                tx.Execute(
                    @"UPDATE Sender_Gate SET SGateDate=@d, SGateTime=@t, SGateremark=@r, UserID=@uid,
                             PCName=@pc, EntryDate=@d, EntryTime=CONVERT(VARCHAR(8), GETDATE(), 108), SGsign=1
                      WHERE SGCompID=@c AND SGGPID=@g AND SGsign=0",
                    GateParams(date, time, remark, Convert.ToInt32(scompid), info.GpId));
            }

            tx.Execute(
                @"UPDATE Receiver_Gate SET RGateDate=@d, RGateTime=@t, RGateremark=@r, UserID=@uid,
                         PCName=@pc, EntryDate=@d, EntryTime=CONVERT(VARCHAR(8), GETDATE(), 108), RGsign=1
                  WHERE RGCompID=@c AND RGGPID=@g AND RGsign=0",
                GateParams(date, time, remark, _user.CompId, info.GpId));
        });

        return new OpResult(true, "Data Successfully Save");
    }

    /// <summary>Receiver confirmation (frmChallanReceiver): requires both gates passed;
    /// records received qty/remark per line and marks the challan received (DeleteRowR = 1).</summary>
    public OpResult Receive(int gpId, string date, string time, List<ReceiveLineInput> lines)
    {
        if (string.IsNullOrWhiteSpace(date)) return new OpResult(false, "Type Received Date");
        if (string.IsNullOrWhiteSpace(time)) return new OpResult(false, "Type Received Time");

        var sgPending = _db.Scalar(
            "SELECT 1 FROM Sender_Gate WHERE SGGPID = @g AND SGsign = 0", Params.New("g", gpId));
        if (sgPending != null) return new OpResult(false, "Sender Gate Not Ok");

        var rgPending = _db.Scalar(
            "SELECT 1 FROM Receiver_Gate WHERE RGGPID = @g AND RGsign = 0", Params.New("g", gpId));
        if (rgPending != null) return new OpResult(false, "Receiver Gate Not Ok");

        _db.Transaction(tx =>
        {
            tx.Execute(
                @"UPDATE Challan_Main SET RcvDate=@d, RcvTime=@t, DeleteRowR=1, RUserID=@uid, RPCName=@pc,
                         REntryDate=CONVERT(DATETIME, FLOOR(CONVERT(FLOAT, GETDATE()))),
                         REntryTime=CONVERT(VARCHAR(8), GETDATE(), 108)
                  WHERE DeleteRow=0 AND GPID=@g",
                Params.New("d", date).Add("t", time).Add("uid", _user.UserId).Add("pc", _user.PcName).Add("g", gpId));

            foreach (var line in lines)
            {
                tx.Execute(
                    @"UPDATE Challan_Sub SET RecQty=@q, Remarks=@r, DeleteRowR=1, RUserID=@uid, RPCName=@pc,
                             REntryDate=CONVERT(DATETIME, FLOOR(CONVERT(FLOAT, GETDATE()))),
                             REntryTime=CONVERT(VARCHAR(8), GETDATE(), 108)
                      WHERE DeleteRow=0 AND GPID=@g AND SLNO=@sl",
                    Params.New("q", line.RecQty).Add("r", line.Remarks ?? "").Add("uid", _user.UserId)
                          .Add("pc", _user.PcName).Add("g", gpId).Add("sl", line.Sl));
            }
        });

        return new OpResult(true, "Data Successfully Save");
    }

    // ========================================================================
    //  Challan edit / delete (frmChallanSender edit & delete branches)
    // ========================================================================

    /// <summary>Guard: a received challan (DeleteRowR = 1) is locked from edits/deletes.</summary>
    private bool IsReceived(int gpId) =>
        _db.Scalar("SELECT 1 FROM Challan_Main WHERE GPID = @g AND DeleteRow = 0 AND ISNULL(DeleteRowR,0) = 1",
            Params.New("g", gpId)) != null;

    /// <summary>Update editable fields (qty/unit/description/remarks) of a challan's lines.
    /// Item and PF assignments are left unchanged.</summary>
    public OpResult UpdateLines(int gpId, List<ChallanLineView> lines)
    {
        if (IsReceived(gpId)) return new OpResult(false, "Challan already received; cannot edit");

        _db.Transaction(tx =>
        {
            foreach (var l in lines)
            {
                tx.Execute(
                    @"UPDATE Challan_Sub SET GPQty=@qty, QtyUnit=@unit, ItDesc=@desc, Remarks=@remarks,
                             UserID=@uid, PCName=@pc, EntryDate=@date, EntryTime=@time
                      WHERE GPID=@g AND SLNO=@sl AND DeleteRow=0",
                    Params.New("qty", l.GpQty).Add("unit", l.Unit).Add("desc", l.Description)
                          .Add("remarks", l.Remarks).Add("uid", _user.UserId).Add("pc", _user.PcName)
                          .Add("date", DateTime.Now.ToString("dd-MMM-yyyy")).Add("time", DateTime.Now.ToString("hh:mm:ss"))
                          .Add("g", gpId).Add("sl", l.Sl));
            }
        });

        return new OpResult(true, "Data Update Successfully");
    }

    /// <summary>Soft-delete a single challan line (cannot delete the last remaining line).</summary>
    public OpResult DeleteLine(int gpId, int sl)
    {
        if (IsReceived(gpId)) return new OpResult(false, "Challan already received; cannot edit");

        var remaining = _db.Scalar(
            "SELECT COUNT(*) FROM Challan_Sub WHERE GPID = @g AND DeleteRow = 0", Params.New("g", gpId));
        if (remaining != null && Convert.ToInt32(remaining) <= 1)
            return new OpResult(false, "Sorry You Can not Delete This Single Item");

        _db.Execute(
            @"UPDATE Challan_Sub SET DeleteRow=1, UserID=@uid, PCName=@pc, EntryDate=@date, EntryTime=@time
              WHERE GPID=@g AND SLNO=@sl AND DeleteRow=0",
            Params.New("uid", _user.UserId).Add("pc", _user.PcName)
                  .Add("date", DateTime.Now.ToString("dd-MMM-yyyy")).Add("time", DateTime.Now.ToString("hh:mm:ss"))
                  .Add("g", gpId).Add("sl", sl));

        return new OpResult(true, "Data Successfully Deleted");
    }

    /// <summary>Soft-delete an entire challan (New_GP + Challan_Main + Challan_Sub + gates + mail).</summary>
    public OpResult DeleteChallan(int gpId)
    {
        if (IsReceived(gpId)) return new OpResult(false, "Challan already received; cannot delete");

        var exists = _db.Scalar(
            "SELECT GPNo FROM NEW_GP WHERE GPID = @g AND DeleteRow = 0", Params.New("g", gpId));
        if (exists == null) return new OpResult(false, "Challan not found");
        var gpNo = exists.ToString();

        _db.Transaction(tx =>
        {
            var meta = Params.New("uid", _user.UserId).Add("pc", _user.PcName)
                .Add("date", DateTime.Now.ToString("dd-MMM-yyyy")).Add("time", DateTime.Now.ToString("hh:mm:ss"))
                .Add("g", gpId);

            tx.Execute("UPDATE NEW_GP SET DeleteRow=1, UserID=@uid, PCName=@pc, EntryDate=@date, EntryTime=@time WHERE GPID=@g AND DeleteRow=0", meta);
            tx.Execute("UPDATE Challan_Main SET DeleteRow=1, UserID=@uid, PCName=@pc, EntryDate=@date, EntryTime=@time WHERE GPID=@g AND DeleteRow=0", meta);
            tx.Execute("UPDATE Challan_Sub SET DeleteRow=1, UserID=@uid, PCName=@pc, EntryDate=@date, EntryTime=@time WHERE GPID=@g AND DeleteRow=0", meta);
            tx.Execute("UPDATE Sender_Gate SET DeleteRow=1, UserID=@uid, PCName=@pc, EntryDate=@date, EntryTime=@time WHERE SGGPID=@g AND DeleteRow=0", meta);
            tx.Execute("UPDATE Receiver_Gate SET DeleteRow=1, UserID=@uid, PCName=@pc, EntryDate=@date, EntryTime=@time WHERE RGGPID=@g AND DeleteRow=0", meta);
            tx.Execute("UPDATE MailSender SET DeleteRow=1 WHERE Challan_No=@no AND DeleteRow=0", Params.New("no", gpNo));
        });

        return new OpResult(true, "Data Successfully Deleted");
    }

    private Params GateParams(string date, string time, string remark, int compId, int gpId) =>
        Params.New("d", date).Add("t", time).Add("r", remark ?? "")
              .Add("uid", _user.UserId).Add("pc", _user.PcName).Add("c", compId).Add("g", gpId);

    private static bool IsOutCompany(ISqlTransaction tx, int compId) =>
        tx.Scalar("SELECT CompID FROM Company_Information_OUT WHERE CompID = @c",
            Params.New("c", compId)) != null;

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
        // Desktop: findMainBuyerID(findPFID(pfNo)) -> buyer linked to the PF number.
        var v = tx.Scalar("SELECT BuyerID FROM New_PFNo WHERE PFID = @id", Params.New("id", pfId));
        return v == null ? 0 : Convert.ToInt32(v);
    }
}
