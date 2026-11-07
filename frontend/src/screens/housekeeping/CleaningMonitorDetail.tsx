import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import CleaningCancellationPanel from "./CleaningCancellationPanel";
import {
  readCleaning,
  monitorError,
  type MonitorDetail,
} from "../../api/dev1/cleaningMonitor";
export default function CleaningMonitorDetail({
  branch,
  kind,
  id,
}: {
  branch: string;
  kind: string;
  id: string;
}) {
  const [page, setPage] = useState(1),
    [retry, setRetry] = useState(0);
  const [result, setResult] = useState<MonitorDetail | null>(null),
    [error, setError] = useState(""),
    [loading, setLoading] = useState(true);
  useEffect(() => {
    const abort = new AbortController();
    setResult(null);
    setError("");
    setLoading(true);
    readCleaning(branch, kind, id, page, abort.signal)
      .then((data) => {
        if (!abort.signal.aborted) setResult(data);
      })
      .catch((e) => {
        if (!abort.signal.aborted) setError(monitorError(e));
      })
      .finally(() => {
        if (!abort.signal.aborted) setLoading(false);
      });
    return () => abort.abort();
  }, [branch, kind, id, page, retry]);
  return (
    <>
      <h1>Cleaning Task Detail</h1>
      {kind === "cleaning" && (
        <Link to={`/cleaning/tasks/${id}?branchId=${branch}&execution=1`}>
          Start / Reassign
        </Link>
      )}
      {kind === "rework" && (
        <Link to={`/rework/${id}?branchId=${branch}`}>Mở thực hiện rework</Link>
      )}
      <Link to={`/cleaning/tasks?branchId=${branch}`}>Danh sách công việc</Link>
      {loading && <p role="status">Đang tải chi tiết…</p>}
      {error && (
        <p role="alert">
          {error}{" "}
          <button onClick={() => setRetry((v) => v + 1)}>Thử lại</button>
        </p>
      )}
      {result && (
        <>
          {kind === "cleaning" && (
            <CleaningCancellationPanel
              key={`${branch}/${id}`}
              branch={branch}
              task={id}
            />
          )}
          <h2>
            {result.item.taskNumber} · {result.item.roomNumber}
          </h2>
          <p>
            Chi nhánh {branch} · {result.item.kind} ·{" "}
            {result.item.workflowState ?? result.item.recordedStatus}
          </p>
          <dl>
            <dt>Nhân viên</dt>
            <dd>{result.item.assigneeName ?? "Chưa phân công"}</dd>
            <dt>Tiến độ</dt>
            <dd>
              {result.item.progressPercent === null
                ? "—"
                : `${result.item.progressPercent}%`}
            </dd>
            <dt>Hạn UTC</dt>
            <dd>{result.item.dueAtUtc ?? "—"}</dd>
            <dt>Mô tả</dt>
            <dd>{result.item.description ?? "—"}</dd>
            <dt>Blocker</dt>
            <dd>
              {result.item.hasUnresolvedBlocker == null
                ? "Chưa xác định"
                : result.item.hasUnresolvedBlocker
                  ? "Chưa xử lý"
                  : "Không"}{" "}
              {result.item.blockerCode ?? "—"} {result.item.blockerNote}
            </dd>
            <dt>Chu kỳ hiện tại</dt>
            <dd>
              {result.item.isCurrentCycle == null
                ? "Chưa xác định"
                : result.item.isCurrentCycle
                  ? "Có"
                  : "Không"}
            </dd>
          </dl>
          <p>
            Readiness phòng hiện tại:{" "}
            {result.item.effectiveReadiness ?? "Chưa xác định"} ·{" "}
            {result.item.readinessCoverage ?? "UNAVAILABLE"}
          </p>
          <Link
            to={`/rooms/turnover?branchId=${branch}&roomId=${result.item.roomId}`}
          >
            Lịch sử turnover của phòng
          </Link>
          {kind === "cleaning" && (
            <Link to={`/cleaning/tasks/${id}?branchId=${branch}`}>
              Mở UC032 · tiến độ của người được phân công
            </Link>
          )}
          {kind === "cleaning" && (
            <p>
              <Link to={`/cleaning/assignment?branchId=${branch}&taskId=${id}`}>
                Xem phương án phân công
              </Link>
            </p>
          )}
          <h2>
            Phân công {kind === "rework" ? "cleaning cycle " : ""}(
            {result.assignmentCount})
          </h2>
          {result.assignments.length === 0 && (
            <p>Không có phân công trên trang này.</p>
          )}
          <ul>
            {result.assignments.map((r) => (
              <li key={r.id}>
                {r.assigneeId} · {r.status} · {r.assignedAtUtc} · {r.reason}
              </li>
            ))}
          </ul>
          <h2>
            Tiến độ {kind === "rework" ? "cleaning cycle " : ""}(
            {result.progressCount})
          </h2>
          {result.progress.length === 0 && (
            <p>Không có tiến độ trên trang này.</p>
          )}
          <ul>
            {result.progress.map((r) => (
              <li key={r.id}>
                {r.percent}% · {r.loggedAtUtc} · {r.status} · {r.blockerCode} ·{" "}
                {r.note}
              </li>
            ))}
          </ul>
          <h2>Audit ({result.auditCount})</h2>
          {result.audit.length === 0 && <p>Không có audit trên trang này.</p>}
          <ul>
            {result.audit.map((r) => (
              <li key={r.id}>
                {r.action} · {r.entityType} · {r.actorId ?? "—"} ·{" "}
                {r.occurredAtUtc}
              </li>
            ))}
          </ul>
          <button disabled={page <= 1} onClick={() => setPage((v) => v - 1)}>
            Timeline trước
          </button>
          <span> Trang {page} </span>
          <button
            disabled={
              page * result.timelineLimit >=
              Math.max(
                result.assignmentCount,
                result.progressCount,
                result.auditCount,
              )
            }
            onClick={() => setPage((v) => v + 1)}
          >
            Timeline sau
          </button>
        </>
      )}
    </>
  );
}
