using System.Data;
using GtrackWeb.Data;
using GtrackWeb.Helpers;

namespace GtrackWeb.Services;

/// <summary>
/// Reporting queries. The desktop reports (frmRpt*) were WinForms bound to
/// SQL Server views filtered by GP-date range + company. This service runs those
/// same views with parameters and returns a <see cref="DataTable"/> that the
/// generic report view renders (with CSV export).
///
/// View names are chosen from a fixed whitelist (never from user input) so the
/// mode selector cannot be used for SQL injection.
/// </summary>
public sealed class ReportService
{
    private readonly ISqlDataAccess _db;
    private readonly CurrentUser _user;

    public ReportService(ISqlDataAccess db, CurrentUser user)
    {
        _db = db;
        _user = user;
    }

    public sealed record ReportMode(string Key, string Label);

    // ---- Challan Auditing (frmRptChallanAuditing) --------------------------

    public static readonly IReadOnlyList<ReportMode> ChallanModes = new List<ReportMode>
    {
        new("SenderSend", "Sender – Sent"),
        new("SenderPending", "Sender – Pending"),
        new("ReceiverPending", "Receiver – Pending"),
    };

    public DataTable ChallanAuditing(string mode, string from, string to)
    {
        // (view, company-filter column) whitelisted per mode.
        var (view, compCol) = mode switch
        {
            "SenderPending" => ("VIEWChallanSdrPending", "sdrcompid"),
            "ReceiverPending" => ("VIEWChallanRcvrPending", "MSID"),
            _ => ("VIEWChallanSenderSend", "sdrcompid"),
        };
        return RunChallanView(view, compCol, from, to);
    }

    // ---- Gate Auditing (frmRptGateChallanAuditing) -------------------------

    public static readonly IReadOnlyList<ReportMode> GateModes = new List<ReportMode>
    {
        new("SenderGateReceived", "Sender Gate – Received"),
        new("SenderGatePending", "Sender Gate – Pending"),
        new("ReceiverGatePending", "Receiver Gate – Pending"),
    };

    public DataTable GateAuditing(string mode, string from, string to)
    {
        var (view, compCol) = mode switch
        {
            "SenderGatePending" => ("VIEWChallanSdrGatePending", "sdrcompid"),
            "ReceiverGatePending" => ("VIEW_RcvrGatePending_Report", "MSID"),
            _ => ("VIEW_SdrGateRcvd_Report", "sdrcompid"),
        };
        return RunChallanView(view, compCol, from, to);
    }

    private DataTable RunChallanView(string view, string compCol, string from, string to)
    {
        // 'view' and 'compCol' come only from the whitelists above.
        var sql =
            $@"SELECT SdrCompName, RcvrCompName, GPDate, GPNo, SdrName, SdrEMPCode, SdrDept, SdrSec,
                      RcvrName, RcvrEMPCode, RcvrDept, RcvrSec, itemName, SendQty
               FROM {view}
               WHERE gpdate BETWEEN @from AND @to AND {compCol} = @cid
               ORDER BY GPDate, GPNo";
        return _db.Query(sql,
            Params.New("from", from).Add("to", to).Add("cid", _user.CompId));
    }

    // ---- Return Challan Auditing (frmRptReturnChallanAuditing) --------------

    public static readonly IReadOnlyList<ReportMode> ReturnModes = new List<ReportMode>
    {
        new("Received", "Received"),
        new("SenderPending", "Sender – Pending"),
        new("ReceiverPending", "Receiver – Pending"),
    };

    public DataTable ReturnAuditing(string mode, string from, string to)
    {
        var (view, compCol) = mode switch
        {
            "SenderPending" => ("VIEWReturnChallanSdrPending", "sdrcompid"),
            "ReceiverPending" => ("VIEW_ReturnRcvrPending_Report", "MSID"),
            _ => ("VIEW_Rcvd_Return_Challan_Report", "sdrcompid"),
        };
        return RunView(view, compCol, from, to);
    }

    // ---- Out-Company Challan Auditing (frmRptOutCmpyChallanAuditing) --------

