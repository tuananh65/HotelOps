import { isObject } from "../auth/http";
import type { Values } from "./contracts";

export const materialRequesterActions = ["A004A-044", "A004A-045", "A004A-049"];
export function materialRequesterReady(record: Values, actor: string, action: string): boolean {
  if (record.isNewWorkflow !== true || record.mutationEligible !== true || String(record.requesterId) !== actor
    || typeof record.rowVersion !== "string" || !Array.isArray(record.lines) || !Array.isArray(record.fulfillments)) return false;
  if (record.lines.some(l => !isObject(l) || Number(l.fulfilledQty) !== 0)
    || record.fulfillments.some(f => !isObject(f) || f.stockDocumentId != null)) return false;
  return (action === "A004A-045" ? ["DRAFT", "NEEDS_INFORMATION"]
    : ["DRAFT", "PENDING_APPROVAL", "NEEDS_INFORMATION", "APPROVED"]).includes(String(record.rawStatus));
}

export function MaterialCancellationFields({ values, onChange }: { values: Values; onChange: (v: Values) => void }) {
  return <section><label>Lý do hủy<textarea required minLength={10} value={String(values.reason ?? "")}
    onChange={event => onChange({ ...values, reason: event.target.value })} /></label>
    <label><input type="checkbox" required checked={values.confirmCancellation === true}
      onChange={event => onChange({ ...values, confirmCancellation: event.target.checked })} />
      Tôi xác nhận hủy yêu cầu vật tư này.</label></section>;
}
