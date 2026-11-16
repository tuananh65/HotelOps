import { MaintenanceVerifyPanel } from "./MaintenanceVerifyPanel";
import { IncidentEvidencePanel } from "./IncidentEvidencePanel";
import { MaintenanceReturnPanel } from "./MaintenanceReturnPanel";
import { MaintenanceDeclinePanel } from "./MaintenanceDeclinePanel";
import { Link } from "react-router-dom";
import { isObject } from "../auth/http";
import type { Values } from "./contracts";
import { MaintenanceProgressPanel } from "./MaintenanceProgressPanel";
import { MaintenanceSubmissionPanel } from "./MaintenanceSubmissionPanel";
import { MaintenanceResumePanel } from "./MaintenanceResumePanel";

const show = (v: unknown) => typeof v === "string" || typeof v === "number" ? String(v) : "Unavailable";
const target = (row: Values) => [row.roomNumber && `Room ${row.roomNumber}`, row.areaName,
  row.assetCode && `${row.assetCode}${row.assetName ? " ? " + row.assetName : ""}`]
  .filter(v => typeof v === "string" && v.length > 0).join(" / ") || "Target unavailable";
const time = (v: unknown) => {
  if (typeof v !== "string") return "Unavailable";
  const date = new Date(/(?:Z|[+-]\d{2}:\d{2})$/.test(v) ? v : v + "Z");
  return Number.isNaN(date.getTime()) ? "Unavailable" : date.toLocaleString();
};

