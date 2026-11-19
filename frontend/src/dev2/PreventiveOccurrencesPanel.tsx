import { referenceLabel } from "../i18n/referenceLabel";
import { SearchInput, searchSuggestions } from "../components/SearchInput";
import { userMessage } from "../i18n/userMessage";
import { SearchableSelect } from "../components/SearchableSelect";
import {
  ActionDialog,
  CollapsibleFilterPanel,
} from "../components/workspace-interactions";
import { displayCode } from "../i18n/vi";
import { useEffect, useRef, useState } from "react";
import { PreventiveTechnicianSelect } from "./PreventiveTechnicianSelect";
import { useAuth } from "../auth/AuthProvider";
import { isObject } from "../auth/http";
import { Link } from "react-router-dom";

type Occurrence = {
  runId: number;
  scheduleId: number;
  title: string;
  instruction: string;
  status: string;
  dueAt: string;
  isOverdue: boolean;
  assigneeName?: string | null;
  roomNumber?: string | null;
  areaName?: string | null;
  assetCode?: string | null;
  assetName?: string | null;
  performedAt: string | null;
  result: string | null;
  note: string | null;
  assigneeId: number | null;
  assetId: number | null;
  roomId: number | null;
  areaId: number | null;
  version: string;
  canAssign: boolean;
  canCancel: boolean;
  canStart: boolean;
  canComplete: boolean;
  results: { key: string; passed: boolean; note: string | null }[] | null;
  checkItems: { key: string; description: string; required: boolean }[];
};
const positive = (v: unknown): v is number =>
  typeof v === "number" && Number.isSafeInteger(v) && v > 0;
