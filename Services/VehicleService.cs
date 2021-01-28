using System.Data;
using GtrackWeb.Data;
using GtrackWeb.Helpers;
using GtrackWeb.Models;

namespace GtrackWeb.Services;

/// <summary>
/// Port of <c>form.frmVehicel</c>. Vehicles live in <c>INFOVehicle</c>
/// (soft-deleted via <c>sign</c>); the vehicle number itself is the key.
/// A vehicle referenced by <c>Challan_Main.Vehicleno</c> cannot be deleted.
/// </summary>
public sealed class VehicleService
{
    private readonly ISqlDataAccess _db;
    private readonly CurrentUser _user;

    public VehicleService(ISqlDataAccess db, CurrentUser user)
    {
        _db = db;
        _user = user;
    }

    public List<VehicleRow> List()
    {
        var dt = _db.Query(
            "SELECT Vehicleno FROM INFOVehicle WHERE sign = 1 ORDER BY Vehicleno");
        return dt.AsEnumerable().Select(r => new VehicleRow(r["Vehicleno"].ToString() ?? "")).ToList();
    }

    public OpResult Create(string vehicleNo)
    {
        vehicleNo = (vehicleNo ?? "").Trim();
        if (string.IsNullOrEmpty(vehicleNo)) return new OpResult(false, "Type Vehicle No");

        if (_db.Scalar("SELECT 1 FROM INFOVehicle WHERE Vehicleno = @v AND sign = 1",
                Params.New("v", vehicleNo)) != null)
            return new OpResult(false, "Duplicate Type of Vehicleno");

        _db.Execute(
            @"INSERT INTO INFOVehicle (Vehicleno, sign, UserID, PCName, EntryDate, EntryTime)
              VALUES (@v, 1, @uid, @pc,
                      CONVERT(DATETIME, FLOOR(CONVERT(FLOAT, GETDATE()))), CONVERT(VARCHAR(8), GETDATE(), 108))",
            Params.New("v", vehicleNo).Add("uid", _user.UserId).Add("pc", _user.PcName));

        return new OpResult(true, "Data Saved Successfully");
    }

    public OpResult Update(string oldVehicleNo, string newVehicleNo)
    {
        oldVehicleNo = (oldVehicleNo ?? "").Trim();
        newVehicleNo = (newVehicleNo ?? "").Trim();
        if (string.IsNullOrEmpty(oldVehicleNo)) return new OpResult(false, "Select a vehicle");
        if (string.IsNullOrEmpty(newVehicleNo)) return new OpResult(false, "Type Vehicle No");

        if (InUse(oldVehicleNo))
            return new OpResult(false, "Vehicleno Exist In Challan. Data can not Update");

        _db.Execute(
            "UPDATE INFOVehicle SET Vehicleno = @new, UserID = @uid, PCName = @pc WHERE Vehicleno = @old AND sign = 1",
            Params.New("new", newVehicleNo).Add("uid", _user.UserId).Add("pc", _user.PcName).Add("old", oldVehicleNo));

        return new OpResult(true, "Data Update Successfully");
    }

    public OpResult Delete(string vehicleNo)
    {
        vehicleNo = (vehicleNo ?? "").Trim();
        if (string.IsNullOrEmpty(vehicleNo)) return new OpResult(false, "Select a vehicle");
        if (InUse(vehicleNo))
            return new OpResult(false, "Vehicleno Exist In Challan. Data can not delete");

        _db.Execute("UPDATE INFOVehicle SET sign = 0 WHERE Vehicleno = @v", Params.New("v", vehicleNo));
        return new OpResult(true, "Data Delete Successfully");
    }

    private bool InUse(string vehicleNo) =>
        _db.Scalar("SELECT 1 FROM Challan_Main WHERE Vehicleno = @v", Params.New("v", vehicleNo)) != null;
}
