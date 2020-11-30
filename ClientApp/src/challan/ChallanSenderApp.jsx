import React, { useEffect, useMemo, useState } from 'react';
import { getJson, postJson } from '../shared/api.js';

const emptyLine = () => ({
  itemGroup: '', itemName: '', pfNo: '', description: '', unit: '', quantity: '', remarks: '',
});

const CHALLAN_TYPES = [
  { value: 1, text: 'Shipment' },
  { value: 2, text: 'Garments' },
  { value: 3, text: 'Other Goods' },
];

// Web port of frmChallanSender header + line-item grid.
export default function ChallanSenderApp({ lookupsUrl, saveUrl, listUrl }) {
  const [lookups, setLookups] = useState(null);
  const [header, setHeader] = useState({
    gpDate: new Date().toISOString().slice(0, 10),
    itemGroupName: '',
    receiverCompId: '',
    senderEmpId: '',
    driverId: '',
    purposeId: '',
    vehicleNo: '',
    challanType: 1,
    returnable: false,
    returnDate: '',
  });
  const [lines, setLines] = useState([emptyLine()]);
  const [message, setMessage] = useState(null);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    getJson(lookupsUrl).then(setLookups).catch((e) => setMessage({ ok: false, text: e.message }));
  }, [lookupsUrl]);

  const options = useMemo(() => lookups || {
    itemGroups: [], items: [], pfNos: [], receiverCompanies: [], drivers: [], purposes: [], employees: [],
  }, [lookups]);

  function setLine(idx, patch) {
    setLines((cur) => cur.map((l, i) => (i === idx ? { ...l, ...patch } : l)));
  }
  const addLine = () => setLines((cur) => [...cur, emptyLine()]);
  const removeLine = (idx) => setLines((cur) => cur.filter((_, i) => i !== idx));

  async function save(e) {
    e.preventDefault();
    setSaving(true);
    setMessage(null);
    try {
      const payload = {
        ...header,
        receiverCompId: Number(header.receiverCompId) || 0,
        senderEmpId: Number(header.senderEmpId) || 0,
        driverId: Number(header.driverId) || 0,
        purposeId: Number(header.purposeId) || 0,
        challanType: Number(header.challanType) || 1,
        lines: lines
          .filter((l) => l.itemName && l.quantity)
          .map((l) => ({ ...l, quantity: Number(l.quantity) || 0 })),
      };
      if (payload.lines.length === 0) {
        setMessage({ ok: false, text: 'Add at least one item line' });
        return;
      }
      const res = await postJson(saveUrl, payload);
      setMessage({ ok: res.ok, text: res.ok ? `${res.message} (GP No: ${res.gpNo})` : res.message });
      if (res.ok) {
        setLines([emptyLine()]);
      }
    } catch (err) {
      setMessage({ ok: false, text: err.message });
    } finally {
      setSaving(false);
    }
  }

  const Sel = ({ value, onChange, items, placeholder }) => (
    <select className="form-select form-select-sm" value={value} onChange={onChange}>
      <option value="">{placeholder}</option>
      {items.map((o) => <option key={o.value} value={o.value}>{o.text}</option>)}
    </select>
  );

  return (
    <form onSubmit={save}>
      {message && (
        <div className={`alert ${message.ok ? 'alert-success' : 'alert-danger'}`}>{message.text}</div>
      )}

      <div className="card shadow-sm mb-3">
        <div className="card-header">Challan Header</div>
        <div className="card-body">
          <div className="row g-2">
            <div className="col-md-3">
              <label className="form-label">GP Date</label>
              <input type="date" className="form-control form-control-sm"
                     value={header.gpDate}
                     onChange={(e) => setHeader({ ...header, gpDate: e.target.value })} />
            </div>
            <div className="col-md-3">
              <label className="form-label">Item Group</label>
              <Sel value={header.itemGroupName} placeholder="-- group --" items={options.itemGroups}
                   onChange={(e) => setHeader({ ...header, itemGroupName: e.target.value })} />
            </div>
            <div className="col-md-3">
              <label className="form-label">Receiving Company</label>
              <Sel value={header.receiverCompId} placeholder="-- company --" items={options.receiverCompanies}
                   onChange={(e) => setHeader({ ...header, receiverCompId: e.target.value })} />
            </div>
            <div className="col-md-3">
              <label className="form-label">Sender Employee</label>
              <Sel value={header.senderEmpId} placeholder="-- employee --" items={options.employees}
                   onChange={(e) => setHeader({ ...header, senderEmpId: e.target.value })} />
            </div>
            <div className="col-md-3">
              <label className="form-label">Driver</label>
              <Sel value={header.driverId} placeholder="-- driver --" items={options.drivers}
                   onChange={(e) => setHeader({ ...header, driverId: e.target.value })} />
            </div>
            <div className="col-md-3">
              <label className="form-label">Purpose</label>
              <Sel value={header.purposeId} placeholder="-- purpose --" items={options.purposes}
                   onChange={(e) => setHeader({ ...header, purposeId: e.target.value })} />
            </div>
            <div className="col-md-3">
              <label className="form-label">Vehicle No</label>
              <input className="form-control form-control-sm" value={header.vehicleNo}
                     onChange={(e) => setHeader({ ...header, vehicleNo: e.target.value })} />
            </div>
            <div className="col-md-3">
              <label className="form-label">Challan Type</label>
              <Sel value={header.challanType} placeholder="" items={CHALLAN_TYPES}
                   onChange={(e) => setHeader({ ...header, challanType: e.target.value })} />
            </div>
            <div className="col-md-3 d-flex align-items-end">
              <div className="form-check">
                <input className="form-check-input" type="checkbox" id="returnable"
                       checked={header.returnable}
                       onChange={(e) => setHeader({ ...header, returnable: e.target.checked })} />
                <label className="form-check-label" htmlFor="returnable">Returnable</label>
              </div>
            </div>
            {header.returnable && (
              <div className="col-md-3">
                <label className="form-label">Return Date</label>
                <input type="date" className="form-control form-control-sm" value={header.returnDate}
                       onChange={(e) => setHeader({ ...header, returnDate: e.target.value })} />
              </div>
            )}
          </div>
        </div>
      </div>

      <div className="card shadow-sm mb-3">
        <div className="card-header d-flex justify-content-between align-items-center">
          <span>Line Items</span>
          <button type="button" className="btn btn-sm btn-outline-primary" onClick={addLine}>+ Add Row</button>
        </div>
        <div className="table-responsive">
          <table className="table table-sm table-bordered mb-0">
            <thead className="table-light">
              <tr>
                <th style={{ width: 40 }}>#</th>
                <th>Group</th><th>Item</th><th>PF No</th><th>Description</th>
                <th>Unit</th><th style={{ width: 90 }}>Qty</th><th>Remarks</th><th></th>
              </tr>
            </thead>
            <tbody>
              {lines.map((l, i) => (
                <tr key={i}>
                  <td>{i + 1}</td>
                  <td><Sel value={l.itemGroup} placeholder="" items={options.itemGroups}
                           onChange={(e) => setLine(i, { itemGroup: e.target.value })} /></td>
                  <td><Sel value={l.itemName} placeholder="" items={options.items.map((x) => ({ value: x.text, text: x.text }))}
                           onChange={(e) => setLine(i, { itemName: e.target.value })} /></td>
                  <td><Sel value={l.pfNo} placeholder="" items={options.pfNos.map((x) => ({ value: x.text, text: x.text }))}
                           onChange={(e) => setLine(i, { pfNo: e.target.value })} /></td>
                  <td><input className="form-control form-control-sm" value={l.description}
                             onChange={(e) => setLine(i, { description: e.target.value })} /></td>
                  <td><input className="form-control form-control-sm" value={l.unit}
                             onChange={(e) => setLine(i, { unit: e.target.value })} /></td>
                  <td><input type="number" className="form-control form-control-sm" value={l.quantity}
                             onChange={(e) => setLine(i, { quantity: e.target.value })} /></td>
                  <td><input className="form-control form-control-sm" value={l.remarks}
                             onChange={(e) => setLine(i, { remarks: e.target.value })} /></td>
                  <td>
                    <button type="button" className="btn btn-sm btn-outline-danger"
                            onClick={() => removeLine(i)} disabled={lines.length === 1}>×</button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      <div className="d-flex gap-2">
        <button type="submit" className="btn btn-success" disabled={saving}>
          {saving ? 'Saving…' : 'Save Challan'}
        </button>
        <a className="btn btn-outline-secondary" href={listUrl}>Back to List</a>
      </div>
    </form>
  );
}
