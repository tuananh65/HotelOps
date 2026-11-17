import { useEffect, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import { strongBranchTag } from "./contracts";

type Review = { branchId: number; activeRooms: number; warehousesWithNonzeroStock: number;
  states: { kind: string; status: string | null; count: number; classification: string }[];
  canDeactivate: boolean; coordination: string; scopeResolutionComplete: boolean; hasUnresolvedStates: boolean };

/** The server owns admission and rechecks every dependency in the mutation transaction. */
export function BranchDeactivationReview({ branchId }: { branchId: number }) {
  const { state, actions } = useAuth();
  const owner = state.status === "AUTHENTICATED" && !state.pending ? state.authorityGeneration : null;
  const current = useRef(owner); current.current = owner;
  const [attempt, setAttempt] = useState(0);
  const [result, setResult] = useState<{ owner: typeof owner; id: number; value?: Review; version?: string; error?: string } | null>(null);
  const [reason,setReason]=useState(""),[confirmed,setConfirmed]=useState(false),[busy,setBusy]=useState(false),[message,setMessage]=useState("");
  const writer=useRef<AbortController|null>(null);
  useEffect(() => {
    setResult(null); setConfirmed(false);setReason("");setMessage("");writer.current?.abort();setBusy(false);
    if (!owner || !attempt || !Number.isSafeInteger(branchId) || branchId <= 0) return;
    const abort = new AbortController();
    void (async () => {
      const response = await fetch(`/api/branches/${branchId}/deactivation-dependencies`, {
        credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal });
      if (response.status === 401) actions.invalidateSession(owner);
      if (!response.ok) throw new Error(response.status === 403 ? "ACCESS_DENIED" : `Không đọc được phụ thuộc (${response.status}).`);
      const value = await response.json() as Review;
      if (value.branchId !== branchId || !Array.isArray(value.states)) throw new Error("Dữ liệu phụ thuộc không hợp lệ.");
      const version=response.headers.get("ETag");
      if(!strongBranchTag(version))throw new Error("Native branch version required.");
      if (!abort.signal.aborted && current.current === owner) setResult({ owner, id: branchId, value,version });
    })().catch(e => { if (!abort.signal.aborted && current.current === owner) setResult({ owner, id: branchId,
      error: e instanceof Error ? e.message : "Không đọc được phụ thuộc." }); });
    return () => {abort.abort();writer.current?.abort();};
  }, [attempt, branchId, owner, actions]);
  const visible = result?.owner === owner && result?.id === branchId ? result : null;
  async function deactivate(){
    if(!owner||!visible?.value?.canDeactivate||!visible.version||!confirmed||reason.trim().length<10||busy)return;
    const abort=new AbortController();writer.current=abort;setBusy(true);setMessage("");
    try{
      const csrf=await fetch("/api/auth/csrf",{credentials:"same-origin",cache:"no-store",redirect:"error",signal:abort.signal});
      if(csrf.status===401)actions.invalidateSession(owner);
      const token=await csrf.json();
      if(!csrf.ok||token.headerName!=="X-CSRF-TOKEN"||typeof token.csrfToken!=="string"||!token.csrfToken)throw new Error("CSRF unavailable.");
      if(abort.signal.aborted||current.current!==owner)return;
      const response=await fetch(`/api/branches/${branchId}/deactivate`,{method:"POST",credentials:"same-origin",cache:"no-store",redirect:"error",signal:abort.signal,
        headers:{"Content-Type":"application/json","X-CSRF-TOKEN":token.csrfToken,"If-Match":visible.version},body:JSON.stringify({confirmation:true,reason:reason.trim()})});
      if(response.status===401)actions.invalidateSession(owner);
      if(!response.ok){const p=await response.json().catch(()=>null);throw new Error(p?.title??`Deactivate failed (${response.status}).`);}
      if(!abort.signal.aborted&&current.current===owner){setResult(null);setConfirmed(false);setMessage("MSG08 — Chi nhánh đã Inactive. Tải lại thông tin chi nhánh trước thao tác tiếp theo.");}
    }catch(e){if(!abort.signal.aborted&&current.current===owner){setResult(null);setConfirmed(false);setMessage((e instanceof Error?e.message:"Kết quả chưa xác định.")+" Kiểm tra lại dữ liệu trước khi thử lại.");}}
    finally{if(writer.current===abort){writer.current=null;setBusy(false);}}
  }
  return <section><h2>Deactivate chi nhánh</h2>
    <button disabled={!owner||busy} onClick={() => setAttempt(v => v + 1)}>Kiểm tra phụ thuộc hiện tại</button>
    {message&&<p role="status">{message}</p>}
    {visible?.error && <p role="alert">{visible.error}</p>}
    {visible?.value && <><p>Phòng Active: {visible.value.activeRooms}; kho còn tồn: {visible.value.warehousesWithNonzeroStock}</p>
      <ul>{visible.value.states.filter(s => s.classification !== "CLOSED").map(s =>
        <li key={`${s.kind}:${s.status}`}>{s.kind}: {s.status ?? "N/A"} — {s.count} ({s.classification})</li>)}</ul>
      {!visible.value.canDeactivate && <p role="alert">MSG34 — Chưa cho phép Deactivate: {visible.value.coordination}.
        {visible.value.hasUnresolvedStates && " Còn trạng thái hoặc phạm vi phụ thuộc chưa được chứng minh."}</p>}
      {visible.value.canDeactivate&&<form onSubmit={e=>{e.preventDefault();void deactivate();}}><fieldset disabled={busy}>
        <legend>MSG33 — Xác nhận Deactivate chi nhánh</legend>
        <label>Lý do<textarea required minLength={10} maxLength={1000} value={reason} onChange={e=>setReason(e.target.value)}/></label>
        <label><input type="checkbox" checked={confirmed} onChange={e=>setConfirmed(e.target.checked)}/>Tôi xác nhận vô hiệu hóa chi nhánh; giữ nguyên lịch sử.</label>
        <button disabled={!confirmed||reason.trim().length<10} type="submit">Deactivate</button>
      </fieldset></form>}
    </>}
  </section>;
}
