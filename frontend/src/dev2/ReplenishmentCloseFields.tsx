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
  return <section aria-label="Close replenishment remainder">
    <p>Close the remaining quantities that will not arrive. Existing posted receipt quantities are preserved.</p>
    {!replenishmentCanClose(record) && <p role="status">Load an open replenishment with a positive remainder before closing.</p>}
    <ul>{lines.filter(l => Number(l.remainingBaseQty) > 0).map(l => <li key={String(l.replenishmentLineId)}>
      Item {String(l.itemId)}: requested {String(l.requestedBaseQty)}, received {String(l.netReceivedBaseQty)},
      remaining {String(l.remainingBaseQty)} (base unit {String(l.baseUomId)}).
    </li>)}</ul>
    <label>Reason for closing
      <textarea required minLength={10} value={String(values.reason ?? "")}
        onChange={e => onChange({ ...values, reason: e.target.value })} />
    </label>
    <label><input type="checkbox" required checked={values.confirmRemainderClosure === true}
      onChange={e => onChange({ ...values, confirmRemainderClosure: e.target.checked })} />
      I confirm that the remaining quantities will not arrive.
    </label>
  </section>;
}
