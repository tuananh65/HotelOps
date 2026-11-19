import { referenceLabel } from "../../i18n/referenceLabel";
import { userMessage } from "../../i18n/userMessage";
import { SearchableSelect } from "../../components/SearchableSelect";
import { ActionDialog } from "../../components/workspace-interactions";
import { useEffect, useRef, useState } from "react";
import {
  InspectionAssignmentClient,
  assignmentError,
  type AssignmentQueue,
  type AssignmentOptions,
} from "./client";
const api = new InspectionAssignmentClient();
export function InspectionAssignmentCandidate({
  branchId,
  roomId,
  onOpenInspection,
}: {
  branchId: number;
  roomId?: number;
  onOpenInspection: (id: number) => void;
}) {
  return (
    <Assignment
      key={branchId + ":" + (roomId ?? "all")}
      branchId={branchId}
      roomId={roomId}
      onOpenInspection={onOpenInspection}
    />
  );
}
function Assignment({
  branchId,
  roomId,
  onOpenInspection,
}: {
  branchId: number;
  roomId?: number;
  onOpenInspection: (id: number) => void;
}) {
  const operationId = useRef<string | undefined>(undefined);
  const [queue, setQueue] = useState<AssignmentQueue>({
      pending: [],
      assigned: [],
    }),
    [source, setSource] = useState<AssignmentOptions | null>(null),
    [busy, setBusy] = useState(false),
    [error, setError] = useState(""),
    [inspector, setInspector] = useState(""),
    [supervisor, setSupervisor] = useState(""),
    [due, setDue] = useState(""),
    [reason, setReason] = useState(""),
    [review, setReview] = useState(false),
    [confirmed, setConfirmed] = useState(false),
    [blocked, setBlocked] = useState(false);
  const optionsRequest = useRef<AbortController | null>(null);
  useEffect(() => () => optionsRequest.current?.abort(), []);
  useEffect(() => {
    const c = new AbortController();
    setBusy(true);
    api
      .queue(branchId, c.signal, roomId)
      .then((value) => {
        if (!c.signal.aborted) setQueue(value);
      })
      .catch((e) => {
        if (!c.signal.aborted) setError(assignmentError(e));
      })
      .finally(() => {
        if (!c.signal.aborted) setBusy(false);
      });
    return () => c.abort();
  }, [branchId, roomId]);
  async function open(task: number) {
    operationId.current = undefined;
    optionsRequest.current?.abort();
    const request = new AbortController();
    optionsRequest.current = request;
    setBusy(true);
    setError("");
    setReview(false);
    setConfirmed(false);
    setSource(null);
    setInspector("");
    setSupervisor("");
    setDue("");
    setReason("");
    setBlocked(true);
    try {
      const selected = queue.pending.find((t) => t.taskId === task);
      if (!selected) return;
      const fresh = await api.options(
        branchId,
        task,
        request.signal,
        roomId ?? selected.roomId,
      );
      if (request.signal.aborted) return;
      setSource(fresh);
      setInspector("");
      setSupervisor("");
      setBlocked(false);
    } catch (e) {
      if (request.signal.aborted) return;
      setError(assignmentError(e));
      setBlocked(true);
    } finally {
      if (!request.signal.aborted) setBusy(false);
    }
  }
  async function confirm() {
    if (!source || !confirmed || blocked || busy) return;
    setBusy(true);
    try {
      await api.confirm(
        branchId,
        source,
        Number(inspector),
        Number(supervisor),
        new Date(due).toISOString(),
        reason,
        true,
        undefined,
        operationId.current,
      );
    } catch (e) {
      setError(assignmentError(e));
      setBlocked(true);
    } finally {
      setBusy(false);
    }
  }
  const valid = source && inspector && supervisor && due && reason.trim();
  return (
    <section aria-label="Phân công kiểm tra">
      <h2>Phân công kiểm tra</h2>
      {busy && <p role="status">Đang tải…</p>}
      {error && <p role="alert">{userMessage(error)}</p>}
      <h3>Các lượt kiểm tra đang chờ</h3>
      {!busy && !queue.pending.length && <p>Không có công việc đang chờ.</p>}
      {queue.pending.map((t) => (
        <button disabled={busy} key={t.taskId} onClick={() => open(t.taskId)}>
          Phòng {referenceLabel(t, "roomId")} · Công việc {referenceLabel(t, "taskId")}
        </button>
      ))}
      <h3>Giao cho tôi</h3>
      {queue.assigned.map((i) => (
        <button
          key={i.inspectionId}
          onClick={() => onOpenInspection(i.inspectionId)}
        >
          Mở kiểm tra {referenceLabel(i, "inspectionId")}
        </button>
      ))}
      {source && (
        <ActionDialog
          open
          title="Phân công người kiểm tra"
          onClose={() => setSource(null)}
          busy={busy}
        >
          <div>
            <p>
              Phòng {referenceLabel(source, "roomId")}; phiên bản danh sách kiểm tra{" "}
              {referenceLabel(source, "checklistVersionId")}
            </p>
            <fieldset disabled={busy || blocked || review}>
              <label>
                Người kiểm tra
                <SearchableSelect
                  value={inspector}
                  onChange={(e) => setInspector(e.target.value)}
                >
                  <option value="">Chọn người kiểm tra</option>
                  {source.inspectors.map((p) => (
                    <option key={p.userId} value={p.userId}>
                      {p.fullName}
                    </option>
                  ))}
                </SearchableSelect>
              </label>
              <label>
                Giám sát chịu trách nhiệm
                <SearchableSelect
                  value={supervisor}
                  onChange={(e) => setSupervisor(e.target.value)}
                >
                  <option value="">Chọn rõ người giám sát</option>
                  {source.responsibleSupervisors.map((p) => (
                    <option key={p.userId} value={p.userId}>
                      {p.fullName}
                    </option>
                  ))}
                </SearchableSelect>
              </label>
              <label>
                Thời hạn
                <input
                  type="datetime-local"
                  value={due}
                  onChange={(e) => setDue(e.target.value)}
                />
              </label>
              <label>
                Lý do
                <input
                  value={reason}
                  onChange={(e) => setReason(e.target.value)}
                />
              </label>
            </fieldset>
            <button
              disabled={!valid || busy || blocked}
              onClick={() => {
                // Retain the same UUID for repeated review of this immutable source/input.
                operationId.current ??= crypto.randomUUID();
                setReview(true);
                setConfirmed(false);
              }}
            >
              Xem lại phân công
            </button>
            {review && (
              <div>
                <p>
                  Xác nhận người kiểm tra, giám sát chịu trách nhiệm, danh sách
                  kiểm tra và thời hạn đã chọn.
                </p>
                <label>
                  <input
                    type="checkbox"
                    checked={confirmed}
                    disabled={busy || blocked}
                    onChange={(e) => setConfirmed(e.target.checked)}
                  />
                  Tôi xác nhận phân công này
                </label>
                <button
                  data-dialog-action
                  disabled={!confirmed || busy || blocked}
                  onClick={confirm}
                >
                  Xác nhận phân công
                </button>
              </div>
            )}
            <p>
              Phân công thực tế chưa khả dụng; thông báo bắt buộc chưa được chấp
              nhận.
            </p>
            <button disabled={busy} onClick={() => open(source.taskId)}>
              Tải lại nguồn phân công
            </button>
          </div>
        </ActionDialog>
      )}
    </section>
  );
}
