import { userMessage } from "../i18n/userMessage";
import {
  useWorkflowAvailability,
  workflowProblemCode,
} from "./WorkflowAvailability";
import { staffDate } from "./staffDate";
import {
  announceWorkflowSuccess,
  ActionDialog,
} from "../components/workspace-interactions";
import { useEffect, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";

interface View {
  requestId: number;
  status: string;
  version: string;
  percent: number | null;
  note: string | null;
  result: string | null;
  deadline: string | null;
  canUpdate: boolean;
  canChangeDeadline: boolean;
  evidence?: {
    id: number;
    name: string;
    mimeType: string;
    size: number;
    downloadPath: string;
  }[];
  evidenceCount?: number;
}
const version = (v: unknown): v is string =>
  typeof v === "string" && /^[A-Za-z0-9+/]{11}=$/.test(v);
class Failure extends Error {
  constructor(
    readonly status: number,
    readonly code?: string,
  ) {
    super("Tiến độ chưa khả dụng");
  }
}
async function json(
  path: string,
  signal: AbortSignal,
  init?: RequestInit,
): Promise<unknown> {
  const response = await fetch(path, {
    ...init,
    credentials: "same-origin",
    cache: "no-store",
    redirect: "error",
    signal,
  });
  if (!response.ok)
    throw new Failure(response.status, await workflowProblemCode(response));
  if (!response.headers.get("content-type")?.includes("application/json"))
    throw new Error("Phản hồi không hợp lệ");
  return response.json();
}
export function InternalRequestProgressPanel({
  id,
  onSaved,
}: {
  id: number;
  onSaved?: () => void;
}) {
  const { state, actions } = useAuth();
  const reportUnavailable = useWorkflowAvailability();
  const owner =
    state.status === "AUTHENTICATED" &&
    state.pending === null &&
    !state.session?.mustChangePassword
      ? state.authorityGeneration
      : null;
  const [loaded, setLoaded] = useState<{
    owner: typeof owner;
    id: number;
    view: View;
  } | null>(null);
  const [revision, setRevision] = useState(0);
  const [percent, setPercent] = useState("");
  const [note, setNote] = useState("");
  const [result, setResult] = useState("");
  const [deadline, setDeadline] = useState("");
  const [reason, setReason] = useState("");
  const [message, setMessage] = useState("");
  const [busy, setBusy] = useState(false);
  const [files, setFiles] = useState<File[]>([]);
  const sending = useRef(false);
  const controller = useRef<AbortController | null>(null);
  useEffect(() => {
    const abort = new AbortController();
    controller.current = abort;
    setLoaded(null);
    setPercent("");
    setNote("");
    setResult("");
    setDeadline("");
    setReason("");
    setFiles([]);
    setMessage("");
    setBusy(false);
    sending.current = false;
    if (owner)
      void json(`/api/internal-requests/${id}/progress`, abort.signal)
        .then((raw) => {
          const v = raw as View;
          if (
            !v ||
            v.requestId !== id ||
            typeof v.status !== "string" ||
            !version(v.version) ||
            !(
              v.percent === null ||
              (Number.isInteger(v.percent) &&
                v.percent >= 0 &&
                v.percent <= 100)
            ) ||
            ![v.note, v.result, v.deadline].every(
              (t) => t === null || typeof t === "string",
            ) ||
            (v.deadline !== null && !Number.isFinite(Date.parse(v.deadline))) ||
            typeof v.canUpdate !== "boolean" ||
            typeof v.canChangeDeadline !== "boolean"
          )
            throw new Error("Phản hồi tiến độ không hợp lệ");
          if (
            v.evidence !== undefined &&
            (!Array.isArray(v.evidence) ||
              !Number.isSafeInteger(v.evidenceCount) ||
              v.evidenceCount! < v.evidence.length ||
              v.evidence.some(
                (e) =>
                  !Number.isSafeInteger(e.id) ||
                  e.id <= 0 ||
                  typeof e.name !== "string" ||
                  typeof e.mimeType !== "string" ||
                  !Number.isSafeInteger(e.size) ||
                  e.size < 1 ||
                  e.downloadPath !==
                    `/api/internal-requests/${id}/progress/evidence/${e.id}`,
              ))
          )
            throw new Error("Phản hồi bằng chứng không hợp lệ");
          if (!abort.signal.aborted) {
            setLoaded({ owner, id, view: v });
            setPercent(String(v.percent ?? 0));
            setNote(v.note ?? "");
            setResult(v.result ?? "");
          }
        })
        .catch((e) => {
          if (abort.signal.aborted) return;
          if (reportUnavailable(e)) return;
          if (e instanceof Failure && [403, 404].includes(e.status)) return;
          if (!abort.signal.aborted) {
            setMessage(
              "Chưa tải được tiến độ. Tải lại để kiểm tra quyền hiện tại.",
            );
            if (e instanceof Failure && e.status === 401)
              actions.invalidateSession(owner!);
          }
        });
    return () => abort.abort();
  }, [id, owner, revision, actions, reportUnavailable]);
  const view =
    owner && loaded?.owner === owner && loaded.id === id ? loaded.view : null;
  async function olderEvidence() {
    const abort = controller.current;
    const before = view?.evidence?.at(-1)?.id;
    if (
      !owner ||
      !view ||
      !before ||
      !abort ||
      abort.signal.aborted ||
      sending.current
    )
      return;
    sending.current = true;
    setBusy(true);
    setMessage("");
    try {
      const next = (await json(
        `/api/internal-requests/${id}/progress?evidenceBefore=${before}`,
        abort.signal,
      )) as View;
      if (
        next.requestId !== id ||
        next.version !== view.version ||
        next.evidenceCount !== view.evidenceCount ||
        !Array.isArray(next.evidence) ||
        next.evidence.length === 0 ||
        next.evidence.length > 100 ||
        next.evidence.some(
          (e, index, list) =>
            !Number.isSafeInteger(e.id) ||
            e.id <= 0 ||
            e.id >= (index ? list[index - 1].id : before) ||
            typeof e.name !== "string" ||
            typeof e.mimeType !== "string" ||
            !Number.isSafeInteger(e.size) ||
            e.size < 1 ||
            e.downloadPath !==
              `/api/internal-requests/${id}/progress/evidence/${e.id}`,
        ) ||
        view.evidence!.length + next.evidence.length > next.evidenceCount!
      )
        throw new Error("Bằng chứng đã thay đổi");
      if (!abort.signal.aborted)
        setLoaded({
          owner,
          id,
          view: { ...view, evidence: [...view.evidence!, ...next.evidence] },
        });
    } catch (e) {
      if (!abort.signal.aborted) {
        setLoaded(null);
        setMessage(
          "Bằng chứng hoặc quyền đã đổi. Tải lại tiến độ trước khi tiếp tục.",
        );
        if (e instanceof Failure && e.status === 401)
          actions.invalidateSession(owner);
      }
    } finally {
      if (!abort.signal.aborted) {
        sending.current = false;
        setBusy(false);
      }
    }
  }
  async function save() {
    const abort = controller.current;
    if (
      !owner ||
      !view?.canUpdate ||
      view.status !== "IN_PROGRESS" ||
      !abort ||
      abort.signal.aborted ||
      sending.current
    )
      return;
    const value = Number(percent);
    if (
      !percent.trim() ||
      !Number.isInteger(value) ||
      value < 0 ||
      value > 100
    ) {
      setMessage("Nhập tiến độ từ 0 đến 100.");
      return;
    }
    if (
      deadline &&
      (!view.canChangeDeadline ||
        !reason.trim() ||
        !Number.isFinite(Date.parse(deadline + "Z")))
    ) {
      setMessage("Cần quyền quản lý bộ phận để đặt thời hạn và lý do hợp lệ.");
      return;
    }
    if (
      files.length > 5 ||
      files.some(
        (f) =>
          f.size < 1 ||
          f.size > 10 * 1024 * 1024 ||
          !["image/png", "image/jpeg", "application/pdf"].includes(f.type),
      )
    ) {
      setMessage("Chọn tối đa 5 tệp JPG, PNG hoặc PDF, tối đa 10 MB mỗi tệp.");
      return;
    }
    sending.current = true;
    setBusy(true);
    setMessage("");
    try {
      const uploads = [];
      for (const file of files) {
        const data = await new Promise<string>((resolve, reject) => {
          const reader = new FileReader();
          reader.onerror = () => reject(new Error("Không đọc được tệp"));
          reader.onload = () => resolve(String(reader.result));
          reader.readAsDataURL(file);
        });
        if (abort.signal.aborted) return;
        uploads.push({
          name: file.name,
          mimeType: file.type,
          base64: data.slice(data.indexOf(",") + 1),
        });
      }
      const csrf = (await json("/api/auth/csrf", abort.signal)) as {
        csrfToken?: unknown;
      };
      if (
        typeof csrf.csrfToken !== "string" ||
        !csrf.csrfToken ||
        abort.signal.aborted
      )
        throw new Error("Kiểm tra bảo mật thất bại");
      const raw = (await json(
        `/api/internal-requests/${id}/progress`,
        abort.signal,
        {
          method: "POST",
          headers: {
            "Content-Type": "application/json",
            "X-CSRF-TOKEN": csrf.csrfToken,
            "If-Match": `"${view.version}"`,
          },
          body: JSON.stringify({
            percent: value,
            note: note.trim() || null,
            result: result.trim() || null,
            deadline:
              view.canChangeDeadline && deadline
                ? new Date(deadline + "Z").toISOString()
                : null,
            deadlineReason: reason.trim() || null,
            files: uploads,
          }),
        },
      )) as { requestId?: unknown; status?: unknown; version?: unknown };
      if (
        raw.requestId !== id ||
        raw.status !== "IN_PROGRESS" ||
        !version(raw.version)
      )
        throw new Error("Chưa xác nhận tiến độ");
      if (!abort.signal.aborted) {
        setLoaded(null);
        (announceWorkflowSuccess(
          "MSG08: Đã lưu tiến độ. Yêu cầu vẫn đang thực hiện. Tải lại để xem kết quả đã lưu.",
          "internalRequests/ProgressPanel.tsx",
        ),
          setMessage(
            "MSG08: Đã lưu tiến độ. Yêu cầu vẫn đang thực hiện. Tải lại để xem kết quả đã lưu.",
          ));
        onSaved?.();
      }
    } catch (e) {
      if (!abort.signal.aborted) {
        setLoaded(null);
        setMessage(
          e instanceof Failure && e.status === 412
            ? "Yêu cầu đã đổi. Tải lại tiến độ trước khi lưu."
            : e instanceof Failure && e.status === 403
              ? "MSG02: Quyền hiện tại không cho phép cập nhật. Tải lại tiến độ để kiểm tra phân công và quyền đổi hạn."
              : "Chưa xác nhận tiến độ. Tải lại trước khi thử lại.",
        );
        if (e instanceof Failure && e.status === 401)
          actions.invalidateSession(owner);
      }
    } finally {
      if (!abort.signal.aborted) {
        sending.current = false;
        setBusy(false);
      }
    }
  }
  if (!owner) return null;
  if (!view)
    return message ? (
      <p role="status">
        {userMessage(message)}{" "}
        <button onClick={() => setRevision((v) => v + 1)}>Thử lại</button>
      </p>
    ) : null;
  return (
    <ActionDialog
      scope="internalRequests/ProgressPanel.tsx"
      title={view.canUpdate ? "Cập nhật tiến độ" : "Xem tiến độ"}
      busy={busy}
    >
      <section aria-label="Tiến độ yêu cầu">
        <h3>Tiến độ và kết quả</h3>
        {message && <p role="status">{userMessage(message)}</p>}
        <button disabled={busy} onClick={() => setRevision((v) => v + 1)}>
          Tải lại tiến độ
        </button>
        {view && (
          <>
            <p>
              Tiến độ đã lưu:{" "}
              {view.percent === null ? "Chưa ghi nhận" : `${view.percent}%`}
            </p>
            {view.note && <p>{view.note}</p>}
            {view.result && <p>{view.result}</p>}
            {view.deadline && <p>Hạn xử lý: {staffDate(view.deadline)}</p>}
            {!!view.evidence?.length && (
              <>
                <h4>Tệp đính kèm</h4>
                <ul>
                  {view.evidence.map((e) => (
                    <li key={e.id}>
                      <a href={e.downloadPath} download>
                        {e.name}
                      </a>{" "}
                      ({e.size} bytes)
                    </li>
                  ))}
                </ul>
                {view.evidenceCount! > view.evidence.length && (
                  <>
                    <p>
                      Đang hiển thị {view.evidence.length} trên{" "}
                      {view.evidenceCount} tệp.
                    </p>
                    <button
                      disabled={busy}
                      onClick={() => void olderEvidence()}
                    >
                      Tải tệp đính kèm trước đó
                    </button>
                  </>
                )}
              </>
            )}
            {view.canUpdate && view.status === "IN_PROGRESS" && (
              <div aria-busy={busy}>
                <label>
                  Phần trăm tiến độ
                  <input
                    type="number"
                    min={0}
                    max={100}
                    step={1}
                    disabled={busy}
                    value={percent}
                    onChange={(e) => setPercent(e.target.value)}
                  />
                </label>
                <label>
                  Ghi chú tiến độ
                  <textarea
                    aria-label="Ghi chú tiến độ"
                    maxLength={4000}
                    disabled={busy}
                    value={note}
                    onChange={(e) => setNote(e.target.value)}
                  />
                </label>
                <label>
                  Ghi chú kết quả
                  <textarea
                    aria-label="Ghi chú kết quả"
                    maxLength={4000}
                    disabled={busy}
                    value={result}
                    onChange={(e) => setResult(e.target.value)}
                  />
                </label>
                <label>
                  Tệp đính kèm tiến độ
                  <input
                    aria-label="Tệp đính kèm tiến độ"
                    type="file"
                    accept=".jpg,.jpeg,.png,.pdf"
                    multiple
                    disabled={busy}
                    onChange={(e) => setFiles(Array.from(e.target.files ?? []))}
                  />
                </label>
                {view.canChangeDeadline && (
                  <>
                    <label>
                      Thời hạn mới (UTC)
                      <input
                        type="datetime-local"
                        disabled={busy}
                        value={deadline}
                        onChange={(e) => setDeadline(e.target.value)}
                      />
                    </label>
                    <label>
                      Lý do đổi thời hạn
                      <textarea
                        aria-label="Lý do đổi thời hạn"
                        maxLength={4000}
                        disabled={busy}
                        value={reason}
                        onChange={(e) => setReason(e.target.value)}
                      />
                    </label>
                  </>
                )}
                <p>Đạt 100% là sẵn sàng để xét duyệt, không tự đóng yêu cầu.</p>
                <button
                  data-dialog-action
                  disabled={busy}
                  onClick={() => void save()}
                >
                  Lưu tiến độ
                </button>
              </div>
            )}
          </>
        )}
      </section>
    </ActionDialog>
  );
}
