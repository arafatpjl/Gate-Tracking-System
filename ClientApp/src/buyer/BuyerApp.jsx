import React, { useEffect, useState, useCallback } from 'react';
import { getJson, postJson } from '../shared/api.js';

// Web port of frmBuyer: list/create/update/delete buyers (New_Buyer).
export default function BuyerApp({ apiBase }) {
  const [rows, setRows] = useState([]);
  const [mainBuyers, setMainBuyers] = useState([]);
  const [form, setForm] = useState({ buyerId: 0, mainBuyerName: '', buyerName: '' });
  const [message, setMessage] = useState(null);
  const [loading, setLoading] = useState(false);

  const reload = useCallback(async () => {
    setLoading(true);
    try {
      const [list, mains] = await Promise.all([
        getJson(`${apiBase}/List`),
        getJson(`${apiBase}/MainBuyers`),
      ]);
      setRows(list);
      setMainBuyers(mains.map((m) => m.text));
    } finally {
      setLoading(false);
    }
  }, [apiBase]);

  useEffect(() => { reload(); }, [reload]);

  const editing = form.buyerId > 0;

  function resetForm() {
    setForm({ buyerId: 0, mainBuyerName: '', buyerName: '' });
  }

  async function save(e) {
    e.preventDefault();
    const url = editing ? `${apiBase}/Update` : `${apiBase}/Create`;
    const res = await postJson(url, form);
    setMessage({ ok: res.ok, text: res.message });
    if (res.ok) {
      resetForm();
      reload();
    }
  }

  async function remove(row) {
    if (!confirm(`Delete buyer "${row.buyerName}"?`)) return;
    const res = await postJson(`${apiBase}/Delete`, { buyerId: row.buyerId });
    setMessage({ ok: res.ok, text: res.message });
    if (res.ok) reload();
  }

  function edit(row) {
    setForm({ buyerId: row.buyerId, mainBuyerName: row.mainBuyerName, buyerName: row.buyerName });
    setMessage(null);
  }

  return (
    <div className="row">
      <div className="col-md-4">
        <div className="card shadow-sm mb-3">
          <div className="card-header">{editing ? 'Edit Buyer' : 'New Buyer'}</div>
          <div className="card-body">
            {message && (
              <div className={`alert ${message.ok ? 'alert-success' : 'alert-danger'} py-2`}>
                {message.text}
              </div>
            )}
            <form onSubmit={save}>
              <div className="mb-2">
                <label className="form-label">Main Buyer Name</label>
                <input
                  className="form-control"
                  list="main-buyer-list"
                  value={form.mainBuyerName}
                  onChange={(e) => setForm({ ...form, mainBuyerName: e.target.value })}
                  required
                />
                <datalist id="main-buyer-list">
                  {mainBuyers.map((m) => <option key={m} value={m} />)}
                </datalist>
              </div>
              <div className="mb-2">
                <label className="form-label">Buyer Name</label>
                <input
                  className="form-control text-uppercase"
                  value={form.buyerName}
                  onChange={(e) => setForm({ ...form, buyerName: e.target.value })}
                  required
                />
              </div>
              <button type="submit" className="btn btn-primary">
                {editing ? 'Update' : 'Save'}
              </button>
              {editing && (
                <button type="button" className="btn btn-secondary ms-2" onClick={resetForm}>
                  Cancel
                </button>
              )}
            </form>
          </div>
        </div>
      </div>

      <div className="col-md-8">
        {loading ? (
          <div className="text-muted">Loading…</div>
        ) : (
          <table className="table table-striped table-bordered">
            <thead className="table-dark">
              <tr><th>Main Buyer</th><th>Buyer</th><th style={{ width: 160 }}>Action</th></tr>
            </thead>
            <tbody>
              {rows.map((r) => (
                <tr key={r.buyerId}>
                  <td>{r.mainBuyerName}</td>
                  <td>{r.buyerName}</td>
                  <td>
                    <button className="btn btn-sm btn-outline-primary me-1" onClick={() => edit(r)}>Edit</button>
                    <button className="btn btn-sm btn-outline-danger" onClick={() => remove(r)}>Delete</button>
                  </td>
                </tr>
              ))}
              {rows.length === 0 && (
                <tr><td colSpan={3} className="text-center text-muted">No buyers yet.</td></tr>
              )}
            </tbody>
          </table>
        )}
      </div>
    </div>
  );
}
