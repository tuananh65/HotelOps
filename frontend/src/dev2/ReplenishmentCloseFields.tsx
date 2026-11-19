import { referenceLabel } from "../i18n/referenceLabel";
import { isObject } from "../auth/http";
import type { Values } from "./contracts";

export function replenishmentCanClose(record: Values): boolean {
  return typeof record.rowVersion === "string" && /^[A-Za-z0-9+/]{11}=$/.test(record.rowVersion)
    && ["REQUESTED", "PARTIALLY_RECEIVED"].includes(String(record.rawStatus))
    && Array.isArray(record.lines) && record.lines.some(l => isObject(l) && l.isOpenCoverage === true
      && typeof l.remainingBaseQty === "number" && Number.isFinite(l.remainingBaseQty) && l.remainingBaseQty > 0);
}

export function ReplenishmentCloseFields({ record, values, onChange }: {
  record: Values; values: Values; onChange: (value: Values) => void;
}) {
  const lines = Array.isArray(record.lines) ? record.lines.filter(isObject) : [];
  return <section aria-label="Đóng phần còn lại của yêu cầu bổ sung">
    <p>Đóng phần số lượng sẽ không nhận được. Số lượng đã ghi sổ trước đó giữ nguyên.</p>
    {!replenishmentCanClose(record) && <p role="status">Tải yêu cầu bổ sung đang mở có phần còn lại lớn hơn 0 trước khi đóng.</p>}
    <ul>{lines.filter(l => Number(l.remainingBaseQty) > 0).map(l => <li key={String(l.replenishmentLineId)}>
      Vật tư {referenceLabel(l, "itemId")}: Đã yêu cầu {String(l.requestedBaseQty)}, Đã nhận {String(l.netReceivedBaseQty)}, Còn lại {String(l.remainingBaseQty)} (Đơn vị cơ sở {referenceLabel(l, "baseUomId")}).
    </li>)}</ul>
    <label>Lý do đóng
      <textarea required minLength={10} value={String(values.reason ?? "")}
        onChange={e => onChange({ ...values, reason: e.target.value })} />
    </label>
    <label><input type="checkbox" required checked={values.confirmRemainderClosure === true}
      onChange={e => onChange({ ...values, confirmRemainderClosure: e.target.checked })} />
      Tôi xác nhận sẽ không nhận phần số lượng còn lại.
    </label>
  </section>;
}