    public static readonly IReadOnlyList<ReportMode> OutCompanyModes = new List<ReportMode>
    {
        new("SenderSend", "Sender – Sent"),
        new("SenderPending", "Sender – Pending"),
    };

    public DataTable OutCompanyAuditing(string mode, string from, string to)
    {
        var (view, compCol) = mode switch
        {
            "SenderPending" => ("VIEWOUTCMPYChallanSdrPending", "sdrcompid"),
            _ => ("VIEWOUTCMPYChallanSdrSend", "sdrcompid"),
        };
        return RunView(view, compCol, from, to);
    }

    // ---- Company-wise All Challan (frmRptallcompanychallan) -----------------

    public static readonly IReadOnlyList<ReportMode> CompanyWiseModes = new List<ReportMode>
    {
        new("All", "All Challan"),
    };

    public DataTable CompanyWise(string mode, string from, string to) =>
        RunView("View_companywise_dateWise_Allchallan", "sdrcompid", from, to);

    // ---- User-wise Challan (frmrptuserwiseInfo / frmrptuserwisereceiver) ----

    public static readonly IReadOnlyList<ReportMode> UserWiseModes = new List<ReportMode>
    {
        new("Sender", "Sender"),
        new("Receiver", "Receiver"),
    };

    public DataTable UserWise(string mode, int userId, string from, string to)
    {
        var compCol = mode == "Receiver" ? "MSID" : "sdrcompid";
        var sql =
            $@"SELECT * FROM VIEW_UserWiseSdrRcvr_Challan_Report
               WHERE gpdate BETWEEN @from AND @to AND {compCol} = @cid
                 AND (@uid = 0 OR userid = @uid)
               ORDER BY GPDate, GPNo";
        return _db.Query(sql,
            Params.New("from", from).Add("to", to).Add("cid", _user.CompId).Add("uid", userId));
    }

    // ---- Short / Excess Summary (frmrptShortExInfo) ------------------------

    public static readonly IReadOnlyList<ReportMode> ShortExcessModes = new List<ReportMode>
    {
        new("All", "Short / Excess Summary"),
    };

    public DataTable ShortExcess(string mode, string from, string to) =>
        RunView("ViewSummShort", "compid", from, to);

    // ---- Returnable Challan Qty (frmReturnableChallanQtyreport) -------------

    public static readonly IReadOnlyList<ReportMode> ReturnableQtyModes = new List<ReportMode>
    {
        new("NotReturned", "Not Returned"),
        new("Short", "Short (partially returned)"),
        new("AllReturned", "Fully Returned"),
    };

