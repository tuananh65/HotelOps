import { useEffect, useRef, useState } from "react";
import { Link, useParams, useSearchParams } from "react-router-dom";
import { useAuth } from "../../auth/AuthProvider";
import { ApiError } from "../../api/dev1/client";
import { inspectionAssignmentRejection } from "../../api/dev1/inspectionReassignment";
import { InspectionReassignmentForm } from "./InspectionReassignmentForm";
import {
  assignReinspection,
  readReinspectionOptions,
  type ReinspectionOptions,
} from "../../api/dev1/reinspectionAssignment";

export function ReinspectionAssignmentPage() {
  const { state } = useAuth();
  const { id = "" } = useParams();
  const [params] = useSearchParams();
  const branch = params.get("branchId") ?? "";
  const identity = useRef(state.authorityGeneration);
  const revision = useRef(0);
  if (identity.current !== state.authorityGeneration) {
    identity.current = state.authorityGeneration;
    ++revision.current;
  }
  return (
    <Assignment
      key={`${branch}:${id}:${revision.current}`}
      branch={branch}
      id={id}
    />
  );
}

function Assignment({ branch, id }: { branch: string; id: string }) {
  const { state, actions } = useAuth();
  const owner =
    state.status === "AUTHENTICATED" &&
    state.pending === null &&
    state.session &&
    !state.session.mustChangePassword
      ? state.authorityGeneration
      : null;
  const [refresh, setRefresh] = useState(0);
  const key = `${branch}:${id}:${refresh}`;
  const current = useRef({ owner, key });
  current.current = { owner, key };
  const lifetime = useRef<AbortController | null>(null);
  const writing = useRef(false);
  const [result, setResult] = useState<{
    owner: typeof owner;
    key: string;
    data?: ReinspectionOptions;
    error?: string;
  } | null>(null);
  const [inspector, setInspector] = useState("");
  const [due, setDue] = useState("");
  const [reason, setReason] = useState("");
  const [review, setReview] = useState(false);
  const [busy, setBusy] = useState(false);
  const [locked, setLocked] = useState(false);
  const [message, setMessage] = useState("");
  const [saved, setSaved] = useState<{
    owner: typeof owner;
    source: string;
    id: number;
  } | null>(null);
  const sourceKey = `${branch}:${id}`;
  useEffect(() => {
    if (!owner) return;
    const abort = new AbortController();
    lifetime.current = abort;
    setResult(null);
    setReview(false);
    setInspector("");
    void readReinspectionOptions(branch, id, abort.signal)
      .then((data) => {
        if (!abort.signal.aborted) setResult({ owner, key, data });
      })
      .catch((error) => {
        if (!abort.signal.aborted) {
          setResult({
            owner,
            key,
            error: "Công việc làm lại chưa khả dụng hoặc bạn không còn quyền.",
          });
          if (error instanceof ApiError && error.status === 401)
            actions.invalidateSession(owner);
        }
      });
    return () => {
      abort.abort();
      if (lifetime.current === abort) lifetime.current = null;
    };
  }, [owner, key, branch, id, actions]);
  const visible =
    owner && result?.owner === owner && result.key === key ? result : null;
  const data = visible?.data;
  const confirmed =
    owner && saved?.owner === owner && saved.source === sourceKey
      ? saved
      : null;
  async function submit() {
    const scope = lifetime.current;
    if (
      !owner ||
      !scope ||
      scope.signal.aborted ||
      !data?.canAssign ||
      !review ||
      writing.current ||
      locked ||
      confirmed
    )
      return;
    const inspectorId = Number(inspector),
      deadline = new Date(due);
    if (
      !data.inspectors.some((p) => p.id === inspectorId) ||
      !Number.isFinite(deadline.getTime()) ||
      deadline.getTime() <= Date.now() ||
      !reason.trim() ||
      reason.trim().length > 500
    ) {
      setMessage(
        "Chọn người kiểm tra hợp lệ, hạn tương lai và lý do.",
      );
      return;
    }
    writing.current = true;
    setBusy(true);
    setMessage("");
    const isCurrent = () =>
      current.current.owner === owner &&
      current.current.key === key &&
      lifetime.current === scope;
    try {
      const assigned = await assignReinspection(
        branch,
        id,
        data.reworkETag,
        crypto.randomUUID(),
        { inspectorId, dueAt: deadline.toISOString(), reason: reason.trim() },
        { signal: scope.signal, isCurrent },
      );
      if (!scope.signal.aborted && isCurrent()) {
        setSaved({ owner, source: sourceKey, id: assigned.id });
        setLocked(true);
        setReview(false);
      }
    } catch (error) {
      if (!scope.signal.aborted && isCurrent()) {
        setLocked(true);
        setReview(false);
        setMessage(
          inspectionAssignmentRejection(error) ??
            (error instanceof ApiError &&
            [400, 403, 409, 412, 428].includes(error.status)
              ? "Phân công bị từ chối. Tải lại lần kiểm tra hiện tại; không tự gửi lại."
              : "Chưa xác nhận kết quả phân công. Tải lại để kiểm tra trạng thái; không gửi thêm phân công."),
        );
        if (error instanceof ApiError && error.status === 401)
          actions.invalidateSession(owner);
      }
    } finally {
      writing.current = false;
      if (!scope.signal.aborted && isCurrent()) setBusy(false);
    }
  }
  if (!owner) return <p role="status">Đang kiểm tra phiên đăng nhập…</p>;
  return (
    <main>
      <h1>Phân công kiểm tra lại</h1>
      <Link to="/notifications">Thông báo</Link>
      {!visible && <p role="status">Đang tải kết quả làm lại đã nộp…</p>}
      {visible?.error && <p role="alert">{visible.error}</p>}
      {confirmed && (
        <p role="status">
          <span>
            Đã phân công kiểm tra. Mã kiểm tra #{confirmed.id}.
          </span>
          <Link to={`/reinspections/${branch}/${confirmed.id}/execution`}>
            Mở kiểm tra lại
          </Link>
        </p>
      )}
      {message && <p role="alert">{message}</p>}
      {data && (
        <>
          <h2>Phòng {data.roomNumber}</h2>
          <p>
            Làm lại #{data.reworkId} · Chu kỳ vệ sinh #{data.cleaningTaskId}
          </p>
          <p>
            {data.checklistName} · Phiên bản {data.checklistVersionNumber}
          </p>
          {data.existingInspectionId !== null && (
            <p role="status">
              <span>
                MSG03: Đã có phân công kiểm tra: #
                {data.existingInspectionId}.
              </span>
              <Link
                to={`/reinspections/${branch}/${data.existingInspectionId}/execution`}
              >
                Mở kiểm tra lại
              </Link>
            </p>
          )}
          {data.existingInspectionId !== null && (
            <InspectionReassignmentForm
              key={`${key}:${data.existingInspectionId}`}
              branch={branch}
              id={data.existingInspectionId}
              reinspection
            />
          )}
          {!data.canAssign && data.existingInspectionId === null && (
            <p role="status">
              {data.unavailableReason === "CHECKLIST_UNAVAILABLE"
                ? "MSG03: Phiên bản danh sách kiểm tra gốc chưa khả dụng để phân công. Công bố phiên bản hợp lệ; không được gán lỗi gốc sang phiên bản khác."
                : data.unavailableReason === "NO_ELIGIBLE_INSPECTOR"
                  ? "Không có người kiểm tra đủ điều kiện cho chu kỳ vệ sinh này."
                  : "Quyền hiện tại không cho phép phân công."}
            </p>
          )}
          {data.canAssign && !confirmed && !locked && (
            <form
              onSubmit={(event) => {
                event.preventDefault();
                setMessage("");
                setReview(true);
              }}
            >
              <fieldset disabled={busy || review}>
                <label htmlFor="reinspection-inspector">Người kiểm tra</label>
                <select
                  id="reinspection-inspector"
                  required
                  value={inspector}
                  onChange={(event) => setInspector(event.target.value)}
                >
                  <option value="">Chọn người kiểm tra</option>
                  {data.inspectors.map((person) => (
                    <option key={person.id} value={person.id}>
                      {person.name}
                    </option>
                  ))}
                </select>
                <label>
                  Thời hạn
                  <input
                    required
                    type="datetime-local"
                    value={due}
                    onChange={(event) => setDue(event.target.value)}
                  />
                </label>
                <label>
                  Lý do phân công
                  <textarea
                    required
                    maxLength={500}
                    value={reason}
                    onChange={(event) => setReason(event.target.value)}
                  />
                </label>
                <button type="submit">Xem lại phân công</button>
              </fieldset>
              {review && (
                <section aria-label="Xem lại phân công kiểm tra lại">
                  <p>
                    Phân công đến{" "}
                    {
                      data.inspectors.find(
                        (person) => person.id === Number(inspector),
                      )?.name
                    }{" "}
                    bởi {due.replace("T", " ")}.
                  </p>
                  <p>{reason}</p>
                  <button
                    type="button"
                    disabled={busy}
                    onClick={() => void submit()}
                  >
                    Xác nhận phân công
                  </button>
                  <button
                    type="button"
                    disabled={busy}
                    onClick={() => setReview(false)}
                  >
                    Chỉnh sửa phân công
                  </button>
                </section>
              )}
            </form>
          )}
        </>
      )}
      {busy && <p role="status">Đang chờ phân công…</p>}
      <button
        type="button"
        disabled={busy}
        onClick={() => setRefresh((value) => value + 1)}
      >
        Tải lại kiểm tra lại
      </button>
    </main>
  );
}
