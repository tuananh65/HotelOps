import { useState } from "react";
import { api, message, type Row } from "./api";
import { AlertPopup } from "./alert-popup";
import { Dialog } from "./ui";

export function StocktakeItems({ doc, items, existing, close, saved }: { doc: Row; items: Row[]; existing: Row[]; close: () => void; saved: () => void }) {
  const [selected, setSelected] = useState<number[]>([]), [search, setSearch] = useState("");
  const [pending, setPending] = useState(false), [error, setError] = useState("");
  const choices = items.filter(i => !existing.some(l => l.itemId === i.itemId) && `${i.name} ${i.code}`.toLocaleLowerCase("vi").includes(search.toLocaleLowerCase("vi")));
  return <Dialog title="Chọn vật tư kiểm kê" close={() => !pending && close()}><form onSubmit={async e => {
    e.preventDefault(); if (pending || !selected.length) return;
    setPending(true); setError("");
    try { await api(`documents/${doc.documentId}/stocktake-items`, { rowVersion: doc.rowVersion, itemIds: selected }); saved(); }
    catch (e) { setError(message(e)); } finally { setPending(false); }
  }}><label>Tìm vật tư<input value={search} onChange={e => setSearch(e.target.value)} /></label><div className="table-scroll"><table><thead><tr><th>Chọn</th><th>Vật tư</th></tr></thead><tbody>{choices.map(i => <tr key={i.itemId}><td><input type="checkbox" aria-label={`Chọn ${i.name}`} checked={selected.includes(i.itemId)} onChange={e => setSelected(old => e.target.checked ? [...old, i.itemId] : old.filter(id => id !== i.itemId))} /></td><td>{i.code} · {i.name}</td></tr>)}</tbody></table></div>{error && <AlertPopup>{error}</AlertPopup>}<div className="actions"><button className="primary" disabled={pending || !selected.length || selected.length > 100}>Thêm {selected.length} vật tư</button><button type="button" disabled={pending} onClick={close}>Hủy</button></div></form></Dialog>;
}

export function StocktakeCount({ doc, line, pending, submit }: { doc: Row; line: Row; pending: boolean; submit: (path: string, body: Row, method?: string) => Promise<void> }) {
  const [value, setValue] = useState(String(line.countedQty ?? 0));
  return <form className="toolbar" onSubmit={e => { e.preventDefault(); if (pending) return; void submit(`documents/${doc.documentId}/lines/${line.lineId}`, { rowVersion: doc.rowVersion, itemId: line.itemId, unitId: line.unitId, quantity: line.actualQty, unitCost: null, sourceLineId: line.sourceLineId, countedQty: Number(value) }, "PUT"); }}><input aria-label="Số lượng thực đếm" type="number" min="0" step="0.000001" required value={value} onChange={e => setValue(e.target.value)} /><button disabled={pending || value === "" || Number(value) === line.countedQty}>Lưu</button></form>;
}
