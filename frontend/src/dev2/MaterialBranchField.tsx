import { useEffect, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import { createHttpClient, isObject } from "../auth/http";

type Branch = { branchId: number; name: string };
const request = createHttpClient();
function decode(raw: unknown): Branch[] | undefined {
  if (!Array.isArray(raw) || raw.some(b => !isObject(b) || typeof b.branchId !== "number" || !Number.isSafeInteger(b.branchId)
    || b.branchId <= 0 || typeof b.name !== "string") || new Set(raw.map(b => b.branchId)).size !== raw.length) return undefined;
  return raw as Branch[];
}

export function MaterialBranchField({ value, onChange, fixed = false }: { value: unknown; onChange: (branch: string) => void; fixed?: boolean }) {
  const { state, actions } = useAuth();
  const owner = state.status === "AUTHENTICATED" && !state.pending ? state.authorityGeneration : null;
  const [revision, setRevision] = useState(0);
  const [loaded, setLoaded] = useState<{ owner: typeof owner; branches: Branch[]; error?: string }>();
  useEffect(() => {
    setLoaded(undefined);
    if (!owner) return;
    const abort = new AbortController();
    void request({ method: "GET", url: "/api/material-requests/creation-branches", expectedStatus: 200, decode, signal: abort.signal }).then(result => {
      if (abort.signal.aborted) return;
      setLoaded({ owner, branches: result.ok ? result.data : [], error: result.ok ? undefined : "Chưa có lựa chọn chi nhánh. Tải lại quyền truy cập hiện tại." });
      if (!result.ok && result.error.kind === "http" && result.error.status === 401) void actions.revalidate();
    });
    return () => abort.abort();
  }, [owner, revision, actions]);
  const current = loaded?.owner === owner ? loaded : undefined;
  return <div><label>Chi nhánh<select required name="branchId" disabled={fixed}
    value={current?.branches.some(b => String(b.branchId) === String(value)) ? String(value) : ""}
    onChange={event => onChange(event.target.value)}>
    <option value="">Chọn chi nhánh</option>{current?.branches.map(b => <option key={b.branchId} value={b.branchId}>{b.name}</option>)}
  </select></label>
    <button type="button" onClick={() => setRevision(v => v + 1)}>Tải lại chi nhánh</button>
    <p role="status">{current?.error ?? (!current ? "Đang tải chi nhánh…" : current.branches.length ? "" : "Hiện bạn không được tạo yêu cầu vật tư ở chi nhánh nào.")}</p>
  </div>;
}
