import {
  announceWorkflowSuccess,
  ActionDialog,
} from "../components/workspace-interactions";
import { useEffect, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import { isObject } from "../auth/http";
import { IncidentEvidencePanel } from "./IncidentEvidencePanel";
import type { Values } from "./contracts";

export function MaintenanceSubmissionPanel({
  data,
  onCommitted,
}: {
  data: Values;
  onCommitted?: (message: string) => void;
}) {
  const { state } = useAuth();
  const session = state.session;
  const [bound] = useState(session);
  const current = useRef(session);
  current.current = session;
  const [diagnosis, setDiagnosis] = useState(""),
    [action, setAction] = useState(""),
    [note, setNote] = useState("");
  const [selected, setSelected] = useState<{ id: number; name: string }[]>([]);
  const [review, setReview] = useState(false),
    [busy, setBusy] = useState(false),
    [attempted, setAttempted] = useState(false);
  const [message, setMessage] = useState("");
  const [evidenceBusy, setEvidenceBusy] = useState(false);
  const sending = useRef(false),
    writer = useRef<AbortController | null>(null);
  useEffect(() => () => writer.current?.abort(), [session, data]);
  const id = data.maintenanceWorkId,
    incident = data.incidentId,
    tag = data.executionETag,
    actor = Number(session?.user.id);
  if (
    !session ||
    session !== bound ||
    data.canSubmit !== true ||
    data.status !== "IN_PROGRESS" ||
    typeof id !== "number" ||
    !Number.isSafeInteger(id) ||
    id <= 0 ||
    typeof incident !== "number" ||
    !Number.isSafeInteger(incident) ||
    incident <= 0 ||
    !Number.isSafeInteger(actor) ||
    actor <= 0 ||
    typeof tag !== "string" ||
    !/^"[0-9A-F]{16}"$/.test(tag)
  )
    return null;
  const valid = diagnosis.trim().length > 0 && action.trim().length > 0;
  async function submit() {
    if (
      !valid ||
      evidenceBusy ||
      sending.current ||
      attempted ||
      current.current !== session
    )
      return;
    sending.current = true;
    setBusy(true);
    setAttempted(true);
    setMessage("");
    const abort = new AbortController();
    writer.current = abort;
    try {
      const bootstrap = await fetch("/api/auth/csrf", {
        credentials: "same-origin",
        cache: "no-store",
        redirect: "error",
        signal: abort.signal,
      });
      if (!bootstrap.ok)
        throw Error(
          "Không xác minh được phiên đăng nhập. Tải lại trước khi thử lại.",
        );
      const csrf: unknown = await bootstrap.json();
      if (
        !isObject(csrf) ||
        csrf.headerName !== "X-CSRF-TOKEN" ||
        typeof csrf.csrfToken !== "string" ||
        !/^[\x21-\x7e]+$/.test(csrf.csrfToken) ||
        abort.signal.aborted ||
        current.current !== session
      )
        throw Error("Phiên đăng nhập đã thay đổi. Tải lại trước khi thử lại.");
      const response = await fetch(
        `/api/maintenance-works/${id}/submit-result`,
        {
          method: "POST",
          credentials: "same-origin",
          cache: "no-store",
          redirect: "error",
          signal: abort.signal,
          headers: {
            "Content-Type": "application/json",
            "X-CSRF-TOKEN": csrf.csrfToken,
            "If-Match": tag as string,
          },
          body: JSON.stringify({
            actorUserId: actor,
            diagnosis: diagnosis.trim(),
            actionTaken: action.trim(),
            resolutionNote: note.trim() || null,
            resultAttachmentIds: selected.map((x) => x.id),
          }),
        },
      );
      if (response.status === 400) {
        const problem: unknown = await response.json();
        if (
          isObject(problem) &&
          problem.title === "Maintenance validation failed" &&
          typeof problem.detail === "string"
        ) {
          if (!abort.signal.aborted && current.current === session)
            setAttempted(false);
          throw Error(
            problem.detail +
              "Sửa kết quả hoặc tải lại công việc trước khi xem xét lại.",
          );
        }
      }
      if (!response.ok)
        throw Error(
          response.status === 412
            ? "Công việc đã đổi. Tải lại trước khi nộp tiếp."
            : "Chưa xác nhận nộp. Tải lại và kiểm tra kết quả, vật tư, bằng chứng và quyền hiện tại.",
        );
      const result: unknown = await response.json();
      if (
        !isObject(result) ||
        result.maintenanceWorkId !== id ||
        result.incidentId !== incident ||
        result.assigneeId !== actor ||
        result.status !== "COMPLETED" ||
        result.submissionWorkflow !== "PENDING_VERIFICATION" ||
        typeof result.executionETag !== "string" ||
        !/^"[0-9A-F]{16}"$/.test(result.executionETag) ||
        result.executionETag === tag ||
        typeof result.resolvedAt !== "string" ||
        !Number.isFinite(Date.parse(result.resolvedAt))
      )
        throw Error("Chưa xác minh phản hồi nộp. Tải lại trạng thái hiện tại.");
      if (!abort.signal.aborted && current.current === session) {
        const notice =
          "Đã nộp kết quả để xác minh. Khoảng làm việc đã kết thúc.";
        setMessage(notice);
        announceWorkflowSuccess(notice, "dev2/MaintenanceSubmissionPanel.tsx");
        onCommitted?.(notice);
      }
    } catch (error) {
      if (!abort.signal.aborted && current.current === session)
        setMessage(
          error instanceof Error
            ? error.message
            : "Chưa xác nhận gửi kết quả. Vui lòng tải lại.",
        );
    } finally {
      if (!abort.signal.aborted) {
        setBusy(false);
        setReview(false);
        sending.current = false;
      }
    }
  }
  return (
    <ActionDialog
      scope="dev2/MaintenanceSubmissionPanel.tsx"
      title="Gửi kết quả bảo trì"
      busy={busy}
    >
      <section aria-label="Gửi kết quả bảo trì">
        <h3>Nộp kết quả để xác minh</h3>
        <p>
          Nộp sẽ kết thúc khoảng làm việc hiện tại. Máy chủ ghi giờ kết thúc
          thực tế. Vật tư đã dùng được đối chiếu số đã cấp và trả.
        </p>
        {message && <p role="status">{message}</p>}
        <fieldset disabled={busy || attempted || review}>
          <label>
            Chẩn đoán / nguyên nhân gốc
            <textarea
              value={diagnosis}
              onChange={(e) => setDiagnosis(e.target.value)}
            />
          </label>
          <label>
            Biện pháp đã thực hiện
            <textarea
              value={action}
              onChange={(e) => setAction(e.target.value)}
            />
          </label>
          <label>
            Ghi chú kết quả bổ sung
            <textarea value={note} onChange={(e) => setNote(e.target.value)} />
          </label>
          <IncidentEvidencePanel
            incidentId={incident}
            onBusyChange={setEvidenceBusy}
            selectedIds={selected.map((x) => x.id)}
            onSelect={(item, checked) =>
              setSelected((items) =>
                checked
                  ? [...items.filter((x) => x.id !== item.id), item]
                  : items.filter((x) => x.id !== item.id),
              )
            }
          />
        </fieldset>
        {review ? (
          <>
            <p>Xác nhận kết quả và kết thúc khoảng thời gian đang chạy?</p>
            <p>{diagnosis.trim()}</p>
            <p>{action.trim()}</p>
            <p>{note.trim()}</p>
            <p>
              Bằng chứng đã chọn:{" "}
              {selected.map((x) => x.name).join(", ") || "Không có"}
            </p>
            <button
              data-dialog-action
              disabled={busy}
              onClick={() => void submit()}
            >
              Xác nhận gửi kết quả
            </button>
            <button disabled={busy} onClick={() => setReview(false)}>
              Hủy
            </button>
          </>
        ) : (
          <button
            disabled={!valid || busy || attempted || evidenceBusy}
            onClick={() => setReview(true)}
          >
            Xem lại gửi kết quả
          </button>
        )}
      </section>
    </ActionDialog>
  );
}
