using System.Data;
using GtrackWeb.Data;
using GtrackWeb.Helpers;
using GtrackWeb.Models;

namespace GtrackWeb.Services;

/// <summary>
/// Out-Company challan flow (frmOutCompanyChallanSender / gate / receiver).
/// Mirrors the in-company sender but persists to the parallel <c>OutCmpy_*</c>
/// tables (OutCmpy_Challan_Main / Sub + audit, OutCmpy_Sender_Gate) while still
/// sharing <c>NEW_GP</c> for GP numbering. The receiving party is an external
/// company, so there is only a sender gate; the receiver screen records receipt
/// directly (no gate preconditions, matching the desktop).
///
/// Deferred (README): EMB/PACKEGING pallet numbering, MailSender rows, and the
/// inner/outer (OptSign) employee-source distinction — here OptSign defaults to 'OUT'.
/// </summary>
public sealed class OutCompanyChallanService
{
    private readonly ISqlDataAccess _db;
    private readonly CurrentUser _user;

    public OutCompanyChallanService(ISqlDataAccess db, CurrentUser user)
    {
        _db = db;
        _user = user;
    }

    // ---- Edit / Delete -----------------------------------------------------

    private bool IsReceived(int gpId) =>
        _db.Scalar("SELECT 1 FROM OutCmpy_Challan_Main WHERE GPID=@g AND DeleteRow=0 AND ISNULL(DeleteRowR,0)=1",
            Params.New("g", gpId)) != null;

