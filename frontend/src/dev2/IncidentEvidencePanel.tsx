import { referenceLabel } from "../i18n/referenceLabel";
import { userMessage } from "../i18n/userMessage";
import {
  announceWorkflowSuccess,
  ActionDialog,
} from "../components/workspace-interactions";
import { useEffect, useRef, useState } from "react";
import { createHttpClient, isObject } from "../auth/http";
import { useAuth } from "../auth/AuthProvider";
import { staffDate } from "../internalRequests/staffDate";

type Item = {
  id: number;
  name: string;
  note: string | null;
  authorId: number;
  uploadedAt: string;
};
type Page = {
  incidentId: number;
  page: number;
  total: number;
  items: Item[];
  version: string;
  canAdd: boolean;
  noteTotal: number;
  notes: { id: number; note: string | null; authorId: number; at: string }[];
};
const request = createHttpClient();
type Props = {
  incidentId: number;
  readOnly?: boolean;
  selectedIds?: number[];
  onSelect?: (item: { id: number; name: string }, selected: boolean) => void;
  onBusyChange?: (busy: boolean) => void;
};
export function IncidentEvidencePanel(props: Props) {
  const { state } = useAuth();
  if (
    state.status !== "AUTHENTICATED" ||
    state.pending ||
    !Number.isSafeInteger(props.incidentId) ||
    props.incidentId <= 0
  )
    return null;
  return (
    <BoundIncidentEvidencePanel
      key={state.authorityGeneration + ":" + props.incidentId}
      {...props}
    />
  );
}
function BoundIncidentEvidencePanel({
  incidentId,
  selectedIds = [],
  onSelect,
  onBusyChange,
  readOnly = false,
}: Props) {
  const [page, setPage] = useState(1);
  const [revision, setRevision] = useState(0);
  const [loaded, setLoaded] = useState<{
    key: string;
    data?: Page;
    error?: string;
  }>();
  const [note, setNote] = useState("");
  const [files, setFiles] = useState<File[]>([]);
  const [message, setMessage] = useState("");
  const [busy, setBusy] = useState(false);
  useEffect(() => {
    onBusyChange?.(busy);
  }, [busy, onBusyChange]);
  const sending = useRef(false);
  const writer = useRef<AbortController | null>(null);
  useEffect(() => () => writer.current?.abort(), []);
  const key = `${incidentId}:${page}:${revision}`;
  useEffect(() => {
    const abort = new AbortController();
    void request<Page>({
      method: "GET",
      url: `/api/incidents/${incidentId}/evidence?page=${page}`,
      expectedStatus: 200,
      signal: abort.signal,
      decode: (raw) => {
        if (
          !isObject(raw) ||
          raw.incidentId !== incidentId ||
          raw.page !== page ||
          !Number.isSafeInteger(raw.total) ||
          typeof raw.version !== "string" ||
          !/^[A-Za-z0-9+/]{43}=$/.test(raw.version) ||
          typeof raw.canAdd !== "boolean" ||
          !Number.isSafeInteger(raw.noteTotal) ||
          Number(raw.noteTotal) < 0 ||
          !Array.isArray(raw.notes) ||
          raw.notes.length !==
            Math.min(
              25,
              Math.max(0, Number(raw.noteTotal) - (page - 1) * 25),
            ) ||
          raw.notes.some(
            (n) =>
              !isObject(n) ||
              !Number.isSafeInteger(n.id) ||
              Number(n.id) <= 0 ||
              !(n.note === null || typeof n.note === "string") ||
              !Number.isSafeInteger(n.authorId) ||
              Number(n.authorId) <= 0 ||
              typeof n.at !== "string" ||
              !Number.isFinite(Date.parse(n.at)),
          ) ||
          Number(raw.total) < 0 ||
          !Array.isArray(raw.items) ||
          raw.items.length !==
            Math.min(25, Math.max(0, Number(raw.total) - (page - 1) * 25))
        )
          return undefined;
        const ids = new Set<number>();
        for (const item of raw.items) {
          if (
            !isObject(item) ||
            typeof item.id !== "number" ||
            !Number.isSafeInteger(item.id) ||
            item.id <= 0 ||
            ids.has(item.id) ||
            typeof item.name !== "string" ||
            !(item.note === null || typeof item.note === "string") ||
            typeof item.authorId !== "number" ||
            !Number.isSafeInteger(item.authorId) ||
            item.authorId <= 0 ||
            typeof item.uploadedAt !== "string" ||
            !Number.isFinite(Date.parse(item.uploadedAt))
          )
            return undefined;
          ids.add(item.id);
        }
        return raw as unknown as Page;
      },
    }).then((result) => {
      if (!abort.signal.aborted)
        setLoaded({
          key,
          ...(result.ok
            ? { data: result.data }
            : {
                error:
                  "Bằng chứng sự cố chưa khả dụng. Tải lại để kiểm tra quyền.",
              }),
        });
    });
    return () => abort.abort();
  }, [incidentId, page, key]);
  const current = loaded?.key === key ? loaded : undefined;
  async function save() {
    if (readOnly || !current?.data?.canAdd || sending.current) return;
    if (
      (!note.trim() && !files.length) ||
      note.trim().length > 4000 ||
      files.length > 5 ||
      files.some(
        (f) =>
          f.size < 1 ||
          f.size > 10 * 1024 * 1024 ||
          !["image/jpeg", "image/png", "application/pdf"].includes(f.type),
      )
    ) {
      setMessage(
        "Nhập ghi chú hoặc tối đa 5 tệp JPG, PNG, PDF; mỗi tệp tối đa 10 MiB.",
      );
      return;
    }
    const abort = new AbortController();
    writer.current = abort;
    sending.current = true;
    setBusy(true);
    setMessage("");
    let inputRejected = false,
      closed = false;
    try {
      const uploads = await Promise.all(
        files.map(async (file) => {
          const bytes = new Uint8Array(await file.arrayBuffer());
          let binary = "";
          for (let offset = 0; offset < bytes.length; offset += 8192)
            binary += String.fromCharCode(
              ...bytes.subarray(offset, offset + 8192),
            );
          return { name: file.name, mimeType: file.type, base64: btoa(binary) };
        }),
      );
      const csrf = await request({
        method: "GET",
        url: "/api/auth/csrf",
        expectedStatus: 200,
        signal: abort.signal,
        decode: (value) =>
          isObject(value) &&
          value.headerName === "X-CSRF-TOKEN" &&
          typeof value.csrfToken === "string" &&
          /^[\x21-\x7e]+$/.test(value.csrfToken)
            ? value.csrfToken
            : undefined,
      });
      if (!csrf.ok || abort.signal.aborted)
        throw Error(
          "Không xác minh được phiên đăng nhập. Tải lại trước khi lưu.",
        );
      const response = await fetch(`/api/incidents/${incidentId}/evidence`, {
        method: "POST",
        credentials: "same-origin",
        cache: "no-store",
        redirect: "error",
        signal: abort.signal,
        headers: {
          "Content-Type": "application/json",
          "X-CSRF-TOKEN": csrf.data,
          "If-Match": `"${current.data.version}"`,
        },
        body: JSON.stringify({ note, files: uploads }),
      });
      inputRejected = response.status === 400;
      closed = response.status === 409;
      if (!response.ok)
        throw Error(
          response.status === 412
            ? "Sự cố đã thay đổi. Tải lại bằng chứng trước khi thử lại."
            : response.status === 409
              ? "MSG24: Sự cố đã đóng hoặc hủy. Bằng chứng chỉ được xem."
              : response.status === 400
                ? "MSG23: Kiểm tra ghi chú, định dạng, dung lượng và nội dung tệp trước khi thử lại."
                : "Chưa xác nhận lưu. Tải lại quyền và bằng chứng trước khi thử lại.",
        );
      const result: unknown = await response.json();
      if (
        !isObject(result) ||
        result.incidentId !== incidentId ||
        !Number.isSafeInteger(result.eventId) ||
        Number(result.eventId) <= 0 ||
        result.message !== "MSG08" ||
        typeof result.version !== "string" ||
        !/^[A-Za-z0-9+/]{43}=$/.test(result.version)
      )
        throw Error("Chưa xác minh được phản hồi lưu. Tải lại bằng chứng.");
      if (!abort.signal.aborted) {
        setNote("");
        setFiles([]);
        (announceWorkflowSuccess(
          "MSG08: Đã lưu bằng chứng.",
          "dev2/IncidentEvidencePanel.tsx",
        ),
          setMessage("MSG08: Đã lưu bằng chứng."));
        setRevision((r) => r + 1);
      }
    } catch (error) {
      if (!abort.signal.aborted) {
        const text =
          error instanceof Error
            ? error.message
            : "Chưa xác nhận lưu. Tải lại bằng chứng.";
        if (closed) setRevision((r) => r + 1);
        else if (!inputRejected) setLoaded({ key, error: text });
        setMessage(text);
      }
    } finally {
      if (!abort.signal.aborted) {
        sending.current = false;
        setBusy(false);
      }
    }
  }
  return (
    <section aria-label="Bằng chứng sự cố">
      <h3>Bằng chứng sự cố</h3>
      <button
        type="button"
        disabled={busy}
        onClick={() => setRevision((r) => r + 1)}
      >
        Tải lại bằng chứng
      </button>
      {message && <p role="status">{userMessage(message)}</p>}
      {!current && <p role="status">Đang tải bằng chứng…</p>}
      {current?.error && <p role="alert">{userMessage(current.error)}</p>}
      {current?.data && (
        <>
          {!current.data.total && <p>Chưa có tệp đính kèm.</p>}
          <ul>
            {current.data.items.map((item) => (
              <li key={item.id}>
                {onSelect && (
                  <label>
                    <input
                      type="checkbox"
                      checked={selectedIds.includes(item.id)}
                      onChange={(e) =>
                        onSelect(
                          { id: item.id, name: item.name },
                          e.target.checked,
                        )
                      }
                    />
                    Sử dụng {item.name} làm bằng chứng kết quả
                  </label>
                )}
                <a
                  href={`/api/incidents/${incidentId}/evidence/${item.id}`}
                  download
                >
                  {item.name}
                </a>
                <p>
                  Người tải lên {referenceLabel(item, "authorId")} lúc {staffDate(item.uploadedAt)}
                </p>
                {item.note && <p>{item.note}</p>}
              </li>
            ))}
          </ul>
          <ul>
            {current.data.notes.map((n) => (
              <li key={n.id}>
                 {n.note ?? "Tệp đã thêm"} — {referenceLabel(n, "authorId")}, {staffDate(n.at)}
              </li>
            ))}
          </ul>
          <button
            type="button"
            disabled={busy || page <= 1}
            onClick={() => setPage((p) => p - 1)}
          >
            Bằng chứng trước
          </button>
          <span> Trang {page} </span>
          <button
            type="button"
            disabled={
              busy ||
              page * 25 >= Math.max(current.data.total, current.data.noteTotal)
            }
            onClick={() => setPage((p) => p + 1)}
          >
            Bằng chứng tiếp theo
          </button>
          {!readOnly && current.data.canAdd && (
            <ActionDialog
              scope="dev2/IncidentEvidencePanel.tsx"
              title="Thêm bằng chứng sự cố"
              busy={busy}
            >
              <fieldset disabled={busy}>
                <legend>Thêm bằng chứng</legend>
                <label>
                  Ghi chú
                  <textarea
                    maxLength={4000}
                    value={note}
                    onChange={(e) => setNote(e.target.value)}
                  />
                </label>
                <label>
                  Tệp
                  <input
                    key={revision}
                    type="file"
                    multiple
                    accept=".jpg,.jpeg,.png,.pdf"
                    onChange={(e) => setFiles(Array.from(e.target.files ?? []))}
                  />
                </label>
                {files.length > 0 && (
                  <p>
                    Tệp đã chọn: {files.map((file) => file.name).join(", ")}
                  </p>
                )}
                <button type="button" onClick={() => void save()}>
                  {busy ? "Đang lưu…" : "Lưu bằng chứng"}
                </button>
              </fieldset>
            </ActionDialog>
          )}
        </>
      )}
    </section>
  );
}
