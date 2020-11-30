# GtrackWeb

Web port of the **Gtrack** WinForms gate-pass / challan tracking system.

- **Backend:** ASP.NET Core MVC (net7.0), Razor views, controllers.
- **Frontend:** Razor pages with **React widgets** for the interactive screens
  (Buyer grid, Challan master-detail), built with Vite.
- **Database:** MSSQL, accessed with parameterized ADO.NET queries.

This is **Phase 1 – Foundation + core modules**. It establishes the architecture
and ports Login, Company selection, the Menu, and the Buyer / Out-Company /
Challan-Sender modules end-to-end. The remaining ~55 desktop forms follow the
same patterns (see *Phase 2 backlog*).

---

## Running

### 1. Configure the database
Edit `appsettings.json` → `ConnectionStrings:Gtrack` to point at your SQL Server
instance holding the Gtrack schema:

```json
"ConnectionStrings": {
  "Gtrack": "Data Source=SERVER;Initial Catalog=Gtrack;User ID=sa;Password=***;TrustServerCertificate=True;Encrypt=False"
}
```

### 2. Build the React widgets
```bash
cd ClientApp
npm install
npm run build      # emits bundles into ../wwwroot/js
# npm run dev      # watch mode while developing widgets
```

### 3. Run the app
```bash
dotnet run
```
Browse to the printed URL, log in with a `Sys_User_Name_UP` account, pick a
company + year, and you land on the dashboard.

---

## How the desktop app maps to the web app

