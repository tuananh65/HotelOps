import { isObject } from "../auth/http";
import type { Values } from "./contracts";

const rows = (value: unknown): Values[] => Array.isArray(value) ? value.filter(isObject) : [];
const display = (value: unknown) => value == null || value === "" ? "Not recorded" : String(value);

export function ReplenishmentDetailView({ record }: { record: Values }) {
  const lines = rows(record.lines);
  const receipts = rows(record.receipts);
  return <section aria-label="Replenishment details">
    <h3>{display(record.replenishmentNo)}</h3>
    <p>Status: {display(record.rawStatus)}; warehouse: {display(record.warehouseId)}; branch: {display(record.branchId)}</p>
    <p>Supplier: {display(record.supplierName)}; expected: {display(record.expectedOn)}; created: {display(record.createdAt)}</p>
    <table><caption>Quantities in each item's base unit</caption>
      <thead><tr><th>Item</th><th>Unit</th><th>Requested</th><th>Net received</th><th>Remaining</th><th>Coverage</th><th>Note</th></tr></thead>
      <tbody>{lines.map(line => <tr key={String(line.replenishmentLineId)}>
        <td>{display(line.itemName)}</td><td>{display(line.unitName)}</td><td>{display(line.requestedBaseQty)}</td>
        <td>{display(line.netReceivedBaseQty)}</td><td>{display(line.remainingBaseQty)}</td>
        <td>{line.isOpenCoverage === true ? "Open" : "Closed"}</td><td>{display(line.note)}</td>
      </tr>)}</tbody>
    </table>
    <h4>Linked receipt evidence</h4>
    <p>Only valid Posted receipts contribute to received quantities. Quantities below use the linked item's base unit.</p>
    {receipts.length === 0 ? <p>No linked receipts.</p> : <table>
      <thead><tr><th>Receipt</th><th>Item</th><th>Status</th><th>Credited</th><th>Effective</th><th>Reversal</th><th>Linked at</th></tr></thead>
      <tbody>{receipts.map(receipt => {
        const line = lines.find(l => l.replenishmentLineId === receipt.replenishmentLineId);
        return <tr key={String(receipt.linkId)}>
          <td>{display(receipt.documentNo)}</td><td>{display(line?.itemName)} ({display(line?.unitName)})</td>
          <td>{display(receipt.receiptStatus)}</td><td>{display(receipt.creditedBaseQty)}</td><td>{display(receipt.effectiveBaseQty)}</td>
          <td>{receipt.reversalDocumentId == null ? "None" : display(receipt.reversalDocumentId)}</td><td>{display(receipt.linkedAt)}</td>
        </tr>;
      })}</tbody>
    </table>}
    {record.rawStatus === "CLOSED" && <p>Closed at {display(record.closedAt)} by user {display(record.closedBy)}. Reason: {display(record.closeReason)}. Unreceived quantities will not be received under this replenishment.</p>}
  </section>;
}
