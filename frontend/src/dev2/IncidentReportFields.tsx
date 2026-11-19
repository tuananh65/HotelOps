import { userMessage } from "../i18n/userMessage";
import { SearchableSelect } from "../components/SearchableSelect";
import { IncidentLocationFields } from "./IncidentLocationFields";
import { useEffect, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import { createHttpClient, isObject } from "../auth/http";
import type { Values } from "./contracts";

type Branch = { branchId: number; name: string };
const request = createHttpClient();
function decode(raw: unknown): Branch[] | undefined {
  if (!Array.isArray(raw) || raw.some(b => !isObject(b) || !Number.isSafeInteger(b.branchId)
    || Number(b.branchId) <= 0 || typeof b.name !== "string")) return undefined;
  return raw as Branch[];
}

export function IncidentReportFields({ values, onChange, fixed }: {
  values: Values; onChange: (values: Values) => void; fixed: boolean;
}) {
  const { state, actions } = useAuth();
  const owner = state.status === "AUTHENTICATED" && !state.pending ? state.authorityGeneration : null;
  const [loaded, setLoaded] = useState<{ owner: typeof owner; branches: Branch[]; error?: string }>();
  const [revision, setRevision] = useState(0);
  useEffect(() => {
    setLoaded(undefined);
    if (!owner) return;
    const abort = new AbortController();
    void request({ method: "GET", url: "/api/incidents/creation-branches", expectedStatus: 200, decode, signal: abort.signal }).then(result => {
      if (abort.signal.aborted) return;
      setLoaded({ owner, branches: result.ok ? result.data : [], error: result.ok ? undefined : "Không tải được chi nhánh. Vui lòng thử lại." });
      if (!result.ok && result.error.kind === "http" && result.error.status === 401) void actions.revalidate();
    });
    return () => abort.abort();
  }, [owner, revision, actions]);
  const current = loaded?.owner === owner ? loaded : undefined;
  const selected = current?.branches.find(b => String(b.branchId) === String(values.branchId));
  useEffect(() => {
    if (!fixed && current && !selected && current.branches.length) {
      onChange({ ...values, branchId: String(current.branches[0].branchId), roomId: "", areaId: "", assetId: "", categoryId: "", priorityId: "", severityId: "" });
    }
  }, [current, selected, fixed, values, onChange]);
  return <>
    <label>Chi nhánh của bạn
      <SearchableSelect name="branchId" required value={selected ? String(selected.branchId) : ""}
        onChange={e => { if (!fixed) onChange({ ...values, branchId: e.target.value, roomId: "", areaId: "", assetId: "", categoryId: "", priorityId: "", severityId: "" }); }}>
        <option value="">{current ? "Không có chi nhánh phù hợp" : "Đang tải chi nhánh…"}</option>
        {current?.branches.filter(b => !fixed || b === selected).map(b => <option key={b.branchId} value={b.branchId}>{b.name}</option>)}
      </SearchableSelect>
    </label>
    {(!current || current.error || !current.branches.length || fixed && !selected) && <p role="status">{userMessage(current?.error ?? (!current ? "Đang kiểm tra quyền báo sự cố…" : "Bạn chưa có quyền báo sự cố tại chi nhánh này."))}</p>}
    {current?.error && <button type="button" onClick={() => setRevision(v => v + 1)}>Thử lại</button>}
    <div className="dev2-fields">
      <label>Tiêu đề sự cố *<input name="incidentTitle" required maxLength={200} value={String(values.incidentTitle ?? "")} onChange={e => onChange({ ...values, incidentTitle: e.target.value })} /></label>
      <label>Mô tả sự cố *<textarea name="description" required value={String(values.description ?? "")} onChange={e => onChange({ ...values, description: e.target.value })} /></label>
      <label>Thời điểm phát hiện *<input name="discoveredAt" type="datetime-local" required value={String(values.discoveredAt ?? "")} onChange={e => onChange({ ...values, discoveredAt: e.target.value })} /></label>
    </div>
    <IncidentLocationFields values={values} onChange={onChange} />
  </>;
}
