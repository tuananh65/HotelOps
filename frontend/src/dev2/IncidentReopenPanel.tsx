import {
  announceWorkflowSuccess,
  ActionDialog,
} from "../components/workspace-interactions";
import { useEffect, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import { isObject } from "../auth/http";
import type { Values } from "./contracts";
import { Link } from "react-router-dom";

type Context = {
  incidentId: number;
  previousWorkId: number;
  closedAt: string;
  closureNote: string;
  evaluatedAt: string;
  windowDays: number;
  version: string;
};
const positive = (v: unknown): v is number =>
  typeof v === "number" && Number.isSafeInteger(v) && v > 0;
const date = (v: unknown): v is string =>
  typeof v === "string" && Number.isFinite(Date.parse(v));

export function IncidentReopenPanel({
  data,
  onCommitted,
}: {
  data: Values;
  onCommitted?: (notice: string) => void;
}) {
  const { state } = useAuth();
  const owner =
    state.status === "AUTHENTICATED" && !state.pending
      ? state.authorityGeneration
      : null;
  const mountedOwner = useRef(owner).current;
  const currentOwner = useRef(owner);
  currentOwner.current = owner;
  const id = data.incidentId;
  const [context, setContext] = useState<Context>();
  const [error, setError] = useState("");
  const [expired, setExpired] = useState(false);
  const [reason, setReason] = useState(""),
    [note, setNote] = useState(""),
    [discovery, setDiscovery] = useState("");
  const [files, setFiles] = useState<File[]>([]);
  const [review, setReview] = useState(false),
    [busy, setBusy] = useState(false),
    [attempted, setAttempted] = useState(false);
  const [message, setMessage] = useState("");
  const sending = useRef(false),
    writer = useRef<AbortController | null>(null);
  useEffect(() => {
    if (
      !owner ||
      owner !== mountedOwner ||
      !positive(id) ||
      data.status !== "CLOSED"
    )
      return;
    const abort = new AbortController();
    void (async () => {
      try {
        const response = await fetch(`/api/incidents/${id}/reopen`, {
          credentials: "same-origin",
          cache: "no-store",
          redirect: "error",
          signal: abort.signal,
        });
        if (response.status === 403 || response.status === 404) return;
        if (!response.ok) {
          const problem: unknown = await response.json().catch(() => null);
          const windowExpired =
            response.status === 409 &&
            isObject(problem) &&
            problem.code === "INCIDENT_REOPEN_WINDOW_EXPIRED";
          if (!abort.signal.aborted && currentOwner.current === owner)
            setExpired(windowExpired);
          throw Error(
            windowExpired
              ? "Đã hết thời hạn mở lại. Báo sự cố mới cho lần tái phát này."
              : response.status === 409
                ? "Không thể mở lại: bằng chứng đóng bắt buộc không cho phép mở lại."
                : "Chưa thể mở lại. Tải lại để kiểm tra phiên và cấu hình.",
          );
        }
        const raw: unknown = await response.json();
        if (
          !isObject(raw) ||
          raw.incidentId !== id ||
          !positive(raw.previousWorkId) ||
          !date(raw.closedAt) ||
          !date(raw.evaluatedAt) ||
          typeof raw.closureNote !== "string" ||
          !positive(raw.windowDays) ||
          typeof raw.version !== "string" ||
          !/^"[0-9A-F]{16}"$/.test(raw.version)
        )
          throw Error("Chưa xác minh được chi tiết mở lại. Tải lại.");
        if (!abort.signal.aborted && currentOwner.current === owner)
          setContext(raw as Context);
      } catch (e) {
        if (!abort.signal.aborted && currentOwner.current === owner)
          setError(e instanceof Error ? e.message : "Mở lại chưa khả dụng.");
      }
    })();
    return () => {
      abort.abort();
      writer.current?.abort();
    };
  }, [owner, mountedOwner, id, data.status]);

  const instant = new Date(discovery);
  const valid =
    context &&
    reason.trim().length >= 10 &&
    reason.length <= 4000 &&
    note.length <= 4000 &&
    (note.trim().length > 0 || files.length > 0) &&
    files.length <= 5 &&
    files.every(
      (f) =>
        f.size > 0 &&
        f.size <= 10 * 1024 * 1024 &&
        ["image/jpeg", "image/png", "application/pdf"].includes(f.type),
    ) &&
    Number.isFinite(instant.getTime()) &&
    instant.getTime() >= Date.parse(context.closedAt) &&
    instant.getTime() <= Date.now();
  async function submit() {
    if (
      !owner ||
      currentOwner.current !== owner ||
      !context ||
      !valid ||
      !review ||
      sending.current ||
      attempted
    )
      return;
    sending.current = true;
    setBusy(true);
    setAttempted(true);
    const abort = new AbortController();
    writer.current = abort;
    try {
      const uploads = await Promise.all(
        files.map(async (f) => {
          const bytes = new Uint8Array(await f.arrayBuffer());
          let binary = "";
          for (let offset = 0; offset < bytes.length; offset += 8192)
            binary += String.fromCharCode(
              ...bytes.subarray(offset, offset + 8192),
            );
          return { name: f.name, mimeType: f.type, base64: btoa(binary) };
        }),
      );
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
        currentOwner.current !== owner
      )
        throw Error("Phiên đăng nhập đã thay đổi. Vui lòng tải lại.");
      const response = await fetch(`/api/incidents/${id}/reopen`, {
        method: "POST",
        credentials: "same-origin",
        cache: "no-store",
        redirect: "error",
        signal: abort.signal,
        headers: {
          "Content-Type": "application/json",
          "X-CSRF-TOKEN": csrf.csrfToken,
          "If-Match": context.version,
        },
        body: JSON.stringify({
          reason: reason.trim(),
          recurrenceDiscoveredAt: instant.toISOString(),
          evidence: { note: note.trim(), files: uploads },
        }),
      });
      if (!response.ok) {
        if (response.status === 400) {
          const problem: unknown = await response.json().catch(() => null);
          // Only the controller's known validation rejection is safe to edit and
          // retry. Unknown outcomes retain the reload barrier against duplication.
          if (
            isObject(problem) &&
            problem.title === "Invalid incident reopening" &&
            !abort.signal.aborted &&
            currentOwner.current === owner
          ) {
            setAttempted(false);
            throw Error(
              typeof problem.detail === "string"
                ? problem.detail
                : "Kiểm tra thời điểm tái phát, lý do và bằng chứng.",
            );
          }
        }
        throw Error(
          response.status === 412
            ? "Sự cố đã thay đổi. Tải lại trước khi thử lại."
            : "Chưa xác nhận mở lại. Tải lại sự cố để kiểm tra trạng thái và quyền trước khi thử lại.",
        );
      }
      const result: unknown = await response.json();
      if (
        !isObject(result) ||
        result.incidentId !== id ||
        !positive(result.eventId) ||
        result.workflow !== "PENDING_TRIAGE" ||
        typeof result.version !== "string" ||
        !/^"[0-9A-F]{16}"$/.test(result.version)
      )
        throw Error("Chưa xác minh được phản hồi mở lại. Tải lại sự cố.");
      if (!abort.signal.aborted && currentOwner.current === owner) {
        const notice =
          "Đã mở lại sự cố ở trạng thái chờ tiếp nhận. Lịch sử trước giữ nguyên. Phân loại, tiếp nhận và phân công chu kỳ mới.";
        setMessage(notice);
        announceWorkflowSuccess(notice, "dev2/IncidentReopenPanel.tsx");
        onCommitted?.(notice);
      }
    } catch (e) {
      if (!abort.signal.aborted && currentOwner.current === owner)
        setMessage(
          e instanceof Error
            ? e.message
            : "Chưa xác nhận mở lại. Vui lòng tải lại.",
        );
    } finally {
      if (!abort.signal.aborted) {
        setBusy(false);
        setReview(false);
        sending.current = false;
      }
    }
  }
  if (
    !owner ||
    owner !== mountedOwner ||
    !positive(id) ||
    data.status !== "CLOSED"
  )
    return null;
  if (!context)
    return error ? (
      <div role="status">
        <p>{error}</p>
        {expired && (
          <Link
            to={`/operations/incidents?action=A004A-021${positive(data.branchId) ? `&branchId=${data.branchId}` : ""}`}
          >
            Báo sự cố mới
          </Link>
        )}
      </div>
    ) : null;
  return (
    <ActionDialog
      scope="dev2/IncidentReopenPanel.tsx"
      title="Mở lại sự cố"
      busy={busy}
    >
      <section aria-label="Mở lại sự cố">
        <h3>Mở lại sự cố</h3>
        <p>
          Đã đóng: {new Date(context.closedAt).toLocaleString()}. Reopen window:{" "}
          {context.windowDays} ngày sau khi đóng.
        </p>
        <p>{context.closureNote}</p>
        <fieldset disabled={busy || attempted || review}>
          <label>
            Phát hiện tái phát lúc (giờ địa phương)
            <input
              type="datetime-local"
              step="1"
              value={discovery}
              onChange={(e) => setDiscovery(e.target.value)}
            />
          </label>
          <label>
            Lý do (ít nhất 10 ký tự)
            <textarea
              maxLength={4000}
              value={reason}
              onChange={(e) => setReason(e.target.value)}
            />
          </label>
          <label>
            Bằng chứng tái phát
            <textarea
              maxLength={4000}
              value={note}
              onChange={(e) => setNote(e.target.value)}
            />
          </label>
          <label>
            Tệp bằng chứng (tối đa 5 tệp JPG, PNG hoặc PDF; 10 MiB mỗi tệp)
            <input
              type="file"
              multiple
              accept=".jpg,.jpeg,.png,.pdf"
              onChange={(e) => setFiles(Array.from(e.target.files ?? []))}
            />
          </label>
        </fieldset>
        {message && <p role="status">{message}</p>}
        {review ? (
          <>
            <p>
              Bắt đầu chu kỳ xử lý mới với thời điểm tái phát đã xác nhận? Hồ sơ
              sửa chữa trước giữ nguyên; SLA mới đặt khi tiếp nhận.
            </p>
            <button
              data-dialog-action
              disabled={busy || attempted}
              onClick={() => void submit()}
            >
              Xác nhận mở lại
            </button>
            <button disabled={busy} onClick={() => setReview(false)}>
              Quay lại
            </button>
          </>
        ) : (
          <button
            disabled={!valid || busy || attempted}
            onClick={() => setReview(true)}
          >
            Xem lại mở lại
          </button>
        )}
      </section>
    </ActionDialog>
  );
}
