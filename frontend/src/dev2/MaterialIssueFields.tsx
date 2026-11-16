import { isObject } from "../auth/http";
import type { Values } from "./contracts";

export function materialIssueReady(record: Values, actor: string): boolean {
  const cycle = Array.isArray(record.cycles) ? record.cycles.filter(isObject).sort((a, b) => Number(b.revisionNo) - Number(a.revisionNo))[0] : undefined;
  return record.isNewWorkflow === true && record.mutationEligible === true && typeof record.rowVersion === "string"
    && ["APPROVED", "PARTIALLY_ISSUED"].includes(String(record.rawStatus)) && String(record.requesterId) !== actor
    && cycle?.status === "APPROVED" && String(cycle.assignedApproverUserId) !== actor;
}

export function MaterialIssueFields({ record, values, onChange }: { record: Values; values: Values; onChange: (value: Values) => void }) {
  const lines = Array.isArray(record.lines) ? record.lines.filter(isObject).filter(l => l.isActive === true && Number(l.openFulfillableQty) > 0) : [];
  const entered = Array.isArray(values.lines) ? values.lines.filter(isObject) : [];
  return <section aria-label="Issue approved materials">
    <p>Only the remaining approved quantities can be issued. Availability is not a reservation; stock is checked again when saving.</p>
    {lines.map(line => <div key={String(line.requestLineId)}>
      <p>{String(line.itemName ?? `Material ${line.itemId}`)}: {String(line.openFulfillableQty)} {String(line.unitName ?? "")} remaining.</p>
      {values.mode === "Partial" && <label>Quantity to issue (zero skips this line)
        <input type="number" min="0" max={Number(line.openFulfillableQty)} step="0.001" required
          value={String(entered.find(l => String(l.requestLineId) === String(line.requestLineId))?.quantity ?? 0)}
          onChange={event => onChange({ ...values, lines: lines.map(l => ({ requestLineId: l.requestLineId,
            quantity: l.requestLineId === line.requestLineId ? event.target.value
              : entered.find(e => String(e.requestLineId) === String(l.requestLineId))?.quantity ?? 0 })) })} />
      </label>}
    </div>)}
    {values.mode === "FullRemaining" && <p>All remaining quantities shown above will be issued together, or the transaction will fail.</p>}
  </section>;
}
