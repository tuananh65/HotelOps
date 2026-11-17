import { Link } from "react-router-dom";
import { isObject } from "../auth/http";
import type { Values } from "./contracts";

type Event = { key: string; title: string; at: unknown; actor: unknown; note?: unknown; lines?: Values[]; fulfillment?: unknown; returnLines?: Values[] };
const rows = (v: unknown): Values[] => Array.isArray(v) ? v.filter(isObject) : [];
const stamp = (v: unknown) => typeof v === "string" && Number.isFinite(Date.parse(v)) ? Date.parse(v) : null;

export function MaterialHistoryView({ record }: { record: Values }) {
  const cycles = rows(record.cycles);
  const events: Event[] = [{ key: "created", title: "Request created", at: record.createdAt, actor: record.requesterId }];
  for (const cycle of cycles) events.push({ key: `cycle-${cycle.approvalCycleId}`, title: `Submitted for review — revision ${cycle.revisionNo}`,
    at: cycle.submittedAt, actor: cycle.submittedBy });
  for (const decision of rows(record.decisions)) events.push({ key: `decision-${decision.approvalId}`,
    title: String(decision.decision), at: decision.decidedAt, actor: decision.approverId, note: decision.note,
    lines: rows(cycles.find(c => c.approvalCycleId === decision.approvalCycleId)?.lines) });
  for (const batch of rows(record.fulfillments)) events.push({ key: `issue-${batch.fulfillmentId}`,
    title: `Issue ${batch.fulfillmentNo} (${batch.status})`, at: batch.fulfilledAt, actor: batch.fulfilledBy,
    fulfillment: batch.fulfillmentId, lines: rows(batch.lines), note: batch.receiverName == null ? "Receiver name: N/A (not recorded)" : `Receiver name: ${batch.receiverName}` });
  const returns = new Map<unknown, Values[]>();
  for (const line of rows(record.returns)) returns.set(line.documentId, [...(returns.get(line.documentId) ?? []), line]);
  for (const [id, lines] of returns) {
    const receipt = lines[0];
    events.push({ key: `return-created-${id}`, title: `Return ${receipt.documentNo} created (${receipt.status})`,
      at: receipt.createdAt, actor: null });
    if (receipt.postedAt != null || receipt.status === "POSTED" || receipt.status === "REVERSED")
      events.push({ key: `return-posted-${id}`, title: `Return ${receipt.documentNo} posting (${receipt.status})`,
        at: receipt.postedAt, actor: receipt.postedBy, returnLines: lines });
  }
  if (record.remainderClosedAt != null) events.push({ key: "remainder", title: "Unissued remainder closed",
    at: record.remainderClosedAt, actor: record.remainderClosedBy, note: record.remainderCloseReason });
  events.sort((a, b) => (stamp(a.at) ?? Infinity) - (stamp(b.at) ?? Infinity) || a.key.localeCompare(b.key));
  return <section aria-label="Material request business history">
    <h2>Workflow history</h2>
    <p>Current status: <strong>{String(record.rawStatus)}</strong>. Times below are recorded business timestamps in UTC.</p>
    <ol>{events.map(event => <li key={event.key}>
      <strong>{event.title}</strong>{" — "}
      {stamp(event.at) == null ? <span>Timestamp unavailable</span> : <time dateTime={String(event.at)}>{new Date(String(event.at)).toISOString()}</time>}
      {event.actor != null && <span> · User {String(event.actor)}</span>}
      {event.note != null && <p>{String(event.note)}</p>}
      {event.returnLines && <ul>{event.returnLines.map(line => <li key={String(line.returnLineId ?? line.originalIssueLineId)}>
        {String(line.itemName ?? line.itemId ?? "Material unavailable")}: recorded quantity {String(line.actualQty ?? "N/A")},
        damaged {String(line.damagedQty ?? "N/A")} {String(line.unitName ?? "Unit unavailable")};
        original issue line {String(line.originalIssueLineId)}.
      </li>)}</ul>}
      {event.lines && event.lines.length > 0 && <ul>{event.lines.map((line, index) => <li key={`${line.requestLineId}:${index}`}>
        Material {String(line.itemId)}, unit {String(line.uomId)}: {event.fulfillment != null
          ? `issued ${line.issuedQty}` : `requested ${line.requestedQty}; approved ${line.approvedQty ?? "not recorded"}`}
      </li>)}</ul>}
      {event.fulfillment != null && <Link to={`/operations/material-requests/${encodeURIComponent(String(record.requestId))}?action=A004A-042&fulfillmentId=${encodeURIComponent(String(event.fulfillment))}`}>Open fulfillment receipt</Link>}
    </li>)}</ol>
    {record.rawStatus === "CANCELLED" && <p>Request cancelled: {String(record.cancellationReason ?? "Reason unavailable")}.
      A separate cancellation timestamp is not available in this response.</p>}
    <h3>Current quantities</h3>
    <ul>{rows(record.lines).filter(l => l.isActive === true).map(line => <li key={String(line.requestLineId)}>
      {String(line.itemName ?? line.itemId)}: approved {String(line.approvedQty ?? "N/A")}, issued {String(line.fulfilledQty)},
      remaining {String(line.historicalRemainingQty ?? "N/A")} {String(line.unitName ?? line.uomId)};
      currently open to fulfill {String(line.openFulfillableQty)}.
    </li>)}</ul>
    <h3>Return documents</h3>
    <p>Return quantities describe their source documents. Draft, cancelled or reversed returns do not imply available stock or reduce the request's historical issued quantity.</p>
    {rows(record.returns).length ? <ul>{rows(record.returns).map((receipt, index) => <li key={`${receipt.documentId}:${receipt.originalIssueLineId}:${index}`}>
      <Link to={`/inventory/returns/${encodeURIComponent(String(receipt.documentId))}`}>{String(receipt.documentNo)}</Link> — {String(receipt.status)}; original issue line {String(receipt.originalIssueLineId)}.
    </li>)}</ul> : <p>No linked return documents recorded.</p>}
    {record.incidentId != null && <section aria-label="Linked incident">
      <h3>Linked incident</h3>
      {isObject(record.linkedIncident) ? <p>
        <Link to={`/operations/incidents/${encodeURIComponent(String(record.linkedIncident.incidentId))}`}>{String(record.linkedIncident.incidentNo)}</Link>
        {" "}{String(record.linkedIncident.title)} — {String(record.linkedIncident.status)}
      </p> : <p>Incident details unavailable in your current incident viewing scope. The historical link is retained.</p>}
    </section>}
    <h3>Linked internal requests</h3>
    {record.linkedWorkAvailable !== true ? <p>Linked internal request information is unavailable.</p>
      : rows(record.linkedInternalRequests).length === 0 ? <p>No linked internal requests in your current viewing scope.</p>
      : <ul>{rows(record.linkedInternalRequests).map(request => <li key={String(request.requestId)}>
        <Link to={`/internal-requests?requestId=${encodeURIComponent(String(request.requestId))}`}>{String(request.requestNo)}</Link>
        {" "}{String(request.status)}; {request.mandatory === true ? "mandatory material dependency" : "optional material dependency"}.
      </li>)}</ul>}
  </section>;
}