    public DataTable ReturnableQty(string mode, string from, string to)
    {
        // GPQTY = sent qty, RGPQTY = returned qty, RecQty = received-back qty.
        var p = Params.New("cid", _user.CompId).Add("from", from).Add("to", to);
        return mode switch
        {
            "Short" => _db.Query(
                @"SELECT * FROM View_ReturnChallanQty
                  WHERE COMPID = @cid AND (GPQTY - RGPQTY) <> 0 AND RecQty > 0
                    AND GpDate BETWEEN @from AND @to ORDER BY GpDate", p),
            "AllReturned" => _db.Query(
                @"SELECT * FROM View_ReturnChallanQty
                  WHERE COMPID = @cid AND (GPQTY - RGPQTY) = 0 AND RecQty > 0
                    AND GpDate BETWEEN @from AND @to ORDER BY GpDate", p),
            _ => _db.Query(
                @"SELECT * FROM view_NotReturnQty
                  WHERE COMPID = @cid AND gpdate BETWEEN @from AND @to ORDER BY GPDate", p),
        };
    }

    // ---- Shipment Challan Auditing (frmRptShipmentChallanAuditing) ----------

    public static readonly IReadOnlyList<ReportMode> ShipmentModes = new List<ReportMode>
    {
        new("InCompany", "In-Company"),
        new("OutCompany", "Out-Company"),
    };

    public DataTable ShipmentAuditing(string mode, string from, string to)
    {
        var view = mode == "OutCompany" ? "VIEW_OutShipmentChallan_Report" : "VIEW_ShipmentChallan_Report";
        return RunView(view, "sdrcompid", from, to);
    }

    // ---- Out-Company User-wise Send (frmrptOutCMPYuserwiseSendChallan) ------

    public DataTable OutCompanyUserSend(int userId, string from, string to)
    {
        var sql =
            @"SELECT * FROM VIEW_OUTCMPYSdr_Challan_Report
              WHERE gpdate BETWEEN @from AND @to AND sdrcompid = @cid
                AND (@uid = 0 OR userid = @uid)
              ORDER BY GPDate, GPNo";
        return _db.Query(sql,
            Params.New("from", from).Add("to", to).Add("cid", _user.CompId).Add("uid", userId));
    }

    // ---- User GP list (frmrptuserInfo) -------------------------------------

    public DataTable UserGpList(int userId, string from, string to)
    {
        var sql =
            @"SELECT * FROM VIEW_GP_NO
              WHERE GPDate BETWEEN @from AND @to AND SdrCompId = @cid
                AND (@uid = 0 OR UserID = @uid)
              ORDER BY GPDate, GPNo";
        return _db.Query(sql,
            Params.New("from", from).Add("to", to).Add("cid", _user.CompId).Add("uid", userId));
    }

    // ---- Department-wise Challan Auditing (frmRptDeptChallanAuditing) -------

    public static readonly IReadOnlyList<ReportMode> DeptModes = new List<ReportMode>
    {
        new("SenderPending", "Sender – Pending"),
        new("ReceiverPending", "Receiver – Pending"),
    };

    public DataTable DeptAuditing(string mode, string dept, string from, string to)
    {
        // Sender modes filter the sender section (SdrSec); receiver modes the receiver section (RcvrSec).
        var (view, compCol, secCol) = mode switch
        {
            "ReceiverPending" => ("VIEW_RcvrPending_Report", "MSID", "RcvrSec"),
            _ => ("VIEWChallanSdrPending", "sdrcompid", "SdrSec"),
        };
        var sql =
            $@"SELECT * FROM {view}
               WHERE gpdate BETWEEN @from AND @to AND {compCol} = @cid
                 AND (@dept = '' OR {secCol} = @dept)
               ORDER BY GPDate, GPNo";
        return _db.Query(sql,
            Params.New("from", from).Add("to", to).Add("cid", _user.CompId).Add("dept", dept ?? ""));
    }

    /// <summary>Generic view runner (SELECT *) — resilient to per-view column differences.
    /// <paramref name="view"/> and <paramref name="compCol"/> come only from the whitelists above.</summary>
    private DataTable RunView(string view, string compCol, string from, string to)
    {
        var sql =
            $@"SELECT * FROM {view}
               WHERE gpdate BETWEEN @from AND @to AND {compCol} = @cid
               ORDER BY GPDate, GPNo";
        return _db.Query(sql, Params.New("from", from).Add("to", to).Add("cid", _user.CompId));
    }

    // ---- Buyer-wise Challan (frmRptbuyerwiseChallan) -----------------------

    public DataTable BuyerWise(string receiverCompName, string mainBuyer, string from, string to)
    {
        var sql =
            @"SELECT SdrCompName, RcvrCompName, GPDate, GPNo, SdrName, BuyerName, SdrEMPCode, SdrDept,
                     SdrSec, RcvrName, RcvrEMPCode, RcvrDept, RcvrSec, itemName, SendQty, PFNO
              FROM View_Buyer_challan_Details
              WHERE SdrCompId = @cid
                AND GPDate BETWEEN @from AND @to
                AND (@rcvr = '' OR RcvrCompName = @rcvr)
                AND (@buyer = '' OR BuyerName = @buyer)
              ORDER BY GPDate, GPNo";
        return _db.Query(sql,
            Params.New("cid", _user.CompId)
                  .Add("from", from).Add("to", to)
                  .Add("rcvr", receiverCompName ?? "")
                  .Add("buyer", mainBuyer ?? ""));
    }
}
