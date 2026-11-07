import { useEffect, useRef, useState, type FormEvent } from "react";
import { Link } from "react-router-dom";
import { ApiError } from "../../api/dev1/client";
import {
  readAssignmentPlan,
  type AssignmentPlan,
} from "../../api/dev1/cleaningAssignment";
import {
  readCleaningExecution,
  mutateCleaningExecution,
  reassignmentCommand,
  type CleaningExecution,
  type ReassignmentCommand,
} from "../../api/dev1/cleaningExecution";
export default function CleaningExecutionPanel({
  branch,
  task,
}: {
  branch: string;
  task: string;
}) {
  const [data, setData] = useState<CleaningExecution | null>(null),
    [plan, setPlan] = useState<AssignmentPlan | null>(null);
  const [error, setError] = useState(""),
    [notice, setNotice] = useState(""),
    [busy, setBusy] = useState(false),
    [refresh, setRefresh] = useState(0);
  const [candidatePage, setCandidatePage] = useState(1);
  const [pending, setPending] = useState<{
    command: ReassignmentCommand;
    tag: string;
    key: string;
  } | null>(null);
  const alive = useRef(true);
  useEffect(() => {
    alive.current = true;
    return () => {
      alive.current = false;
    };
  }, []);
  useEffect(() => {
    const abort = new AbortController();
    setData(null);
    setPlan(null);
    setError("");
    readCleaningExecution(branch, task, abort.signal)
      .then(async (d) => {
        if (abort.signal.aborted) return;
        setData(d);
        if (d.reassignCapability === "PROVISIONAL_NOT_ACCEPTED") {
          const candidates = await readAssignmentPlan(
            branch,
            task,
            candidatePage,
            "",
            abort.signal,
          );
          if (!abort.signal.aborted) setPlan(candidates);
        }
      })
      .catch((e) => {
        if (!abort.signal.aborted) setError(message(e));
      });
    return () => abort.abort();
  }, [branch, task, refresh, candidatePage]);
  function message(e: unknown) {
    if (e instanceof ApiError) {
      if (e.status === 401) return "Phiên đăng nhập đã hết hạn.";
      if (e.status === 403)
        return "Không có quyền hoặc bạn không còn là người phụ trách.";
      if (e.status === 412)
        return "Công việc đã thay đổi. Tải lại nguồn trước lệnh mới.";
      if (e.status === 409)
        return "Kiểm tra ca làm việc đang mở, công việc khác đang thực hiện và trạng thái hiện tại.";
      if (e.status === 503)
        return "Chức năng tạm thời chưa sẵn sàng. Vui lòng thử lại sau.";
    }
    return "Không xác minh được kết quả. Tải lại nguồn; với Reassign hãy thử đúng lệnh cũ.";
  }
  async function start() {
    if (!data || busy || pending || data.startCapability !== "AVAILABLE")
      return;
    setBusy(true);
    setError("");
    setNotice("");
    try {
      await mutateCleaningExecution(branch, task, "start", data.sourceETag, {
        resolveBlocker: false,
      });
      if (alive.current) {
        setNotice("Đã bắt đầu dọn phòng; thời gian được ghi nhận bởi máy chủ.");
        setRefresh((n) => n + 1);
      }
    } catch (e) {
      if (alive.current) setError(message(e));
    } finally {
      if (alive.current) setBusy(false);
    }
  }
  async function execute(c: NonNullable<typeof pending>) {
    if (busy) return;
    setBusy(true);
    setError("");
    setNotice("");
    try {
      await mutateCleaningExecution(
        branch,
        task,
        "reassign",
        c.tag,
        c.command,
        c.key,
      );
      if (alive.current) {
        setPending(null);
        setNotice("Đã phân công lại và ghi nhận thông báo cho hai nhân viên.");
        setRefresh((n) => n + 1);
      }
    } catch (e) {
      if (alive.current) setError(message(e));
    } finally {
      if (alive.current) setBusy(false);
    }
  }
  function reassign(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    if (!data || busy || pending) return;
    try {
      const f = new FormData(e.currentTarget);
      const command = reassignmentCommand(
        data,
        String(f.get("assigneeId") ?? ""),
        String(f.get("dueAt") ?? ""),
        String(f.get("reason") ?? ""),
      );
      if (!plan?.candidates.some((c) => c.id === command.assigneeId))
        throw Error("INVALID_CANDIDATE");
      const c = { command, tag: data.sourceETag, key: crypto.randomUUID() };
      setPending(c);
      void execute(c);
    } catch {
      setError(
        "Chọn người mới từ danh sách, hạn UTC tương lai và lý do 10–500 ký tự.",
      );
    }
  }
  return (
    <section className="panel">
      <h1>Vòng đời dọn phòng</h1>
      {error && <p role="alert">{error}</p>}
      {notice && <p role="status">{notice}</p>}
      {busy && <p role="status">Đang lưu…</p>}
      {!data && !error && <p role="status">Đang tải công việc…</p>}
      {data && (
        <>
          <h2>{data.taskNumber}</h2>
          <p>
            {data.state} · Nhân viên {data.assigneeId ?? "—"}
          </p>
          <button
            disabled={busy || !!pending || data.startCapability !== "AVAILABLE"}
            onClick={() => void start()}
          >
            Bắt đầu dọn phòng
          </button>
          {data.startCapability === "BLOCKER_RESOLUTION_CONTRACT_REQUIRED" && (
            <p>Chưa thể tiếp tục: cần xác nhận trở ngại đã được giải quyết.</p>
          )}
          {data.state === "IN_PROGRESS" && (
            <Link to={`/cleaning/tasks/${task}?branchId=${branch}`}>
              Mở tiến độ dọn phòng
            </Link>
          )}
          {data.reassignCapability === "PROVISIONAL_NOT_ACCEPTED" && plan && (
            <form onSubmit={reassign}>
              <h3>Phân công lại</h3>
              <p>
                Thay đổi người phụ trách và gửi thông báo cho hai nhân viên.
              </p>
              <label>
                Người phụ trách mới
                <select
                  aria-label="Người phụ trách mới"
                  name="assigneeId"
                  required
                  disabled={busy || !!pending}
                  defaultValue=""
                >
                  <option value="">Chọn nhân viên</option>
                  {plan.candidates
                    .filter((c) => c.id !== data.assigneeId)
                    .map((c) => (
                      <option key={c.id} value={c.id}>
                        {c.name} · {c.openTasks} việc mở
                      </option>
                    ))}
                </select>
              </label>
              <div>
                <button
                  type="button"
                  disabled={busy || !!pending || candidatePage <= 1}
                  onClick={() => setCandidatePage((n) => n - 1)}
                >
                  Trang trước nhân viên
                </button>
                <span>
                  {" "}
                  Trang {candidatePage} /{" "}
                  {Math.max(1, Math.ceil(plan.total / 20))}{" "}
                </span>
                <button
                  type="button"
                  disabled={
                    busy || !!pending || candidatePage * 20 >= plan.total
                  }
                  onClick={() => setCandidatePage((n) => n + 1)}
                >
                  Trang sau nhân viên
                </button>
              </div>
              <label>
                Hạn UTC
                <input
                  name="dueAt"
                  required
                  placeholder="YYYY-MM-DDTHH:mm:ssZ"
                  disabled={busy || !!pending}
                />
              </label>
              <label>
                Lý do
                <input
                  name="reason"
                  required
                  minLength={10}
                  maxLength={500}
                  disabled={busy || !!pending}
                />
              </label>
              <button
                disabled={
                  busy ||
                  !!pending ||
                  !["ASSIGNED", "IN_PROGRESS", "ON_HOLD"].includes(
                    data.state ?? "",
                  )
                }
              >
                Lưu phân công lại
              </button>
            </form>
          )}
        </>
      )}
      {pending && error && (
        <button disabled={busy} onClick={() => void execute(pending)}>
          Thử lại đúng lệnh phân công lại
        </button>
      )}
      <button disabled={busy} onClick={() => setRefresh((n) => n + 1)}>
        Tải lại nguồn
      </button>
    </section>
  );
}
