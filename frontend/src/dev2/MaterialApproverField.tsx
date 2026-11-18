import { displayCode } from "../i18n/vi";
import { useEffect, useState } from "react";
import { createHttpClient, isObject } from "../auth/http";
import { useAuth } from "../auth/AuthProvider";

type Candidate = { userId: number; employeeCode: string; fullName: string };
type Options = { branchId: number; sourceWarehouseId: number | null; departmentId: number; requiredPermission: string; items: Candidate[] };
const request = createHttpClient();
const positive = (v: unknown): v is number => typeof v === "number" && Number.isSafeInteger(v) && v > 0;
function decode(raw: unknown): Options | undefined {
  if (!isObject(raw) || !positive(raw.branchId) || !positive(raw.departmentId)
    || !(raw.sourceWarehouseId === null || positive(raw.sourceWarehouseId))
    || !["MR_APPROVE_L1", "MR_APPROVE_FALLBACK"].includes(String(raw.requiredPermission)) || !Array.isArray(raw.items)
    || raw.items.some(v => !isObject(v) || !positive(v.userId) || typeof v.employeeCode !== "string" || typeof v.fullName !== "string")
    || new Set(raw.items.map(v => v.userId)).size !== raw.items.length) return undefined;
  return raw as Options;
}

export function MaterialApproverField({ branch, warehouse, value, required, onChange }: {
  branch: unknown; warehouse: unknown; value: unknown; required: boolean; onChange: (value: string) => void;
}) {
  const { state, actions } = useAuth();
  const owner = state.status === "AUTHENTICATED" && !state.pending ? state.authorityGeneration : null;
  const branchId = String(branch ?? ""), warehouseId = String(warehouse ?? "");
  const key = `${branchId}:${warehouseId}`;
  const [revision, setRevision] = useState(0);
  const [loaded, setLoaded] = useState<{ owner: typeof owner; key: string; rows: Candidate[]; message: string }>();
  useEffect(() => {
    setLoaded(undefined);
    if (!owner || !/^[1-9]\d*$/.test(branchId) || !Number.isSafeInteger(Number(branchId))
      || warehouseId && (!/^[1-9]\d*$/.test(warehouseId) || !Number.isSafeInteger(Number(warehouseId)))) return;
    const abort = new AbortController();
    void request({ method: "GET", url: `/api/material-requests/creation-approvers?branchId=${branchId}${warehouseId ? `&sourceWarehouseId=${warehouseId}` : ""}`,
      expectedStatus: 200, decode, signal: abort.signal }).then(result => {
      if (abort.signal.aborted) return;
      const matching = result.ok && result.data.branchId === Number(branchId) && result.data.sourceWarehouseId === (warehouseId ? Number(warehouseId) : null);
      setLoaded({ owner, key, rows: matching ? result.data.items : [], message: matching
        ? result.data.items.length ? "" : "Hiện chưa có người duyệt đủ điều kiện. Bạn có thể lưu nháp."
        : "Chưa có danh sách người duyệt. Kiểm tra chi nhánh và kho cấp phát, rồi tải lại." });
      if (!result.ok && result.error.kind === "http" && result.error.status === 401) void actions.revalidate();
    });
    return () => abort.abort();
  }, [owner, branchId, warehouseId, key, revision, actions]);
  const current = loaded?.owner === owner && loaded.key === key ? loaded : undefined;
  const selected = current?.rows.some(c => String(c.userId) === String(value)) ? String(value) : "";
  return <div><label>Người duyệt{required ? "(bắt buộc để gửi)" : "(để gửi kết quả)"}
    <select name="assignedApproverUserId" value={selected} required={required}
      onChange={event => onChange(event.target.value)}>
      <option value="">Chọn người duyệt</option>
      {current?.rows.map(c => <option key={c.userId} value={c.userId}>{c.fullName} ({displayCode(c.employeeCode)})</option>)}
    </select></label>
    <button type="button" onClick={() => { onChange(""); setRevision(v => v + 1); }}>Tải lại người duyệt</button>
    <p role="status">{current?.message ?? (branchId ? "Đang tải người duyệt đủ điều kiện…" : "Chọn chi nhánh để tải người duyệt đủ điều kiện.")}</p>
  </div>;
}
