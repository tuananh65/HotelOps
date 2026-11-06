import { submitAssignment } from "../../api/dev1/cleaningAssignmentCommand";
import { useEffect, useRef, useState, type FormEvent } from "react";
import { Link, useSearchParams } from "react-router-dom";
import {
  readAssignmentPlan,
  buildAssignmentDraft,
  assignmentError,
  type AssignmentPlan,
} from "../../api/dev1/cleaningAssignment";
import { PageHeader } from "../../ui";
export default function CleaningAssignmentScreen() {
  const [params, setParams] = useSearchParams();
  const branch = params.get("branchId") || "",
    task = params.get("taskId") || params.get("record") || "",
    page = Number(params.get("page") || "1"),
    search = params.get("search") || "";
  const key = JSON.stringify([branch, task, page, search]);
  const [result, setResult] = useState<{
      key: string;
      data: AssignmentPlan;
    } | null>(null),
    [error, setError] = useState<{ key: string; message: string } | null>(null);
  const [loading, setLoading] = useState(false),
    [retry, setRetry] = useState(0),
    [draft, setDraft] = useState<{ key: string; message: string } | null>(null);
  useEffect(() => {
    const abort = new AbortController();
    setResult(null);
    setError(null);
    setDraft(null);
    setLoading(Boolean(branch && task));
    if (branch && task)
      readAssignmentPlan(branch, task, page, search, abort.signal)
        .then((data) => {
          if (!abort.signal.aborted) setResult({ key, data });
        })
        .catch((e) => {
          if (!abort.signal.aborted)
            setError({ key, message: assignmentError(e) });
        })
        .finally(() => {
          if (!abort.signal.aborted) setLoading(false);
        });
    return () => abort.abort();
  }, [branch, task, page, search, key, retry]);
  const [command, setCommand] = useState<{
    branch: string;
    task: string;
    draft: ReturnType<typeof buildAssignmentDraft>;
    tag: string;
    operation: string;
  } | null>(null);
  const [saving, setSaving] = useState(false),
    [saveError, setSaveError] = useState(""),
    [committed, setCommitted] = useState<number | null>(null);
  const activeScope = useRef(branch + ":" + task);
  activeScope.current = branch + ":" + task;
  useEffect(() => {
    setCommand(null);
    setCommitted(null);
    setSaveError("");
    setSaving(false);
  }, [branch, task]);
  async function executeCommand(c: NonNullable<typeof command>) {
    const belongs = () => activeScope.current === c.branch + ":" + c.task;
    if (saving) return;
    setSaving(true);
    setSaveError("");
    try {
      const r = await submitAssignment(
        c.branch,
        c.task,
        c.draft,
        c.tag,
        c.operation,
      );
      if (!belongs()) return;
      setCommitted(r.assignmentId);
      setRetry((v) => v + 1);
    } catch (e) {
      if (belongs()) setSaveError(assignmentError(e));
    } finally {
      if (belongs()) setSaving(false);
    }
  }
  function assign(form: HTMLFormElement) {
    if (
      !data ||
      command ||
      saving ||
      data.assignmentCapability !== "PROVISIONAL_NOT_ACCEPTED" ||
      !data.sourceETag
    )
      return;
    try {
      const f = new FormData(form);
      const d = buildAssignmentDraft(
        data,
        String(f.get("assigneeId") || ""),
        String(f.get("dueAt") || ""),
        String(f.get("reason") || ""),
      );
      const c = {
        branch,
        task,
        draft: d,
        tag: data.sourceETag,
        operation: crypto.randomUUID(),
      };
      setCommand(c);
      void executeCommand(c);
    } catch (e) {
      setSaveError(e instanceof Error ? e.message : "Invalid command");
    }
  }
  function load(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const form = new FormData(e.currentTarget);
    const next = new URLSearchParams();
    for (const k of ["branchId", "taskId", "search"]) {
      const v = String(form.get(k) || "").trim();
      if (v) next.set(k, v);
    }
    next.set("page", "1");
    setCommand(null);
    setCommitted(null);
    setSaveError("");
    setParams(next);
    setRetry((v) => v + 1);
  }
  function changePage(n: number) {
    const next = new URLSearchParams(params);
    next.set("page", String(n));
    setCommand(null);
    setCommitted(null);
    setSaveError("");
    setParams(next);
  }
  const data = result?.key === key ? result.data : null;
  function preview(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    if (!data) return;
    const form = new FormData(e.currentTarget);
    try {
      const value = buildAssignmentDraft(
        data,
        String(form.get("assigneeId") || ""),
        String(form.get("dueAt") || ""),
        String(form.get("reason") || ""),
      );
      setDraft({
        key,
        message: `Bản nháp hợp lệ về định dạng: nhân viên ${value.assigneeId}, hạn ${value.dueAt}. Chưa gửi hoặc lưu phân công; điều kiện ca làm sẽ được máy chủ kiểm tra khi chức năng được mở.`,
      });
    } catch (e) {
      setDraft({
        key,
        message: e instanceof Error ? e.message : "Bản nháp không hợp lệ.",
      });
    }
  }
  return (
    <>
      <PageHeader id={27} />
      {committed && (
        <p role="status">
          Assignment {committed} committed. Notification pending delivery.
        </p>
      )}
      {saveError && (
        <p role="alert">
          {saveError}{" "}
          {command && !committed && (
            <button
              disabled={saving}
              onClick={() => void executeCommand(command)}
            >
              Retry same command
            </button>
          )}
        </p>
      )}
      {saving && <p role="status">Saving assignment...</p>}
      <p role="status">
        {data?.assignmentCapability === "PROVISIONAL_NOT_ACCEPTED"
          ? "Isolated development candidate. Assignment includes mandatory notification staging; delivery is asynchronous."
          : "Chỉ xem dữ liệu và chuẩn bị bản nháp. Chưa thể lưu phân công vì thông báo bắt buộc chưa được tích hợp."}
      </p>
      <form key={key} className="filter-bar" onSubmit={load}>
        <label>
          Chi nhánh (ID)
          <input name="branchId" required defaultValue={branch} />
        </label>
        <label>
          Công việc (ID)
          <input name="taskId" required defaultValue={task} />
        </label>
        <label>
          Tìm nhân viên
          <input name="search" defaultValue={search} maxLength={150} />
        </label>
        <button>Tải phương án</button>
      </form>
      {loading && <p role="status">Đang tải phương án phân công…</p>}
      {error?.key === key && (
        <p role="alert">
          {error.message}{" "}
          <button onClick={() => setRetry((v) => v + 1)}>Thử lại</button>
        </p>
      )}
      {!branch || !task ? (
        <p>
          Nhập mã công việc và chi nhánh được cấp quyền; không dùng dữ liệu nhân
          viên minh họa.
        </p>
      ) : null}
      {data && (
        <div className="detail-grid">
          <section className="panel">
            <h2>
              {data.task.number} · {data.task.roomNumber}
            </h2>
            <p>
              Chi nhánh {data.task.branchId} ·{" "}
              {data.task.workflowState ?? data.task.recordedStatus}
            </p>
            <Link
              to={`/cleaning/tasks/${data.task.id}?branchId=${branch}&monitor=1&kind=cleaning`}
            >
              Chi tiết Cleaning Monitor
            </Link>
            <form key={`${key}:${retry}`} onSubmit={preview}>
              <label>
                Nhân viên (bản nháp)
                <select
                  aria-label="Nhân viên (bản nháp)"
                  name="assigneeId"
                  required
                  defaultValue=""
                >
                  <option value="">Chọn nhân viên</option>
                  {data.candidates.map((c) => (
                    <option key={c.id} value={c.id}>
                      {c.name}
                    </option>
                  ))}
                </select>
              </label>
              <label>
                Hạn UTC (ISO 8601)
                <input
                  name="dueAt"
                  required
                  placeholder="YYYY-MM-DDTHH:mm:ssZ"
                />
              </label>
              <label>
                Lý do phân công
                <input name="reason" required maxLength={500} />
              </label>
              <button
                disabled={
                  !data.workflowStateVerified ||
                  data.task.workflowState !== "PENDING_ASSIGNMENT"
                }
              >
                Kiểm tra bản nháp
              </button>
              <button
                type="button"
                disabled={
                  saving ||
                  command !== null ||
                  data.assignmentCapability !== "PROVISIONAL_NOT_ACCEPTED" ||
                  data.task.workflowState !== "PENDING_ASSIGNMENT"
                }
                onClick={(e) => assign(e.currentTarget.form!)}
              >
                Lưu phân công
              </button>
            </form>
            {draft?.key === key && <p role="status">{draft.message}</p>}
            <p>
              Không tự tiếp nhận task legacy, không đổi người phụ trách. Danh
              sách ứng viên và ca ghi nhận chưa phải xác nhận đủ điều kiện phân
              công.
            </p>
          </section>
          <section className="panel">
            <h3>Ứng viên & khối lượng từ máy chủ</h3>
            <p>
              {data.total} ứng viên · trang {page} · {data.filters.from} →{" "}
              {data.filters.to}
            </p>
            {data.candidates.length === 0 && <p>Không có ứng viên phù hợp.</p>}
            {data.candidates.map((c) => (
              <div className="attention-row" key={c.id}>
                <span>
                  <strong>{c.name}</strong>
                  <small>Đơn vị {c.departmentId ?? "—"}</small>
                </span>
                <span>
                  {c.openTasks} việc mở · {c.openTasksInWindow} trong khoảng
                </span>
              </div>
            ))}
            <h3>Ca được ghi nhận</h3>
            {data.recordedShifts.length === 0 && (
              <p>Không có ca được ghi nhận trong khoảng.</p>
            )}
            <ul>
              {data.recordedShifts.map((s) => (
                <li key={`${s.id}:${s.userId}`}>
                  {s.userId} · {s.from} → {s.to} · {s.recordedShiftStatus} /{" "}
                  {s.recordedAssignmentStatus}
                </li>
              ))}
            </ul>
            <button disabled={page <= 1} onClick={() => changePage(page - 1)}>
              Trang trước
            </button>
            <button
              disabled={page * 20 >= data.total}
              onClick={() => changePage(page + 1)}
            >
              Trang sau
            </button>
          </section>
        </div>
      )}
    </>
  );
}