export function MaintenanceReadView({ data, detail, canDetail, canResume = false, canStart = false, canHold = false, canSubmit = false, canLog = false, canMaterials = false, canReturn = false, canVerify = false, onCommitted }: { data: Values; detail: boolean; canDetail: boolean; canResume?: boolean; canStart?: boolean; canHold?: boolean; canSubmit?: boolean; canLog?: boolean; canMaterials?: boolean; canReturn?: boolean; canVerify?: boolean; onCommitted?: (message: string) => void }) {
  if (!detail) {
    const rows = Array.isArray(data.items) ? data.items.filter(isObject) : [];
    return <section aria-label="Maintenance work results">
      <p>{show(data.totalCount)} matching records; page {show(data.page)} of {show(data.totalPages)}.</p>
      {rows.length === 0 ? <p>No maintenance work matches your filters and access scope.</p> :
        <table><thead><tr><th>Reference</th><th>Title</th><th>Source</th><th>Location / asset</th><th>Priority</th><th>Assignee</th><th>Status</th><th>Planned end</th></tr></thead>
          <tbody>{rows.map((row, index) => <tr key={show(row.maintenanceWorkId) + ":" + index}>
            <td>{canDetail && typeof row.maintenanceWorkId === "number" && Number.isSafeInteger(row.maintenanceWorkId) && row.maintenanceWorkId > 0
              ? <Link to={`/operations/maintenance/${row.maintenanceWorkId}?action=A004A-036`}>{show(row.workNo)}</Link> : show(row.workNo)}</td>
            <td>{show(row.title)}</td><td>{show(row.source)}</td><td>{target(row)}</td><td>{show(row.priorityName)}</td><td>{show(row.assigneeName ?? row.assigneeId)}</td><td>{show(row.status)}</td><td>{time(row.plannedEndAt)}</td>
          </tr>)}</tbody></table>}
    </section>;
  }
  const logs = Array.isArray(data.maintenanceWorkLogs) ? data.maintenanceWorkLogs.filter(isObject) : [];
  const submitted = isObject(data.submittedReview) ? data.submittedReview : null;
  const usages = logs.flatMap(log => Array.isArray(log.materialUsages) ? log.materialUsages.filter(isObject) : []);
  return <section aria-label="Maintenance work detail">
    {data.submissionWorkflow === "PENDING_VERIFICATION" && <p role="status">Pending verification. The repair result is submitted and its work interval is closed. The Incident remains open until an independent verifier accepts it.</p>}
    {data.submissionWorkflow === "UNVERIFIED" && <p role="alert">Submission evidence is inconsistent or unavailable. Verification cannot proceed until the record is reconciled.</p>}
    {typeof data.incidentId === "number" && Number.isSafeInteger(data.incidentId) && data.incidentId > 0
      && (data.assignmentWorkflow === "ASSIGNED" || data.assignmentWorkflow === "PENDING_ASSIGNMENT")
      && <Link to={`/operations/incidents/${data.incidentId}?action=A004A-023`}>Open incident assignment</Link>}
    {canStart && <MaintenanceDeclinePanel key={String(data.maintenanceWorkId) + ":" + String(data.executionETag)} data={data} />}
    {Array.isArray(data.declines) && data.declines.length > 0 && <section aria-label="Assignment decline history">
      <h3>Assignment declines</h3><ul>{data.declines.filter(isObject).map(row => <li key={String(row.auditId)}>
        {time(row.occurredAt)} ? Assignee {show(row.previousAssigneeId)}: {show(row.reason)}
      </li>)}</ul></section>}
    {canVerify && <MaintenanceVerifyPanel key={String(data.maintenanceWorkId) + ":" + String(data.executionETag)} data={data} onCommitted={onCommitted} />}
    {canReturn && <MaintenanceReturnPanel key={String(data.maintenanceWorkId) + ":" + String(data.executionETag)} data={data} onCommitted={onCommitted} />}
    {submitted && <section aria-label="Submitted maintenance snapshot">
      <h3>Submitted result</h3>
      <p>Submission #{show(submitted.submissionAuditId)} at {time(submitted.submittedAtUtc)}.</p>
      <dl><dt>Diagnosis / root cause</dt><dd>{show(submitted.diagnosis)}</dd>
        <dt>Action taken</dt><dd>{show(submitted.actionTaken)}</dd></dl>
      <p>Review material usage below. Evidence downloads enforce current Incident access.</p>
      {Array.isArray(submitted.resultAttachmentIds) && typeof data.incidentId === "number" && Number.isSafeInteger(data.incidentId) && data.incidentId > 0 &&
        <ul>{submitted.resultAttachmentIds.filter((id): id is number => typeof id === "number" && Number.isSafeInteger(id) && id > 0).map(id =>
          <li key={id}><a href={`/api/incidents/${data.incidentId}/evidence/${id}`}>Result evidence {id}</a></li>)}</ul>}
    </section>}
    {canLog && <MaintenanceProgressPanel key={String(data.maintenanceWorkId) + ":" + String(data.executionETag)} data={data} allowMaterials={canMaterials} onCommitted={onCommitted} />}
    {canSubmit && <MaintenanceSubmissionPanel key={String(data.maintenanceWorkId) + ":" + String(data.executionETag)} data={data} onCommitted={onCommitted} />}
    {typeof data.incidentId === "number" && Number.isSafeInteger(data.incidentId) && data.incidentId > 0
      && !(canSubmit && data.canSubmit === true) &&
      <IncidentEvidencePanel key={data.incidentId} incidentId={data.incidentId} readOnly />}
    {(canStart || canResume || canHold) && <MaintenanceResumePanel key={String(data.maintenanceWorkId) + ":" + String(data.executionETag)} data={data} allowStart={canStart} allowResume={canResume} allowHold={canHold} onCommitted={onCommitted} />}
    <h3>{show(data.workNo)} — {show(data.title)}</h3>
    <dl><dt>Status</dt><dd>{show(data.status)}</dd><dt>Assignee</dt><dd>{show(data.assigneeName ?? data.assigneeId)}</dd>
      <dt>Source</dt><dd>{show(data.source)}</dd><dt>Location / asset</dt><dd>{target(data)}</dd>
      <dt>Priority</dt><dd>{show(data.priorityName)}</dd>
      <dt>Instruction</dt><dd>{show(data.instruction)}</dd><dt>Planned start</dt><dd>{time(data.plannedStartAt)}</dd>
      <dt>Planned end</dt><dd>{time(data.plannedEndAt)}</dd><dt>Actual start</dt><dd>{time(data.actualStartAt)}</dd>
      <dt>Resolution</dt><dd>{show(data.resolutionNote)}</dd><dt>Resolved at</dt><dd>{time(data.resolvedAt)}</dd></dl>
    <h4>Work history</h4>
    {data.assignmentWorkflow != null && <p>Assignment workflow: {show(data.assignmentWorkflow)}.
      {data.assignmentWorkflow === "UNVERIFIED" && " Triage and assignment evidence must be reconciled before accepting this work."}</p>}
    {data.source === "INCIDENT" && <section aria-label="Incident SLA deadlines">
      <h4>Incident SLA deadlines</h4>
      <dl><dt>Response deadline</dt><dd>{isObject(data.incidentSla) ? time(data.incidentSla.responseDeadlineAt) : "Unavailable"}</dd>
        <dt>Resolution deadline</dt><dd>{isObject(data.incidentSla) ? time(data.incidentSla.resolutionDeadlineAt) : "Unavailable"}</dd></dl>
      {(!isObject(data.incidentSla) || data.incidentSla.unavailableReason != null) && <p>
        {isObject(data.incidentSla) && data.incidentSla.unavailableReason === "INCIDENT_SLA_ACCESS_UNAVAILABLE"
          ? "Current access does not allow viewing Incident SLA deadlines."
          : isObject(data.incidentSla) && data.incidentSla.unavailableReason === "INCIDENT_SLA_INVALID"
            ? "Recorded deadlines are inconsistent and cannot be used."
            : "Incident SLA deadlines have not been recorded or are unavailable."}
      </p>}
      <p>Maintenance planned times do not replace Incident SLA deadlines.</p>
    </section>}
    {logs.length === 0 ? <p>No work logs recorded.</p> : <ol>{logs.map((log, index) => <li key={show(log.logId) + ":" + index}>
      {typeof log.progressPercent === "number" && Number.isInteger(log.progressPercent) && log.progressPercent >= 0 && log.progressPercent <= 100 ? `${log.progressPercent}% ? ` : ""}{time(log.loggedAt)} — {show(log.status)} — {show(log.workNote)} ({show(log.spentMinutes)} minutes)
    </li>)}</ol>}
    <h4>Material usage</h4>
    {data.materialUsageAvailable !== true ? <p>Material usage is unavailable: current material/inventory access and source document integrity must both be verified.</p>
      : usages.length === 0 ? <p>No material usage recorded.</p>
      : <table><thead><tr><th>Item ID</th><th>Used quantity</th><th>Unit ID</th><th>Base quantity</th><th>Base unit ID</th><th>Issue document</th><th>Material request</th></tr></thead>
        <tbody>{usages.map((usage, index) => <tr key={show(usage.usageId) + ":" + index}>
          <td>{show(usage.itemId)}</td><td>{show(usage.usedQty)}</td><td>{show(usage.uomId)}</td>
          <td>{show(usage.usedBaseQty)}</td><td>{show(usage.baseUomId)}</td><td>{show(usage.documentNo)}</td>
          <td>{typeof usage.requestId === "number" && Number.isSafeInteger(usage.requestId) && usage.requestId > 0
            ? <Link to={`/operations/material-requests/${usage.requestId}?action=A004A-038`}>{usage.requestId}</Link> : "Unavailable"}</td>
        </tr>)}</tbody></table>}
  </section>;
}