    public OpResult UpdateLines(int gpId, List<ChallanLineView> lines)
    {
        if (IsReceived(gpId)) return new OpResult(false, "Challan already received; cannot edit");

        _db.Transaction(tx =>
        {
            foreach (var l in lines)
            {
                tx.Execute(
                    @"UPDATE OutCmpy_Challan_Sub SET GPQty=@qty, QtyUnit=@unit, ItDesc=@desc, Remarks=@remarks,
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

    public OpResult DeleteLine(int gpId, int sl)
    {
        if (IsReceived(gpId)) return new OpResult(false, "Challan already received; cannot edit");

        var remaining = _db.Scalar(
            "SELECT COUNT(*) FROM OutCmpy_Challan_Sub WHERE GPID=@g AND DeleteRow=0", Params.New("g", gpId));
        if (remaining != null && Convert.ToInt32(remaining) <= 1)
            return new OpResult(false, "Sorry You Can not Delete This Single Item");

        _db.Execute(
            @"UPDATE OutCmpy_Challan_Sub SET DeleteRow=1, UserID=@uid, PCName=@pc, EntryDate=@date, EntryTime=@time
              WHERE GPID=@g AND SLNO=@sl AND DeleteRow=0",
            Params.New("uid", _user.UserId).Add("pc", _user.PcName)
                  .Add("date", DateTime.Now.ToString("dd-MMM-yyyy")).Add("time", DateTime.Now.ToString("hh:mm:ss"))
                  .Add("g", gpId).Add("sl", sl));
        return new OpResult(true, "Data Successfully Deleted");
    }

    public OpResult DeleteChallan(int gpId)
    {
        if (IsReceived(gpId)) return new OpResult(false, "Challan already received; cannot delete");

        var exists = _db.Scalar("SELECT GPNo FROM NEW_GP WHERE GPID=@g AND DeleteRow=0", Params.New("g", gpId));
        if (exists == null) return new OpResult(false, "Challan not found");
        var gpNo = exists.ToString();

        _db.Transaction(tx =>
        {
            var meta = Params.New("uid", _user.UserId).Add("pc", _user.PcName)
                .Add("date", DateTime.Now.ToString("dd-MMM-yyyy")).Add("time", DateTime.Now.ToString("hh:mm:ss"))
                .Add("g", gpId);
            tx.Execute("UPDATE NEW_GP SET DeleteRow=1, UserID=@uid, PCName=@pc, EntryDate=@date, EntryTime=@time WHERE GPID=@g AND DeleteRow=0", meta);
            tx.Execute("UPDATE OutCmpy_Challan_Main SET DeleteRow=1, UserID=@uid, PCName=@pc, EntryDate=@date, EntryTime=@time WHERE GPID=@g AND DeleteRow=0", meta);
            tx.Execute("UPDATE OutCmpy_Challan_Sub SET DeleteRow=1, UserID=@uid, PCName=@pc, EntryDate=@date, EntryTime=@time WHERE GPID=@g AND DeleteRow=0", meta);
            tx.Execute("UPDATE OutCmpy_Sender_Gate SET DeleteRow=1, UserID=@uid, PCName=@pc, EntryDate=@date, EntryTime=@time WHERE SGGPID=@g AND DeleteRow=0", meta);
            tx.Execute("UPDATE MailSender SET DeleteRow=1 WHERE Challan_No=@no AND DeleteRow=0", Params.New("no", gpNo));
        });
        return new OpResult(true, "Data Successfully Deleted");
    }

    public List<ChallanListRow> Recent(int top = 100)
    {
        var dt = _db.Query(
            $@"SELECT TOP {top} g.GPID, g.GPNo, g.GroupName, m.GPDate
               FROM NEW_GP g
               INNER JOIN OutCmpy_Challan_Main m ON m.GPID = g.GPID
               WHERE g.compID = @cid AND m.DeleteRow = 0
               ORDER BY g.GPID DESC",
            Params.New("cid", _user.CompId));

        return dt.AsEnumerable().Select(r => new ChallanListRow(
            Convert.ToInt32(r["GPID"]),
            r["GPNo"].ToString() ?? "",
            Convert.ToDateTime(r["GPDate"]).ToString("dd-MMM-yyyy"),
            r["GroupName"].ToString() ?? "")).ToList();
    }

    public ChallanService.SaveResult CreateSender(ChallanSenderInput input)
    {
        if (_user.CompId <= 0) return new ChallanService.SaveResult(false, "Select a company first");
        if (input.Lines.Count == 0) return new ChallanService.SaveResult(false, "Add at least one item");
        if (input.ReceiverCompId <= 0) return new ChallanService.SaveResult(false, "Select receiving company");

        var compId = _user.CompId;
        var year = _user.Year;

        return _db.Transaction(tx =>
        {
            var shortName = tx.Scalar("SELECT ComShortName FROM Company_Information WHERE CompID = @c",
                Params.New("c", compId))?.ToString();
            if (string.IsNullOrEmpty(shortName)) shortName = "NF";

            var maxGpNo = tx.Scalar("SELECT MAX(GP_No) FROM NEW_GP WHERE CompID = @c AND GP_Year = @y",
                Params.New("c", compId).Add("y", year));
            var gpNoSeq = (maxGpNo == null ? 0 : Convert.ToInt32(maxGpNo)) + 1;
            var seqStr = gpNoSeq.ToString("D5");
            var gpNo = $"{shortName}{compId}{seqStr}-{year}";

            var maxGpId = tx.Scalar("SELECT MAX(GPID) FROM NEW_GP");
            var gpId = (maxGpId == null ? 0 : Convert.ToInt32(maxGpId)) + 1;

            var returnable = input.Returnable ? "YES" : "NO";

            tx.Execute(
                @"INSERT INTO NEW_GP (GPID, compID, GroupName, GP_No, GP_Year, GPNo, UserID, PCName, EntryDate, EntryTime)
                  VALUES (@gpid, @cid, @group, @gpno, @year, @gpnostr, @uid, @pc,
                          CONVERT(DATETIME, FLOOR(CONVERT(FLOAT, GETDATE()))), CONVERT(VARCHAR(8), GETDATE(), 108))",
                Params.New("gpid", gpId).Add("cid", compId).Add("group", input.ItemGroupName)
                      .Add("gpno", seqStr).Add("year", year).Add("gpnostr", gpNo)
                      .Add("uid", _user.UserId).Add("pc", _user.PcName));

            tx.Execute(
                @"INSERT INTO OutCmpy_Challan_Main
                    (CompID, GPID, Gpdate, EMPID, MSID, SenderId, ReceiverId, CReceiverId, Driverid,
                     authid, pid, Vehicleno, OutTime, CType, Returnable, ReturnDate, UserID, PCName,
                     EntryDate, EntryTime, OptSign, DeleteRow)
                  VALUES
                    (@cid, @gpid, @gpdate, @emp, @msid, @emp, @recv, @carrier, @driver,
                     @auth, @pid, @vehicle, CONVERT(VARCHAR(8), GETDATE(), 108), @ctype, @returnable, @rdate,
                     @uid, @pc, CONVERT(DATETIME, FLOOR(CONVERT(FLOAT, GETDATE()))), CONVERT(VARCHAR(8), GETDATE(), 108), 'OUT', 0)",
                Params.New("cid", compId).Add("gpid", gpId).Add("gpdate", input.GpDate)
                      .Add("emp", input.SenderEmpId).Add("msid", input.ReceiverCompId)
                      .Add("recv", input.ReceiverEmpId).Add("carrier", input.CarrierEmpId)
                      .Add("driver", input.DriverId).Add("auth", input.AuthId).Add("pid", input.PurposeId)
                      .Add("vehicle", input.VehicleNo).Add("ctype", input.ChallanType)
                      .Add("returnable", returnable).Add("rdate", input.ReturnDate)
                      .Add("uid", _user.UserId).Add("pc", _user.PcName));

            var sl = 0;
            foreach (var line in input.Lines)
            {
                sl++;
                var itemId = ResolveItemId(tx, line.ItemName);
                var pfId = ResolvePfId(tx, line.PfNo);
                var buyerId = ResolveBuyerIdByPf(tx, pfId);

                var p = Params.New("gpid", gpId).Add("ittype", line.ItemGroup)
                    .Add("item", itemId).Add("buyer", buyerId).Add("pf", pfId)
                    .Add("desc", line.Description).Add("unit", line.Unit).Add("qty", line.Quantity)
                    .Add("remarks", line.Remarks).Add("sl", sl)
                    .Add("uid", _user.UserId).Add("pc", _user.PcName);

                tx.Execute(
                    @"INSERT INTO OutCmpy_Challan_Sub
                        (GPID, ittype, ItemID, BuyerID, PFID, ItDesc, QtyUnit, GPQty, Remarks, SLNO,
                         UserID, PCName, EntryDate, EntryTime, DeleteRow)
                      VALUES (@gpid, @ittype, @item, @buyer, @pf, @desc, @unit, @qty, @remarks, @sl,
                              @uid, @pc, CONVERT(DATETIME, FLOOR(CONVERT(FLOAT, GETDATE()))),
                              CONVERT(VARCHAR(8), GETDATE(), 108), 0)", p);

                tx.Execute(
                    @"INSERT INTO OutCmpy_Challan_Sub_Del_Edit
                        (GPID, ittype, ItemID, BuyerID, PFID, ItDesc, QtyUnit, GPQty, Remarks, SLNO,
                         UserID, PCName, EntryDate, EntryTime)
                      VALUES (@gpid, @ittype, @item, @buyer, @pf, @desc, @unit, @qty, @remarks, @sl,
                              @uid, @pc, CONVERT(DATETIME, FLOOR(CONVERT(FLOAT, GETDATE()))),
                              CONVERT(VARCHAR(8), GETDATE(), 108))", p);
            }

            tx.Execute(
                "INSERT INTO OutCmpy_Sender_Gate (SGCompID, SGGPID, SGsign) VALUES (@c, @g, 0)",
                Params.New("c", compId).Add("g", gpId));

            return new ChallanService.SaveResult(true, "Out-Company Challan Saved Successfully", gpNo);
        });
    }

    public ChallanHeaderInfo? FindByGpNo(string gpNo)
    {
        gpNo = (gpNo ?? "").Trim();
        if (gpNo.Length == 0) return null;

        var dt = _db.Query(
            @"SELECT g.GPID, g.GPNo, g.GroupName, m.GPDate,
                     ISNULL(sg.SGsign, 0) AS SGsign, ISNULL(m.DeleteRowR, 0) AS DeleteRowR
              FROM NEW_GP g
              INNER JOIN OutCmpy_Challan_Main m ON m.GPID = g.GPID AND m.DeleteRow = 0
              LEFT JOIN OutCmpy_Sender_Gate sg ON sg.SGGPID = g.GPID
              WHERE g.GPNo = @gpno",
            Params.New("gpno", gpNo));

        if (dt.Rows.Count == 0) return null;
        var r = dt.Rows[0];
        return new ChallanHeaderInfo(
            Convert.ToInt32(r["GPID"]), r["GPNo"].ToString() ?? "",
            Convert.ToDateTime(r["GPDate"]).ToString("dd-MMM-yyyy"),
            r["GroupName"].ToString() ?? "",
            Convert.ToInt32(r["SGsign"]) == 1,
            true,  // external receiver has no gate; treated as passed
            Convert.ToInt32(r["DeleteRowR"]) == 1);
    }

    public List<ChallanLineView> Lines(int gpId)
    {
        var dt = _db.Query(
            @"SELECT s.SLNO, s.ittype, ISNULL(i.itemName,'') AS itemName, ISNULL(p.PFNo,'') AS PFNo,
                     ISNULL(s.ItDesc,'') AS ItDesc, ISNULL(s.QtyUnit,'') AS QtyUnit,
                     ISNULL(s.GPQty,0) AS GPQty, ISNULL(s.RecQty,0) AS RecQty, ISNULL(s.Remarks,'') AS Remarks
              FROM OutCmpy_Challan_Sub s
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

    public OpResult SenderGate(string gpNo, string date, string time, string remark)
    {
        var info = FindByGpNo(gpNo);
        if (info == null) return new OpResult(false, "Please Type Correct Challan-No");
        if (info.SenderGateOk) return new OpResult(false, "Sender Gate Already Ok");
        if (string.IsNullOrWhiteSpace(date)) return new OpResult(false, "Type Sender Gate Date");
        if (string.IsNullOrWhiteSpace(time)) return new OpResult(false, "Type Sender Gate Time");

        _db.Execute(
            @"UPDATE OutCmpy_Sender_Gate SET SGateDate=@d, SGateTime=@t, SGateremark=@r, UserID=@uid, PCName=@pc,
                     EntryDate=@d, EntryTime=CONVERT(VARCHAR(8), GETDATE(), 108), SGsign=1
              WHERE SGCompID=@c AND SGGPID=@g AND SGsign=0",
            Params.New("d", date).Add("t", time).Add("r", remark ?? "").Add("uid", _user.UserId)
                  .Add("pc", _user.PcName).Add("c", _user.CompId).Add("g", info.GpId));

        return new OpResult(true, "Data Successfully Save");
    }

    public OpResult Receive(int gpId, string date, string time, List<ReceiveLineInput> lines)
    {
        if (string.IsNullOrWhiteSpace(date)) return new OpResult(false, "Type Received Date");
        if (string.IsNullOrWhiteSpace(time)) return new OpResult(false, "Type Received Time");

        _db.Transaction(tx =>
        {
            tx.Execute(
                @"UPDATE OutCmpy_Challan_Main SET RcvDate=@d, RcvTime=@t, DeleteRowR=1, RUserID=@uid, RPCName=@pc,
                         REntryDate=CONVERT(DATETIME, FLOOR(CONVERT(FLOAT, GETDATE()))),
                         REntryTime=CONVERT(VARCHAR(8), GETDATE(), 108)
                  WHERE DeleteRow=0 AND GPID=@g",
                Params.New("d", date).Add("t", time).Add("uid", _user.UserId).Add("pc", _user.PcName).Add("g", gpId));

            foreach (var line in lines)
            {
                tx.Execute(
                    @"UPDATE OutCmpy_Challan_Sub SET RecQty=@q, Remarks=@r, DeleteRowR=1, RUserID=@uid, RPCName=@pc,
                             REntryDate=CONVERT(DATETIME, FLOOR(CONVERT(FLOAT, GETDATE()))),
                             REntryTime=CONVERT(VARCHAR(8), GETDATE(), 108)
                      WHERE DeleteRow=0 AND GPID=@g AND SLNO=@sl",
                    Params.New("q", line.RecQty).Add("r", line.Remarks ?? "").Add("uid", _user.UserId)
                          .Add("pc", _user.PcName).Add("g", gpId).Add("sl", line.Sl));
            }
        });

        return new OpResult(true, "Data Successfully Save");
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
