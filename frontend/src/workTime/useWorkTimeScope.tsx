import { useEffect, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import { LoadingIndicator } from "../components/LoadingIndicator";

type Branch = { branchId: number; name: string; isPrimary: boolean; canReadShifts: boolean; canReadOwnTime: boolean; correctionDepartments: {id:number; name:string}[] };
type Mode = "workday" | "log" | "corrections";
const eligible = (b: Branch, mode: Mode) => mode === "workday" ? b.canReadShifts || b.canReadOwnTime : mode === "log" ? b.canReadOwnTime : b.canReadOwnTime || b.correctionDepartments.length > 0;
export function useWorkTimeScope(mode: Mode, requestedBranch?: number) {
  const { state, actions } = useAuth();
  const owner = state.status === "AUTHENTICATED" && !state.pending && !state.session?.mustChangePassword ? state.authorityGeneration : null;
  const [result, setResult] = useState<{owner: typeof owner; items?: Branch[]; error?: string} | null>(null);
  const [selection, setSelection] = useState<{owner: typeof owner; id:number} | null>(null);
  const [revision, setRevision] = useState(0);
  useEffect(() => {
    setResult(null); const controller = new AbortController();
    if (owner) void (async () => {
      try {
        const response = await fetch("/api/workdays/workspace-options", {signal:controller.signal, credentials:"same-origin",cache:"no-store",redirect:"error"});
        if (controller.signal.aborted) return;
        if (response.status === 401) { actions.invalidateSession(owner); return; }
        if (!response.ok) throw new Error();
        const items = await response.json() as Branch[];
        const validId = (v: number) => Number.isSafeInteger(v) && v > 0;
        if (!Array.isArray(items) || items.some(b => !b || !validId(b.branchId) || typeof b.name !== "string" || typeof b.isPrimary !== "boolean"
          || typeof b.canReadShifts !== "boolean" || typeof b.canReadOwnTime !== "boolean" || !Array.isArray(b.correctionDepartments)
          || b.correctionDepartments.some(d => !validId(d.id) || typeof d.name !== "string")) || new Set(items.map(b=>b.branchId)).size !== items.length) throw new Error();
        if (!controller.signal.aborted) setResult({owner, items});
      } catch { if (!controller.signal.aborted) setResult({owner,error:"Chưa tải được chi nhánh làm việc. Vui lòng thử lại."}); }
    })();
    return () => controller.abort();
  }, [owner, actions, revision]);
  const visible = owner && result?.owner === owner ? result : null;
  const items = visible?.items?.filter(b => eligible(b, mode));
  const branch = items?.find(b=>b.branchId===(selection?.owner===owner?selection.id:requestedBranch))
    ?? items?.find(b=>b.isPrimary) ?? (items?.length===1?items[0]:undefined);
  return {owner, items, branch, error:visible?.error, select:(id:number)=>setSelection({owner,id}), reload:()=>setRevision(n=>n+1)};
}
export function WorkTimeBranchField({scope, disabled=false}:{scope:ReturnType<typeof useWorkTimeScope>;disabled?:boolean}) {
  return <section className="time-scope" aria-label="Chi nhánh làm việc">
    {scope.error ? <><p role="alert">{scope.error}</p><button onClick={scope.reload}>Thử lại</button></> : !scope.items ? <LoadingIndicator compact label="Đang tải chi nhánh" />
      : scope.items.length===0 ? <p>Bạn chưa có chi nhánh được phép sử dụng chức năng này.</p>
      : scope.items.length===1 ? <div><span className="time-muted">Chi nhánh làm việc</span><strong>{scope.items[0].name}</strong></div>
      : <label>Chi nhánh làm việc<select aria-label="Chi nhánh làm việc" disabled={disabled} value={scope.branch?.branchId??""} onChange={e=>scope.select(Number(e.target.value))}><option value="">Chọn chi nhánh</option>{scope.items.map(b=><option key={b.branchId} value={b.branchId}>{b.name}</option>)}</select></label>}
  </section>;
}