type LinkedIncident = {
  incidentId: number;
  number: string;
  title: string;
  status: string;
  checkKey: string;
  linkedAt: string;
};
type History = {
  previousOccurrences: Occurrence[];
  incidents: LinkedIncident[];
};
type Evidence = { id: number; name: string; mimeType: string; size: number };
function valid(v: unknown): v is Occurrence {
  return (
    isObject(v) &&
    positive(v.runId) &&
    positive(v.scheduleId) &&
    typeof v.title === "string" &&
    typeof v.instruction === "string" &&
    typeof v.status === "string" &&
    [
      "PENDING_ASSIGNMENT",
      "ASSIGNED",
      "IN_PROGRESS",
      "ON_HOLD",
      "COMPLETED",
      "CANCELLED",
    ].includes(v.status) &&
    typeof v.isOverdue === "boolean" &&
    typeof v.dueAt === "string" &&
    Number.isFinite(Date.parse(v.dueAt)) &&
    (v.performedAt === null ||
      (typeof v.performedAt === "string" &&
        Number.isFinite(Date.parse(v.performedAt)))) &&
    [v.result, v.note].every((x) => x === null || typeof x === "string") &&
    [v.assigneeId, v.assetId, v.roomId, v.areaId].every(
      (x) => x === null || positive(x),
    ) &&
    typeof v.version === "string" &&
    /^"[0-9A-F]{16}"$/.test(v.version) &&
    typeof v.canAssign === "boolean" &&
    typeof v.canCancel === "boolean" &&
    typeof v.canStart === "boolean" &&
    typeof v.canComplete === "boolean" &&
    (v.results === null ||
      (Array.isArray(v.results) &&
        v.results.every(
          (r) =>
            isObject(r) &&
            typeof r.key === "string" &&
            typeof r.passed === "boolean" &&
            (r.note === null || typeof r.note === "string"),
        ))) &&
    Array.isArray(v.checkItems) &&
    v.checkItems.every(
      (c) =>
        isObject(c) &&
        typeof c.key === "string" &&
        typeof c.description === "string" &&
        typeof c.required === "boolean",
    )
  );
}
export function PreventiveOccurrencesPanel({
  branchId,
  runId,
}: {
  branchId: number;
  runId?: number;
}) {
  const { state } = useAuth();
  const owner =
    state.status === "AUTHENTICATED" && !state.pending
      ? state.authorityGeneration
      : null;
  const mounted = useRef(owner).current,
    current = useRef(owner);
  current.current = owner;
  const controller = useRef<AbortController | null>(null);
  useEffect(() => () => controller.current?.abort(), []);
  const [rows, setRows] = useState<Occurrence[]>([]),
    [message, setMessage] = useState("");
  const [busy, setBusy] = useState(false),
    sending = useRef(false);
  const [assignees, setAssignees] = useState<Record<number, string>>({}),
    [reasons, setReasons] = useState<Record<number, string>>({});
  const [uncertain, setUncertain] = useState(false);
  const [workdayRequired, setWorkdayRequired] = useState(false);
  const [hasMore, setHasMore] = useState(false);
  const [filterAssignee, setFilterAssignee] = useState(""),
    [keyword, setKeyword] = useState("");
  const [dueFrom, setDueFrom] = useState(""),
    [dueTo, setDueTo] = useState(""),
    [overdue, setOverdue] = useState("");
  const clearResults = () => {
    setRows([]);
    setHistory({});
    setEvidence({});
    setHasMore(false);
  };
  const [mine, setMine] = useState(true),
    [status, setStatus] = useState("OPEN");
  const [outcomes, setOutcomes] = useState<Record<string, string>>({}),
    [checkNotes, setCheckNotes] = useState<Record<string, string>>({});
  const [notes, setNotes] = useState<Record<number, string>>({});
  const [history, setHistory] = useState<Record<number, History>>({});
  const [uploads, setUploads] = useState<Record<number, File[]>>({}),
    [evidence, setEvidence] = useState<Record<number, Evidence[]>>({});
  async function loadEvidence(id: number) {
    if (!owner || owner !== mounted || sending.current) return;
    const abort = new AbortController();
    controller.current = abort;
    sending.current = true;
    setBusy(true);
    setMessage("");
    try {
      const response = await fetch(
        `/api/branches/${branchId}/preventive-occurrences/${id}/evidence`,
        {
          credentials: "same-origin",
          cache: "no-store",
          redirect: "error",
          signal: abort.signal,
        },
      );
      if (!response.ok)
        throw Error(`Bằng chứng chưa khả dụng (${response.status}).`);
      const raw: unknown = await response.json();
      if (
        !Array.isArray(raw) ||
        !raw.every(
          (e) =>
            isObject(e) &&
            positive(e.id) &&
            typeof e.name === "string" &&
            typeof e.mimeType === "string" &&
            positive(e.size),
        )
      )
        throw Error("Không xác minh được phản hồi bằng chứng.");
      if (!abort.signal.aborted && current.current === owner)
        setEvidence((old) => ({ ...old, [id]: raw as Evidence[] }));
    } catch (e) {
      if (!abort.signal.aborted && current.current === owner) {
        setEvidence({});
        setMessage(String(e));
      }
    } finally {
      sending.current = false;
      if (!abort.signal.aborted && current.current === owner) setBusy(false);
    }
  }
  async function loadHistory(id: number) {
    if (!owner || owner !== mounted || sending.current) return;
    const abort = new AbortController();
    controller.current = abort;
    sending.current = true;
    setBusy(true);
    setMessage("");
    try {
      const response = await fetch(
        `/api/branches/${branchId}/preventive-occurrences/${id}/history`,
        {
          credentials: "same-origin",
          cache: "no-store",
          redirect: "error",
          signal: abort.signal,
        },
      );
      if (!response.ok)
        throw Error(`Lịch sử chưa khả dụng (${response.status}).`);
      const raw: unknown = await response.json();
      if (
        !isObject(raw) ||
        !Array.isArray(raw.previousOccurrences) ||
        !raw.previousOccurrences.every(valid) ||
        !Array.isArray(raw.incidents) ||
        !raw.incidents.every(
          (i) =>
            isObject(i) &&
            positive(i.incidentId) &&
            [i.number, i.title, i.status, i.checkKey].every(
              (x) => typeof x === "string",
            ) &&
            typeof i.linkedAt === "string" &&
            Number.isFinite(Date.parse(i.linkedAt)),
        )
      )
        throw Error("Không xác minh được phản hồi lịch sử.");
      if (!abort.signal.aborted && current.current === owner)
        setHistory((old) => ({ ...old, [id]: raw as History }));
    } catch (e) {
      if (!abort.signal.aborted && current.current === owner) {
        setHistory({});
        setMessage(String(e));
      }
    } finally {
      sending.current = false;
      if (!abort.signal.aborted && current.current === owner) setBusy(false);
    }
  }
  function incidentUrl(row: Occurrence, key: string) {
    const query = new URLSearchParams({
      action: "A004A-021",
      branchId: String(branchId),
      occurrenceId: String(row.runId),
      occurrenceVersion: row.version,
      occurrenceCheckKey: key,
    });
    if (row.roomId) query.set("roomId", String(row.roomId));
    if (row.areaId) query.set("areaId", String(row.areaId));
    if (row.assetId) query.set("assetId", String(row.assetId));
    return `/operations/incidents?${query.toString()}`;
  }
  async function change(
    row: Occurrence,
    cancel: boolean,
    start = false,
    complete = false,
  ) {
    if (!owner || owner !== mounted || sending.current || uncertain) return;
    const assigneeId = Number(assignees[row.runId]),
      reason = reasons[row.runId]?.trim() ?? "";
    if (
      !start &&
      !complete &&
      (cancel
        ? reason.length < 10 || reason.length > 4000
        : !positive(assigneeId))
    ) {
      setMessage("Chọn kỹ thuật viên hoặc nhập lý do hủy ít nhất 10 ký tự.");
      return;
    }
    const results = row.checkItems
      .filter((c) => outcomes[`${row.runId}:${c.key}`])
      .map((c) => ({
        key: c.key,
        passed: outcomes[`${row.runId}:${c.key}`] === "PASS",
        note: checkNotes[`${row.runId}:${c.key}`] ?? null,
      }));
    if (
      complete &&
      row.checkItems.some(
        (c) => c.required && !results.some((r) => r.key === c.key),
      )
    ) {
      setMessage("Ghi kết quả cho tất cả hạng mục bắt buộc.");
      return;
    }
    const abort = new AbortController();
    controller.current = abort;
    sending.current = true;
    setBusy(true);
    setMessage("");
    let submitted = false;
    try {
      const selected = complete ? (uploads[row.runId] ?? []) : [];
      if (
        selected.length > 5 ||
        selected.some(
          (f) =>
            f.size < 1 ||
            f.size > 10 * 1024 * 1024 ||
            !["image/jpeg", "image/png", "application/pdf"].includes(f.type),
        )
      )
        throw Error(
          "Chọn tối đa 5 tệp JPG, PNG hoặc PDF, tối đa 10 MiB mỗi tệp.",
        );
      const files = await Promise.all(
        selected.map(async (file) => {
          const bytes = new Uint8Array(await file.arrayBuffer());
          let binary = "";
          for (let offset = 0; offset < bytes.length; offset += 8192)
            binary += String.fromCharCode(
              ...bytes.subarray(offset, offset + 8192),
            );
          return { name: file.name, mimeType: file.type, base64: btoa(binary) };
        }),
      );
      if (abort.signal.aborted || current.current !== owner) return;
      const bootstrap = await fetch("/api/auth/csrf", {
        credentials: "same-origin",
        cache: "no-store",
        redirect: "error",
        signal: abort.signal,
      });
      if (!bootstrap.ok) throw Error("Không xác minh được phiên đăng nhập.");
      const csrf: unknown = await bootstrap.json();
      if (
        !isObject(csrf) ||
        csrf.headerName !== "X-CSRF-TOKEN" ||
        typeof csrf.csrfToken !== "string" ||
        !/^[\x21-\x7e]+$/.test(csrf.csrfToken) ||
        abort.signal.aborted ||
        current.current !== owner
      )
        throw Error("Phiên đăng nhập đã thay đổi. Vui lòng tải lại.");
      submitted = true;
      const response = await fetch(
        `/api/branches/${branchId}/preventive-occurrences/${row.runId}/${complete ? "complete" : start ? "start" : cancel ? "cancel" : "assign"}`,
        {
          method: "POST",
          credentials: "same-origin",
          cache: "no-store",
          redirect: "error",
          signal: abort.signal,
          headers: {
            "Content-Type": "application/json",
            "X-CSRF-TOKEN": csrf.csrfToken,
            "If-Match": row.version,
          },
          body: JSON.stringify(
            complete
              ? { results, note: notes[row.runId] ?? null, files }
              : start
                ? {}
                : cancel
                  ? { reason }
                  : { assigneeId },
          ),
        },
      );
      if (!response.ok) {
        const problem: unknown = await response.json().catch(() => null);
        const code =
          isObject(problem) && typeof problem.code === "string"
            ? problem.code
            : null;
        const rejected =
          response.status === 400 &&
          code !== null &&
          [
            "INVALID_CHECK_RESULTS",
            "MANDATORY_CHECK_RESULTS_REQUIRED",
            "MSG23",
            "CANCELLATION_REASON_REQUIRED",
            "ASSIGNEE_REQUIRED",
            "TECHNICIAN_EXECUTION_AUTHORITY_REQUIRED",
            "TECHNICIAN_READ_AUTHORITY_REQUIRED",
          ].includes(code);
        const workday =
          response.status === 409 && code === "TIME_WORKDAY_REQUIRED";
        const busyWork =
          response.status === 409 && code === "TIME_INTERVAL_ALREADY_OPEN";
        if (rejected || workday || busyWork) {
          if (!abort.signal.aborted && current.current === owner) {
            setWorkdayRequired(workday);
            setMessage(
              workday
                ? "MSG30: Bắt đầu ngày làm việc trong chi nhánh trước khi thực hiện."
                : busyWork
                  ? "MSG31: Có khoảng làm việc khác đang mở. Hoàn tất hoặc tạm dừng trước khi bắt đầu lần thực hiện này."
                  : code === "MSG23"
                    ? "Bằng chứng bị từ chối. Kiểm tra nội dung, định dạng và dung lượng rồi gửi lại."
                    : "Dữ liệu bị từ chối. Kiểm tra kết quả, người được giao và lý do; bản nháp được giữ để sửa.",
            );
          }
          return;
        }
        throw Error(
          response.status === 412
            ? "Lần thực hiện đã thay đổi."
            : `Chưa xác nhận thay đổi (${response.status}).`,
        );
      }
      const raw: unknown = await response.json();
      if (!valid(raw) || raw.runId !== row.runId)
        throw Error("Thay đổi không xác minh được phản hồi.");
      if (!abort.signal.aborted && current.current === owner) {
        // Assignment/status changes can remove the row from the current filter or
        // scope. Discard list pagination; detail may use the authorized response.
        setRows(runId != null ? [raw] : []);
        setHasMore(false);
        setHistory({});
        setEvidence({});
        setAssignees({});
        setReasons({});
        setOutcomes({});
        setCheckNotes({});
        setNotes({});
        setUploads({});
        setMessage(
          (complete
            ? "Đã hoàn tất lần thực hiện và kết thúc khoảng thời gian làm việc."
            : start
              ? row.status === "ON_HOLD"
                ? "Đã tiếp tục lần thực hiện và mở khoảng thời gian làm việc mới."
                : "Đã bắt đầu lần thực hiện và mở khoảng thời gian làm việc."
              : cancel
                ? "Đã hủy lần thực hiện."
                : "Đã phân công kỹ thuật viên.") +
            (runId == null
              ? "Tải lại các lần thực hiện để cập nhật danh sách công việc đã lọc."
              : ""),
        );
      }
    } catch (e) {
      if (!abort.signal.aborted && current.current === owner) {
        setMessage(
          String(e) + (submitted ? "Tải lại trước khi thay đổi tiếp." : ""),
        );
        if (submitted) setUncertain(true);
      }
    } finally {
      sending.current = false;
      if (!abort.signal.aborted && current.current === owner) setBusy(false);
    }
  }
  async function load(more = false) {
    if (!owner || owner !== mounted || sending.current || (more && uncertain))
      return;
    const abort = new AbortController();
    controller.current = abort;
    sending.current = true;
    setBusy(true);
    setMessage("");
    setHistory({});
    setEvidence({});
    try {
      const filters = new URLSearchParams();
      if (filterAssignee) {
        if (!positive(Number(filterAssignee)))
          throw Error("Mã người được giao phải là số nguyên dương.");
        filters.set("assigneeId", filterAssignee);
      }
      if (keyword.trim()) filters.set("keyword", keyword.trim());
      if (dueFrom)
        filters.set("dueFrom", new Date(dueFrom + "+07:00").toISOString());
      if (dueTo) filters.set("dueTo", new Date(dueTo + "+07:00").toISOString());
      if (overdue) filters.set("overdue", overdue);
      const response = await fetch(
        runId != null
          ? `/api/branches/${branchId}/preventive-occurrences/${runId}`
          : `/api/branches/${branchId}/preventive-occurrences?afterId=${more ? (rows.at(-1)?.runId ?? 0) : 0}&mine=${mine}${status ? `&status=${encodeURIComponent(status)}` : ""}&${filters.toString()}`,
        {
          credentials: "same-origin",
          cache: "no-store",
          redirect: "error",
          signal: abort.signal,
        },
      );
      if (!response.ok)
        throw Error(
          `Các lần thực hiện chưa khả dụng (${response.status}). Kiểm tra quyền và cấu hình hiện tại.`,
        );
      const payload: unknown = await response.json();
      const raw = runId != null ? [payload] : payload;
      if (!Array.isArray(raw) || !raw.every(valid))
        throw Error("Không xác minh được chi tiết lần thực hiện.");
      if (!abort.signal.aborted && current.current === owner) {
        setRows(more ? [...rows, ...raw] : raw);
        setHasMore(runId == null && raw.length === 50);
        if (!more) {
          setUncertain(false);
          setWorkdayRequired(false);
          setAssignees({});
          setReasons({});
          setOutcomes({});
          setCheckNotes({});
          setNotes({});
          setUploads({});
        }
        if (!raw.length)
          setMessage("Không còn lần thực hiện trong phạm vi hiện tại.");
      }
    } catch (e) {
      if (!abort.signal.aborted && current.current === owner) {
        setRows([]);
        setMessage(String(e));
      }
    } finally {
      sending.current = false;
      if (!abort.signal.aborted && current.current === owner) setBusy(false);
    }
  }
  if (!owner || owner !== mounted || !positive(branchId)) return null;
  return (
    <section aria-label="Lần bảo trì theo lịch">
      <h2>Bảo trì theo lịch</h2>
      {runId == null && (
        <CollapsibleFilterPanel
          activeCount={
            [filterAssignee, keyword, dueFrom, dueTo, overdue].filter(Boolean)
              .length +
            Number(mine) +
            Number(!!status && status !== "OPEN")
          }
        >
          <fieldset disabled={busy}>
            <legend>Công việc của tôi và lần thực hiện trong chi nhánh</legend>
            <label>
              <input
                type="checkbox"
                checked={mine}
                onChange={(e) => {
                  setMine(e.target.checked);
                  setFilterAssignee("");
                  setRows([]);
                  setHistory({});
                  setEvidence({});
                }}
              />
              Giao cho tôi
            </label>
            <label>
              Trạng thái
              <SearchableSelect
                value={status}
                onChange={(e) => {
                  setStatus(e.target.value);
                  setRows([]);
                  setHistory({});
                  setEvidence({});
                }}
              >
                <option value="OPEN">Công việc chưa hoàn tất</option>
                <option value="">Tất cả trạng thái</option>
                <option value="PENDING_ASSIGNMENT">Chờ phân công</option>
                <option value="ASSIGNED">Đã phân công</option>
                <option value="IN_PROGRESS">Đang thực hiện</option>
                <option value="ON_HOLD">Đang tạm dừng</option>
                <option value="COMPLETED">Đã hoàn tất</option>
                <option value="CANCELLED">Đã hủy</option>
              </SearchableSelect>
            </label>
            <label>
              Tìm theo tiêu đề, mã lần thực hiện hoặc mã lịch
              <SearchInput suggestions={searchSuggestions(rows)}
                maxLength={200}
                value={keyword}
                onChange={(e) => {
                  setKeyword(e.target.value);
                  clearResults();
                }}
              />
            </label>
            <label>
              Mã người được giao
              <input
                type="number"
                min="1"
                step="1"
                disabled={mine}
                value={filterAssignee}
                onChange={(e) => {
                  setFilterAssignee(e.target.value);
                  clearResults();
                }}
              />
            </label>
            <label>
              Hạn từ (UTC+7)
              <input
                type="datetime-local"
                value={dueFrom}
                onChange={(e) => {
                  setDueFrom(e.target.value);
                  clearResults();
                }}
              />
            </label>
            <label>
              Hạn đến (UTC+7)
              <input
                type="datetime-local"
                value={dueTo}
                onChange={(e) => {
                  setDueTo(e.target.value);
                  clearResults();
                }}
              />
            </label>
            <label>
              Quá hạn
              <SearchableSelect
                value={overdue}
                onChange={(e) => {
                  setOverdue(e.target.value);
                  clearResults();
                }}
              >
                <option value="">Tất cả</option>
                <option value="true">Quá hạn</option>
                <option value="false">Chưa quá hạn</option>
              </SearchableSelect>
            </label>
            <p>
              Sắp xếp lần thực hiện đến hạn sớm nhất trước. Kết quả chi nhánh
              vẫn giới hạn theo quyền hiện tại.
            </p>
          </fieldset>
        </CollapsibleFilterPanel>
      )}
      <button disabled={busy} onClick={() => void load()}>
        {runId != null ? "Mở lần thực hiện" : "Tải các lần thực hiện"}
      </button>
      {runId == null && hasMore && !!rows.length && (
        <button disabled={busy || uncertain} onClick={() => void load(true)}>
          Tải thêm lần thực hiện
        </button>
      )}
      {message && <p role="status">{userMessage(message)}</p>}
      {workdayRequired && (
        <Link to={`/workday?branchId=${branchId}`}>
          Mở ngày làm việc của tôi
        </Link>
      )}
      {rows.map((r) => (
        <details key={r.runId} open={runId != null || undefined}>
          <summary>
            {r.title} — {displayCode(r.status)} —{" "}
            {new Date(r.dueAt).toLocaleString()}
          </summary>
          <p>{r.isOverdue ? "Quá hạn" : "Chưa quá hạn"}</p>
          <p>{r.instruction}</p>
          <p>
            Lần thực hiện {referenceLabel(r, "runId")}; Lịch {referenceLabel(r, "scheduleId")}; Người được giao{" "}
            {r.assigneeName ?? r.assigneeId ?? "Chờ phân công"}.
          </p>
          <button disabled={busy} onClick={() => void loadHistory(r.runId)}>
            Tải các lần thực hiện trước và sự cố liên quan
          </button>
          {history[r.runId] && (
            <section aria-label={`Lịch sử lần thực hiện ${r.runId}`}>
              <h3>Các lần thực hiện trước trong phạm vi hiện tại</h3>
              {!history[r.runId].previousOccurrences.length && (
                <p>Không có lần thực hiện trước trong phạm vi của bạn.</p>
              )}
              {history[r.runId].previousOccurrences.map((p) => (
                <details key={p.runId}>
                  <summary>
                    {p.title} — {new Date(p.dueAt).toLocaleString()} —{" "}
                    {displayCode(p.status)}
                  </summary>
                  <p>{p.instruction}</p>
                  {p.note && <p>{p.note}</p>}
                  {p.results && (
                    <ul>
                      {p.results.map((c) => (
                        <li key={c.key}>
                          {
                            p.checkItems.find((i) => i.key === c.key)
                              ?.description
                          }
                          : {c.passed ? "Đạt" : "Không đạt"}
                          {c.note && ` — ${c.note}`}
                        </li>
                      ))}
                    </ul>
                  )}
                </details>
              ))}
              <h3>Sự cố liên quan trong phạm vi quyền xem hiện tại</h3>
              {!history[r.runId].incidents.length && (
                <p>Không có sự cố liên quan trong phạm vi của bạn.</p>
              )}
              <ul>
                {history[r.runId].incidents.map((i) => (
                  <li key={i.incidentId}>
                    <Link
                      to={`/operations/incidents/${i.incidentId}?branchId=${branchId}`}
                    >
                      {i.number}: {i.title}
                    </Link>{" "}
                    — {displayCode(i.status)} — kiểm tra{" "}
                    {r.checkItems.find((c) => c.key === i.checkKey)
                      ?.description ?? i.checkKey}
                  </li>
                ))}
              </ul>
            </section>
          )}
          {r.canStart && (
            <button
              disabled={busy || uncertain}
              onClick={() => void change(r, false, true)}
            >
              {r.status === "ON_HOLD"
                ? "Tiếp tục lần thực hiện"
                : "Bắt đầu lần thực hiện"}
            </button>
          )}
          {r.canComplete && (
            <ActionDialog
              error={message}
              title="Ghi kết quả bảo trì định kỳ"
              busy={busy}
            >
              <fieldset disabled={busy || uncertain}>
                <legend>Ghi kết quả kiểm tra</legend>
                {r.checkItems.map((c) => (
                  <div key={c.key}>
                    <label>
                      {c.description}
                      {c.required ? " (required)" : ""}
                      <SearchableSelect
                        value={outcomes[`${r.runId}:${c.key}`] ?? ""}
                        onChange={(e) =>
                          setOutcomes({
                            ...outcomes,
                            [`${r.runId}:${c.key}`]: e.target.value,
                          })
                        }
                      >
                        <option value="">Chọn kết quả</option>
                        <option value="PASS">Đạt</option>
                        <option value="FAIL">Không đạt</option>
                      </SearchableSelect>
                    </label>
                    <label>
                      Ghi chú kiểm tra
                      <textarea
                        maxLength={2000}
                        value={checkNotes[`${r.runId}:${c.key}`] ?? ""}
                        onChange={(e) =>
                          setCheckNotes({
                            ...checkNotes,
                            [`${r.runId}:${c.key}`]: e.target.value,
                          })
                        }
                      />
                    </label>
                    {outcomes[`${r.runId}:${c.key}`] === "FAIL" &&
                      !busy &&
                      !uncertain && (
                        <Link to={incidentUrl(r, c.key)}>
                          Báo sự cố cho hạng mục kiểm tra không đạt này
                        </Link>
                      )}
                  </div>
                ))}
                <label>
                  Ghi chú hoàn tất
                  <textarea
                    maxLength={4000}
                    value={notes[r.runId] ?? ""}
                    onChange={(e) =>
                      setNotes({ ...notes, [r.runId]: e.target.value })
                    }
                  />
                </label>
                <label>
                  Ảnh hoặc tệp bổ trợ (tối đa 5 tệp, 10 MiB mỗi tệp)
                  <input
                    type="file"
                    multiple
                    accept="image/jpeg,image/png,application/pdf"
                    onChange={(e) =>
                      setUploads({
                        ...uploads,
                        [r.runId]: Array.from(e.target.files ?? []),
                      })
                    }
                  />
                </label>
                <button
                  data-dialog-action
                  onClick={() => void change(r, false, false, true)}
                >
                  Hoàn tất lần thực hiện
                </button>
              </fieldset>
            </ActionDialog>
          )}
          {r.status === "COMPLETED" && (
            <>
              <button
                disabled={busy}
                onClick={() => void loadEvidence(r.runId)}
              >
                Tải bằng chứng hoàn tất
              </button>
              {evidence[r.runId] && (
                <ul>
                  {evidence[r.runId].map((e) => (
                    <li key={e.id}>
                      <a
                        href={`/api/branches/${branchId}/preventive-occurrences/${r.runId}/evidence/${e.id}`}
                        download
                      >
                        {e.name}
                      </a>{" "}
                      ({Math.ceil(e.size / 1024)} KiB)
                    </li>
                  ))}
                </ul>
              )}
            </>
          )}
          {r.results && (
            <ul>
              {r.results.map((c) => (
                <li key={c.key}>
                  {r.checkItems.find((i) => i.key === c.key)?.description}:{" "}
                  {c.passed ? "Đạt" : "Không đạt"}
                  {c.note && ` — ${c.note}`}
                </li>
              ))}
            </ul>
          )}
          <p>
            {r.roomId
              ? "Phòng" + (r.roomNumber ?? "#" + r.roomId)
              : r.areaId
                ? (r.areaName ?? "Khu vực #" + r.areaId)
                : [r.assetCode, r.assetName].filter(Boolean).join(" ? ") ||
                  "Tài sản #" + r.assetId}
          </p>
          <ul>
            {r.checkItems.map((c) => (
              <li key={c.key}>
                {c.description}
                {c.required ? " (required)" : ""}
              </li>
            ))}
          </ul>
          {r.performedAt && (
            <p>Thực hiện lúc: {new Date(r.performedAt).toLocaleString()}</p>
          )}
          {r.result && <p>{r.result}</p>}
          {r.note && <p>{r.note}</p>}
          {r.canAssign && (
            <ActionDialog
              error={message}
              title="Phân công kỹ thuật viên"
              busy={busy}
            >
              <fieldset disabled={busy || uncertain}>
                <PreventiveTechnicianSelect
                  url={`/api/branches/${branchId}/preventive-occurrences/${r.runId}/technicians`}
                  value={assignees[r.runId] ?? ""}
                  onChange={(value) =>
                    setAssignees({ ...assignees, [r.runId]: value })
                  }
                />
                <button onClick={() => void change(r, false)}>
                  Phân công kỹ thuật viên
                </button>
              </fieldset>
            </ActionDialog>
          )}
          {r.canCancel && (
            <ActionDialog
              error={message}
              title="Hủy lần bảo trì định kỳ"
              busy={busy}
            >
              <fieldset disabled={busy || uncertain}>
                <label>
                  Lý do hủy
                  <textarea
                    minLength={10}
                    maxLength={4000}
                    value={reasons[r.runId] ?? ""}
                    onChange={(e) =>
                      setReasons({ ...reasons, [r.runId]: e.target.value })
                    }
                  />
                </label>
                <button onClick={() => void change(r, true)}>
                  Hủy lần thực hiện
                </button>
              </fieldset>
            </ActionDialog>
          )}
        </details>
      ))}
    </section>
  );
}
