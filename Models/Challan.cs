namespace GtrackWeb.Models;

/// <summary>One line item of a challan (a <c>Challan_Sub</c> row).</summary>
public sealed class ChallanLine
{
    public int Sl { get; set; }
    public string ItemGroup { get; set; } = "";   // Challan_Sub.ittype
    public string ItemName { get; set; } = "";     // resolved -> New_Item_Name.itemID
    public string PfNo { get; set; } = "";         // resolved -> New_PFNo.PFID (+ buyer)
    public string Description { get; set; } = "";  // ItDesc
    public string Unit { get; set; } = "";         // QtyUnit
    public decimal Quantity { get; set; }          // GPQty
    public string Remarks { get; set; } = "";
}

/// <summary>Header + lines captured by the Challan Sender screen (frmChallanSender).</summary>
public sealed class ChallanSenderInput
{
    // Header (Challan_Main)
    public string GpDate { get; set; } = DateTime.Now.ToString("dd-MMM-yyyy");
    public string ItemGroupName { get; set; } = "";  // NEW_GP.GroupName / cboItemType
    public int ReceiverCompId { get; set; }           // MSID (other company)
    public int SenderEmpId { get; set; }              // EMPID
    public int ReceiverEmpId { get; set; }            // ReceiverId (optional)
    public int CarrierEmpId { get; set; }             // CReceiverId (optional)
    public int DriverId { get; set; }
    public int AuthId { get; set; }
    public int PurposeId { get; set; }
    public string VehicleNo { get; set; } = "";
    public int ChallanType { get; set; } = 1;         // 1 Shipment, 2 Garments, 3 Other Goods
    public bool Returnable { get; set; }
    public string ReturnDate { get; set; } = "";

    public List<ChallanLine> Lines { get; set; } = new();
}

/// <summary>A saved challan header as shown in the challan list.</summary>
public sealed record ChallanListRow(int GpId, string GpNo, string GpDate, string GroupName);

/// <summary>Header + gate/receive status of an existing challan, looked up by GP No.</summary>
public sealed record ChallanHeaderInfo(
    int GpId, string GpNo, string GpDate, string GroupName,
    bool SenderGateOk, bool ReceiverGateOk, bool Received);

/// <summary>A challan line as shown on the receiver screen (with received quantity).</summary>
public sealed class ChallanLineView
{
    public int Sl { get; set; }
    public string ItemGroup { get; set; } = "";
    public string ItemName { get; set; } = "";
    public string PfNo { get; set; } = "";
    public string Description { get; set; } = "";
    public string Unit { get; set; } = "";
    public decimal GpQty { get; set; }
    public decimal RecQty { get; set; }
    public string Remarks { get; set; } = "";
}

/// <summary>Posted by the receiver screen: received quantity + remark per line.</summary>
public sealed class ReceiveLineInput
{
    public int Sl { get; set; }
    public decimal RecQty { get; set; }
    public string Remarks { get; set; } = "";
}

/// <summary>Status of a return installment (Return_* tables, keyed by GPID + RowSl).</summary>
public sealed record ReturnStatusInfo(
    int GpId, string GpNo, int RowSl, string GpDate,
    bool SenderGateOk, bool ReceiverGateOk, bool Received);
