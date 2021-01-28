namespace GtrackWeb.Services;

/// <summary>A clickable menu entry. <see cref="Enabled"/> = false means the
/// desktop form exists but has not been ported yet (Phase 2 placeholder).</summary>
public sealed record MenuEntry(string Text, string? Controller = null, string? Action = null, bool Enabled = true);

public sealed record MenuGroup(string Title, IReadOnlyList<MenuEntry> Items);

/// <summary>
/// Builds the navigation tree, mirroring the desktop <c>form.frmmenu</c> menu
/// bar. Ported modules link to their controllers; the rest are shown disabled
/// so the full application map stays visible during the incremental port.
/// </summary>
public static class MenuProvider
{
    public static IReadOnlyList<MenuGroup> Build() => new List<MenuGroup>
    {
        new("Setup", new List<MenuEntry>
        {
            new("Item Group", "ItemGroup", "Index"),            // frmIteamGroup ✓
            new("New Item", "Item", "Index"),                   // frmNewIteam ✓
            new("File Type (PF No)", "PfNo", "Index"),          // frmNewpfnoauto ✓
            new("Buyer", "Buyer", "Index"),                     // frmBuyer  ✓
            new("Merchandiser", "Merchandiser", "Index"),       // frmMerchandiser ✓
            new("Employee", "Employee", "Index"),               // frmEmpSearching ✓
            new("Driver", "Driver", "Index"),                   // frmDriver ✓
            new("Vehicle", "Vehicle", "Index"),                 // frmVehicel ✓
            new("Purpose", "Purpose", "Index"),                 // frmPurpuse ✓
            new("Book Range", "BookRange", "Index"),            // frmBookRange ✓
            new("Out Company", "OutCompany", "Index"),          // frmAddCompany ✓
            new("Mail Id", "Mail", "Index"),                    // frmNewMail ✓
            new("User", "User", "Index"),                       // frm_user_information ✓
        }),
        new("Challan", new List<MenuEntry>
        {
            new("Challan Sender", "Challan", "Sender"),         // frmChallanSender ✓
            new("Challan List", "Challan", "Index"),
            new("Edit / Delete Challan", "Challan", "Edit"),    // frmChallanSender edit/delete ✓
            new("Challan Sender Gate", "Challan", "SenderGate"),        // frmChallan_Sender_Gate ✓
            new("Challan Receiver Gate", "Challan", "ReceiverGate"),    // frmChallan_Receiver_Gate ✓
            new("Challan Receiver", "Challan", "Receiver"),            // frmChallanReceiver ✓
        }),
        new("Return Challan", new List<MenuEntry>
        {
            new("Return Challan Sender", "ReturnChallan", "Sender"),          // frmReturnChallanSender ✓
            new("Return Sender Gate", "ReturnChallan", "SenderGate"),         // frmReturn_Challan_Sender_Gate ✓
            new("Return Receiver Gate", "ReturnChallan", "ReceiverGate"),     // frmReturn_Challan_Receiver_Gate ✓
            new("Return Challan Receiver", "ReturnChallan", "Receiver"),      // frmReturnChallanReceiver ✓
            new("Return Edit / Delete", "ReturnChallan", "Edit"),            // frmReturnChallanSender edit/delete ✓
        }),
        new("Out Company Challan", new List<MenuEntry>
        {
            new("Out Company Challan Sender", "OutCompanyChallan", "Sender"),   // frmOutCompanyChallanSender ✓
            new("Out Company Challan List", "OutCompanyChallan", "Index"),
            new("Out Company Edit / Delete", "OutCompanyChallan", "Edit"),
            new("Out Company Sender Gate", "OutCompanyChallan", "SenderGate"),  // frmOutCompanyChallan_Sender_Gate ✓
            new("Out Company Receiver", "OutCompanyChallan", "Receiver"),       // frmOutCmpChallanReceiver ✓
        }),
        new("Reports", new List<MenuEntry>
        {
            new("Challan Auditing", "Report", "ChallanAuditing"),     // frmRptChallanAuditing ✓
            new("Gate Challan Auditing", "Report", "GateAuditing"),   // frmRptGateChallanAuditing ✓
            new("Buyer Wise Challan", "Report", "BuyerWise"),         // frmRptbuyerwiseChallan ✓
            new("Company Wise Challan", "Report", "CompanyWise"),     // frmRptallcompanychallan ✓
            new("Return Challan Auditing", "Report", "ReturnAuditing"),        // frmRptReturnChallanAuditing ✓
            new("Out-Company Challan Auditing", "Report", "OutCompanyAuditing"), // frmRptOutCmpyChallanAuditing ✓
            new("Department Wise Challan", "Report", "DeptAuditing"),  // frmRptDeptChallanAuditing ✓
            new("User Wise Challan", "Report", "UserWise"),           // frmrptuserwiseInfo/receiver ✓
            new("User GP List", "Report", "UserGpList"),              // frmrptuserInfo ✓
            new("Shipment Challan Auditing", "Report", "ShipmentAuditing"),   // frmRptShipmentChallanAuditing ✓
            new("Out-Company User-wise Send", "Report", "OutCompanyUserSend"), // frmrptOutCMPYuserwiseSendChallan ✓
            new("Returnable Challan Qty", "Report", "ReturnableQty"), // frmReturnableChallanQtyreport ✓
            new("Short / Excess Summary", "Report", "ShortExcess"),  // frmrptShortExInfo ✓
        }),
    };
}
