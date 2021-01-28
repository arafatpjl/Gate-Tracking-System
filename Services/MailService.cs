using System.Data;
using GtrackWeb.Data;
using GtrackWeb.Helpers;
using GtrackWeb.Models;

namespace GtrackWeb.Services;

/// <summary>
/// Employee mail-id maintenance (frmNewMail). Stored in <c>New_mail</c>. The
/// employee code must exist in <c>InfoEmp</c> for the operating company, and a
/// (empcode, mailId) pair cannot be duplicated.
/// </summary>
public sealed class MailService
{
    private readonly ISqlDataAccess _db;
    private readonly CurrentUser _user;

    public MailService(ISqlDataAccess db, CurrentUser user)
    {
        _db = db;
        _user = user;
    }

    public List<MailRow> List()
    {
        var dt = _db.Query(
            @"SELECT ISNULL(CompName,'') AS CompName, ISNULL(Empcode,'') AS Empcode, ISNULL(MailId,'') AS MailId
              FROM New_mail WHERE CompID = @c ORDER BY Empcode",
            Params.New("c", _user.CompId));
        return dt.AsEnumerable().Select(r => new MailRow(
            r["CompName"].ToString() ?? "", r["Empcode"].ToString() ?? "", r["MailId"].ToString() ?? "")).ToList();
    }

    public OpResult Create(string empCode, string mailId)
    {
        empCode = (empCode ?? "").Trim();
        mailId = (mailId ?? "").Trim();
        if (string.IsNullOrEmpty(empCode)) return new OpResult(false, "Type Employee Code");
        if (string.IsNullOrEmpty(mailId)) return new OpResult(false, "Type Mail Id");

        var empExists = _db.Scalar(
            "SELECT 1 FROM InfoEmp WHERE empcode = @e AND CompId = @c",
            Params.New("e", empCode).Add("c", _user.CompId));
        if (empExists == null) return new OpResult(false, "Employeecode Not Exist");

        var dup = _db.Scalar(
            "SELECT 1 FROM New_mail WHERE empcode = @e AND MailId = @m",
            Params.New("e", empCode).Add("m", mailId));
        if (dup != null) return new OpResult(false, "MailId Already Exist");

        _db.Execute(
            @"INSERT INTO New_mail (CompID, CompName, Empcode, MailId, EntryDate, EntryTime, UserID, PCName)
              VALUES (@c, @cn, @e, @m, @date, @time, @uid, @pc)",
            Params.New("c", _user.CompId).Add("cn", _user.CompName).Add("e", empCode).Add("m", mailId)
                  .Add("date", DateTime.Now.ToString("dd-MMM-yyyy")).Add("time", DateTime.Now.ToString("hh:mm:ss"))
                  .Add("uid", _user.UserId).Add("pc", _user.PcName));

        return new OpResult(true, "Data Saved Successfully");
    }

    public OpResult Delete(string empCode, string mailId)
    {
        _db.Execute("DELETE FROM New_mail WHERE empcode = @e AND MailId = @m AND CompID = @c",
            Params.New("e", empCode).Add("m", mailId).Add("c", _user.CompId));
        return new OpResult(true, "Data Delete Successfully");
    }
}