| Desktop (WinForms)                     | Web (GtrackWeb)                                             |
|----------------------------------------|------------------------------------------------------------|
| `conn.Mssqlconnect` (static open conn) | `Data/SqlDataAccess.cs` — pooled, **parameterized** queries |
| `Extra.call.EncryptIt/DecryptIt`       | `Helpers/CipherHelper.cs` (identical Caesar cipher)         |
| `Extra.call.UserInfo/CompanyInfo/Year` | `Services/CurrentUser.cs` (claims + session)                |
| `frmlogin`                             | `AccountController.Login` + `Views/Account/Login.cshtml`    |
| `frmchangepassword`                    | `AccountController.ChangePassword`                          |
| `frmsplash` (company/year select)      | `AccountController.SelectCompany`                           |
| `frmmenu`                              | `HomeController.Index` + `Services/MenuProvider.cs`         |
| `form.Find` (lookups)                  | `Services/LookupService.cs`                                 |
| `frmBuyer`                             | `BuyerController` + React `BuyerApp` widget                 |
| `frmAddCompany`                        | `OutCompanyController` + Razor CRUD                         |
| `frmChallanSender`                     | `ChallanController.Sender` + React `ChallanSenderApp` widget |
| `frmChallanSender` (edit/delete)       | `ChallanController.Edit` (line edit, line delete, whole-challan soft-delete) |
| `frmChallan_Sender_Gate`               | `ChallanController.SenderGate` (check-out, SGsign=1)        |
| `frmChallan_Receiver_Gate`             | `ChallanController.ReceiverGate` (check-in, RGsign=1)       |
| `frmChallanReceiver`                   | `ChallanController.Receiver` (confirm receipt, DeleteRowR=1)|
| `frmReturnChallanSender`               | `ReturnChallanController.Sender` (Return_* tables, rowslno) |
| `frmReturn_Challan_Sender_Gate`        | `ReturnChallanController.SenderGate`                        |
| `frmReturn_Challan_Receiver_Gate`      | `ReturnChallanController.ReceiverGate`                      |
| `frmReturnChallanReceiver`             | `ReturnChallanController.Receiver`                          |
| `frmReturnChallanSender` (edit/delete) | `ReturnChallanController.Edit` (per-installment line edit/delete + delete installment) |
| `frmOutCompanyChallanSender`           | `OutCompanyChallanController.Sender` (OutCmpy_* tables)     |
| `frmOutCompanyChallan_Sender_Gate`     | `OutCompanyChallanController.SenderGate`                    |
| `frmOutCmpChallanReceiver`             | `OutCompanyChallanController.Receiver`                      |
| `frmOutCompanyChallanSender` (edit/del)| `OutCompanyChallanController.Edit` (line edit/delete, whole-challan soft-delete) |
| `frmRptChallanAuditing`                | `ReportController.ChallanAuditing` (SQL views + CSV)       |
| `frmRptGateChallanAuditing`            | `ReportController.GateAuditing`                            |
| `frmRptbuyerwiseChallan`               | `ReportController.BuyerWise`                               |
| `frmIteamGroup`                        | `ItemGroupController` + Razor CRUD                          |
| `frmNewIteam`                          | `ItemController` + Razor CRUD                               |
| `frmPurpuse`                           | `PurposeController` + Razor CRUD                            |
| `frmDriver`                            | `DriverController` + Razor CRUD                             |
| `frmVehicel`                           | `VehicleController` + Razor CRUD                            |
| `frmMerchandiser`                      | `MerchandiserController` + Razor CRUD                       |
| `frmNewpfnoauto`                       | `PfNoController` + Razor CRUD (auto PF#n, transactional)    |
| `frm_user_information`                 | `UserController` (Sys_User_Name_UP; no legacy menu-perms)  |
| `frmEmpSearching`                      | `EmployeeController` (read-only InfoEmp browse)            |
| `frmBookRange`                         | `BookRangeController` (clean BookRange CRUD)               |
| `frmNewMail`                           | `MailController` (New_mail; empcode validated)            |
| `frmRptDeptChallanAuditing`            | `ReportController.DeptAuditing`                            |

### Notable improvements over the desktop version
- **SQL injection closed:** every value is passed as a `SqlParameter` instead of
  being concatenated into the query string.
- **Transactions:** a challan save (NEW_GP + Challan_Main + Challan_Sub + audit +
  gate rows) commits or rolls back as a unit (`ISqlDataAccess.Transaction`).
- **Auth enforced globally:** a fallback authorization policy blocks every
  controller until login; `[RequireCompany]` enforces company selection.

### Behaviour preserved intentionally
- Password cipher is the legacy Caesar shift (shift 11) so existing
  `Sys_User_Name_UP.UserPWord` values keep working with **no data migration**.
  Active users are `YsnActive = 0`. See *Security* below for the hardening path.
- Soft deletes (`Sign` / `DeleteRow`) and GP numbering
  (`ComShortName + CompID + {5-digit seq} + "-" + Year`) match the desktop.

---

## Schema assumptions (verify against your live DB)
Built from the SQL in the desktop forms. Confirm these column names exist:
`New_Buyer(BuyerID, BuyerName, MainBuyerName, Sign)`,
`Out_Company_Information(CompID, CompName, CompAdd, DeleteRow, UserID, PCName, EntryDate, EntryTime)`,
`Company_Information(CompID, CompName, CompAdd, ComShortName)`,
`Sys_User_Name_UP(UserID, UserName, UserPWord, YsnActive)`,
`NEW_GP`, `Challan_Main`, `Challan_Sub`, `Challan_Sub_Del_Edit`,
`Sender_Gate`, `Receiver_Gate`, `New_Item_Name`, `new_Item_Type`,
`New_PFNo(PFID, PFNo, BuyerID)`, `new_purpose`, `InfoDriver`, `InfoEmp(EMPID, EmpName, …)`.
`InfoEmp.EmpName` in particular should be checked — the desktop mostly used the
`EmpId` code field.

---

## Security (recommended follow-up)
The legacy Caesar cipher is **not** real cryptography; it is kept only for
drop-in compatibility. Recommended hardening: on next login, verify with the
legacy cipher, then re-hash the password with ASP.NET Core
`PasswordHasher<T>` (PBKDF2) and store it in a new column, migrating users
transparently.

---

## Phase 2 backlog (same patterns, not yet ported)
- **Master data:** ✅ The Setup group is complete — Buyer, Out Company, Item Group,
  New Item, Driver, Vehicle, Purpose, Merchandiser, PF No, User, Employee (read-only
  HR browse), Book Range, Mail Id. Not ported: the legacy per-user menu-permission
  system (the web grants the full menu after login).
- **Challan flow:** ✅ In-company (Sender → Sender Gate → Receiver Gate → Receiver,
  plus **Edit/Delete** of a saved challan — line edits, line delete, and whole-challan
  soft-delete cascade; a received challan is locked),
  ✅ Return challan (sender / gates / receiver / edit-delete per installment), and ✅ Out-Company challan
  (sender → sender gate → receiver, `OutCmpy_*` tables) are all ported.
  The challan domain is complete. Simplifications: the return header is copied
  from the original challan and return gate/receive act on the latest installment
  (rowslno); out-company EMB/PACKEGING pallet numbering, MailSender rows, and the
  inner/outer employee-source distinction (OptSign defaults to 'OUT') are deferred.
- **Challan Sender extras** deferred from Phase 1: EMBOYDARY / PACKEGING pallet
  numbering, MailSender notification rows, and the full multi-employee
  sender/receiver/carrier/authorizer capture (currently a focused subset).
- **Reports:** ✅ A reporting infrastructure is in place (filter form → results
  table → CSV export) over the desktop's SQL Server views. Ported: Challan
  Auditing, Gate Auditing, Return Challan Auditing, Out-Company Challan Auditing,
  Company-wise, Department-wise, User-wise, User GP List, Shipment Auditing,
  Out-Company User-wise Send, Returnable Challan Qty, Short/Excess Summary, and
  Buyer-wise (13 reports). The desktop's `frmRptGpEditDeleteInfo` is intentionally
  not ported — it just re-reads the return-sender-pending view into a temp table,
  duplicating the Return Auditing → Sender Pending report. All reports follow the
  same pattern —
  each maps to a SQL view + a date/company filter; add a `ReportService` method
  and a controller action. PDF export and the exact Crystal Report layouts are not
  reproduced (CSV export is provided). Department-wise reports depend on the not-yet
  ported Employee/department sub-system.
