namespace GtrackWeb.Models;

/// <summary>Shared outcome of a create/update/delete operation.</summary>
public sealed record OpResult(bool Ok, string Message);

/// <summary>Row from <c>new_Item_Type</c> (item groups).</summary>
public sealed record ItemGroupRow(int Id, string ItemType);

/// <summary>Row from <c>New_Item_Name</c> (items).</summary>
public sealed record ItemRow(int ItemId, string ItemName, string ItemType);

/// <summary>Row from <c>new_purpose</c>.</summary>
public sealed record PurposeRow(int Pid, string Purpose);

/// <summary>Row from <c>InfoDriver</c>.</summary>
public sealed record DriverRow(int Did, string Name, string Licence);

/// <summary>Row from <c>INFOVehicle</c> (vehicle number is the key).</summary>
public sealed record VehicleRow(string VehicleNo);

/// <summary>Row from <c>New_Merchandiser</c> (merchandiser name is the key; active = SIGN 0).</summary>
public sealed record MerchandiserRow(string Merchandiser);

/// <summary>Row from <c>New_PFNo</c> (PF / file numbers, auto-numbered PF#n).</summary>
public sealed record PfNoRow(
    string PfNo, string CrDate, string Qty, string BuyerName, string MainBuyerName,
    string Ref, string Merchandiser, string Description);

/// <summary>Row from <c>Sys_User_Name_UP</c> (login users; active = YsnActive 0).</summary>
public sealed record UserRow(int UserId, string UserName, bool Active);

/// <summary>Row from <c>InfoEmp</c> shown in the employee browse.</summary>
public sealed record EmployeeRow(string EmpCode, string EmpName, string Department, string Section);

/// <summary>Row from <c>BookRange</c> (challan book number ranges per department).</summary>
public sealed record BookRangeRow(string DeptName, string SubDeptName, string StartNo, string EndNo);

/// <summary>Row from <c>New_mail</c> (employee mail ids).</summary>
public sealed record MailRow(string CompName, string EmpCode, string MailId);
