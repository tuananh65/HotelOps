import { isObject } from "../auth/http";
import type { Values } from "./contracts";

export const materialApprovalActions = ["A004A-046", "A004A-047", "A004A-048", "A004A-050"];
export function materialApprovalReady(record: Values, actor: string, action: string): boolean {
  if (!record.isNewWorkflow || !record.mutationEligible || !Array.isArray(record.cycles)) return false;
  const cycles = record.cycles.filter(isObject).sort((a, b) => Number(b.revisionNo) - Number(a.revisionNo));
  const cycle = cycles[0];
  return !!cycle && String(cycle.assignedApproverUserId) === actor && String(record.requesterId) !== actor
    && String(cycle.submittedBy) !== actor && typeof record.rowVersion === "string"
    && (action === "A004A-050" ? record.rawStatus === "PARTIALLY_ISSUED" && cycle.status === "APPROVED"
      : record.rawStatus === "PENDING_APPROVAL" && cycle.status === "PENDING");
}

export function MaterialApprovalFields({ record, values, action, onChange }: {
  record: Values; values: Values; action: string; onChange: (v: Values) => void;
}) {
  const rows = Array.isArray(record.lines) ? record.lines.filter(isObject).filter(l => l.isActive === true) : [];
  const quantities = Array.isArray(values.lines) ? values.lines.filter(isObject) : [];
  const reasonRequired = action !== "A004A-047" || quantities.length > 0
    && quantities.every(q => String(q.approvedQty).trim() !== "" && Number(q.approvedQty) === 0);
  function quantity(id: unknown, value: string) {
    onChange({ ...values, lines: rows.map(l => ({ requestLineId: l.requestLineId,
      approvedQty: l.requestLineId === id ? value : quantities.find(q => q.requestLineId === l.requestLineId)?.approvedQty ?? "" })) });
  }
  return <section aria-label="Quyết định duyệt vật tư">
    {action === "A004A-047" && rows.map((line, index) => <label key={String(line.requestLineId)}>
      {String(line.itemName ?? `Dòng ${index + 1}`)} — Đã yêu cầu {String(line.requestedQty)} {String(line.unitName ?? "")}
      <input aria-label={`Số lượng duyệt cho dòng ${index + 1}`} type="number" min="0" max={Number(line.requestedQty)} step="0.001" required
        value={String(quantities.find(q => q.requestLineId === line.requestLineId)?.approvedQty ?? "")}
        onChange={event => quantity(line.requestLineId, event.target.value)} />
    </label>)}
    <label>{action === "A004A-047" ? "Ghi chú quyết định (bắt buộc nếu mọi số lượng đều bằng 0)" : "Lý do"}
      <textarea required={reasonRequired} minLength={reasonRequired ? 10 : undefined}
        value={String(values.reason ?? "")} onChange={event => onChange({ ...values, reason: event.target.value })} />
    </label>
  </section>;
}
