import { displayCode } from "../i18n/vi";
import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import type { AuthorityGeneration } from "../auth/authStore";
import { taskWorkflowStates } from "./api";
import { TaskProgressError } from "./progress";
import {
  readWorkload,
  type WorkloadDetail,
  type WorkloadFilters,
  type WorkloadPage,
  type WorkloadTaskPage,
} from "./workload";

export function TaskWorkloadPanel({
  branch,
  owner,
  initialSearch = "",
  initialFilters,
}: {
  branch: number;
  owner: AuthorityGeneration;
  initialSearch?: string;
  initialFilters?: WorkloadFilters;
}) {
  const { actions } = useAuth();
  const [form, setForm] = useState({
    from: initialFilters?.from ?? "",
    to: initialFilters?.to ?? "",
    departmentId: initialFilters?.departmentId?.toString() ?? "",
    employeeId: "",
    priorityId: "",
    status: initialFilters?.status ?? "",
    deadlineFrom: "",
    deadlineTo: "",
    search: initialSearch,
    overdueOnly: initialFilters?.overdueOnly ?? false,
  });
  const [query, setQuery] = useState<{
    filters: WorkloadFilters;
    page: number;
    employee: number | null;
    revision: number;
  } | null>(initialFilters ? { filters: initialFilters, page: 1, employee: null, revision: 0 } : null);
  const [result, setResult] = useState<WorkloadPage | WorkloadDetail | WorkloadTaskPage | null>(
    null,
  );
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [denied, setDenied] = useState(false);
  useEffect(() => {
    if (!query) return;
    const controller = new AbortController();
    setBusy(true);
    setResult(null);
    setError("");
    void readWorkload(
      branch,
      query.filters,
      query.page,
      query.employee,
      controller.signal,
    )
      .then((data) => {
        if (!controller.signal.aborted) setResult(data);
      })
      .catch((error) => {
        if (controller.signal.aborted) return;
        if (error instanceof TaskProgressError && error.status === 401)
          actions.invalidateSession(owner);
        if (error instanceof TaskProgressError && error.status === 403)
          setDenied(true);
        setError(
          error instanceof TaskProgressError && error.status === 400
            ? "Kiểm tra mã tham chiếu và ngày. Khoảng thời gian phải đúng thứ tự; truy vấn lịch khách sạn tối đa 366 ngày."
            : "Chưa có khối lượng công việc. Kiểm tra bộ lọc, quyền rồi tải lại.",
        );
      })
      .finally(() => {
        if (!controller.signal.aborted) setBusy(false);
      });
    return () => controller.abort();
  }, [branch, owner, actions, query]);
  function load() {
    const filters: WorkloadFilters = { overdueOnly: form.overdueOnly, calendar: initialFilters?.calendar, taskSet: initialFilters?.taskSet };
    for (const key of [
      "from",
      "to",
      "deadlineFrom",
      "deadlineTo",
      "status",
      "search",
    ] as const)
      if (form[key]) filters[key] = form[key];
    for (const key of ["departmentId", "employeeId", "priorityId"] as const)
      if (form[key]) {
        const n = Number(form[key]);
        if (!/^[1-9]\d*$/.test(form[key]) || !Number.isSafeInteger(n)) {
          setError("Các mã tham chiếu phải là số nguyên dương.");
          return;
        }
        filters[key] = n;
      }
    setQuery((q) => ({
      filters,
      page: 1,
      employee: null,
      revision: (q?.revision ?? 0) + 1,
    }));
  }
  if (denied)
    return (
      <p role="status">
        Xem khối lượng công việc cần trưởng bộ phận hoặc quản lý vận hành có quyền.
      </p>
    );
  if (!query)
    return <button onClick={load}>Xem lại khối lượng công việc và nhật ký thời gian</button>;
  const page = result && "items" in result ? result : null;
  const detail = result && "userId" in result ? result : null;
  const taskPage = result && "taskSet" in result ? result : null;
  const total =
    taskPage?.totalCount ?? page?.totalCount ??
    (detail
      ? Math.max(detail.taskCount, detail.intervalCount, detail.shiftCount ?? 0)
      : 0);
  const field = (key: keyof Omit<typeof form, "overdueOnly">, value: string) =>
    setForm((f) => ({ ...f, [key]: value }));
  return (
    <section aria-label="Khối lượng công việc">
      <h3>Khối lượng công việc và nhật ký thời gian</h3>
      <p>Lịch kỳ: {initialFilters?.calendar ?? "Asia/Ho_Chi_Minh"}.</p>
      <form
        onSubmit={(e) => {
          e.preventDefault();
          load();
        }}
      >
        <label>
          Khối lượng từ ngày
          <input
            type="date"
            value={form.from}
            onChange={(e) => field("from", e.target.value)}
          />
        </label>
        <label>
          Khối lượng đến ngày
          <input
            type="date"
            value={form.to}
            onChange={(e) => field("to", e.target.value)}
          />
        </label>
        <label>
          Mã bộ phận theo dõi khối lượng
          <input
            inputMode="numeric"
            pattern="[1-9][0-9]*"
            value={form.departmentId}
            onChange={(e) => field("departmentId", e.target.value)}
          />
        </label>
        <label>
          Mã nhân viên theo dõi khối lượng
          <input
            inputMode="numeric"
            pattern="[1-9][0-9]*"
            value={form.employeeId}
            disabled={initialFilters?.taskSet !== undefined}
            onChange={(e) => field("employeeId", e.target.value)}
          />
        </label>
        <label>
          Mã độ ưu tiên công việc
          <input
            inputMode="numeric"
            pattern="[1-9][0-9]*"
            value={form.priorityId}
            onChange={(e) => field("priorityId", e.target.value)}
          />
        </label>
        <label>
          Trạng thái công việc
          <select
            aria-label="Trạng thái công việc"
            value={form.status}
            onChange={(e) => field("status", e.target.value)}
          >
            <option value="">Tất cả quy trình</option>
            {taskWorkflowStates.map((s) => (
              <option key={s} value={s}>
                {s.replaceAll("_", " ")}
              </option>
            ))}
          </select>
        </label>
        <label>
          Thời hạn từ
          <input
            type="date"
            value={form.deadlineFrom}
            onChange={(e) => field("deadlineFrom", e.target.value)}
          />
        </label>
        <label>
          Thời hạn đến
          <input
            type="date"
            value={form.deadlineTo}
            onChange={(e) => field("deadlineTo", e.target.value)}
          />
        </label>
        <label>
          Tìm công việc theo khối lượng
          <input
            maxLength={150}
            value={form.search}
            onChange={(e) => field("search", e.target.value)}
          />
        </label>
        <label>
          <input
            type="checkbox"
            checked={form.overdueOnly}
            onChange={(e) =>
              setForm((f) => ({ ...f, overdueOnly: e.target.checked }))
            }
          />
          Chỉ hiện quá hạn
        </label>
        <button disabled={busy}>Áp dụng bộ lọc khối lượng công việc</button>
      </form>
      {busy && <p role="status">Đang tải khối lượng công việc…</p>}
      {error && <p role="alert">{error}</p>}
      <button
        disabled={busy}
        onClick={() => setQuery((q) => q && { ...q, revision: q.revision + 1 })}
      >
        Tải lại khối lượng công việc
      </button>
      {result && (
        <p>
          Ngày {result.from} đến {result.to} ({result.calendar ?? "Asia/Ho_Chi_Minh"}). Bản chụp dữ liệu {result.asOf}. Durations are seconds within this period.
        </p>
      )}
      {taskPage && (
        <section aria-label="Công việc phù hợp với bộ lọc khối lượng">
          <h4>Công việc phù hợp {taskPage.taskSet?.replaceAll("_", " ")}</h4>
          <p>{taskPage.totalCount} công việc phù hợp, gồm cả công việc chưa được phân công.</p>
          {taskPage.messageCode === "MSG10" && <p role="status">MSG10: Không có công việc phù hợp bộ lọc.</p>}
          <table aria-label="Công việc phù hợp">
            <thead><tr><th>Công việc</th><th>Trạng thái</th><th>Người được giao hiện tại</th><th>Thời hạn</th><th>Đóng lúc</th></tr></thead>
            <tbody>{taskPage.tasks.map(t => <tr key={t.taskId}>
              <td><Link to={`/operational-tasks?branchId=${branch}&taskId=${t.taskId}`}>{t.code}: {t.title}</Link></td>
              <td>{displayCode(t.state)}</td><td>{t.currentAssigneeId ?? "Chưa có phân công hiện tại"}</td>
              <td>{t.deadline ?? "—"}{t.overdue && " · Overdue"}</td><td>{t.completedAt ?? "—"}</td>
            </tr>)}</tbody>
          </table>
        </section>
      )}
      {page && (
        <>
          {page.messageCode === "MSG10" && (
            <p role="status">MSG10: Không có khối lượng công việc phù hợp bộ lọc.</p>
          )}
          <table aria-label="Khối lượng công việc của nhân viên">
            <thead>
              <tr>
                <th>Nhân viên</th>
                <th>Công việc đang mở</th>
                <th>Quá hạn</th>
                <th>Đóng trong kỳ</th>
                <th>Thời gian thực hiện đã kết thúc (giây)</th>
                <th>Thời gian đang chạy tạm tính (giây)</th>
                <th>Chi tiết</th>
              </tr>
            </thead>
            <tbody>
              {page.items.map((u) => (
                <tr key={u.userId}>
                  <td>
                    {u.name} ({u.employeeCode})
                  </td>
                  <td>{u.openTasks}</td>
                  <td>{u.overdueTasks}</td>
                  <td>{u.closedTasks}</td>
                  <td>{u.closedEffortSeconds}</td>
                  <td>{u.provisionalSeconds}</td>
                  <td>
                    <button
                      onClick={() =>
                        setQuery(
                          (q) => q && { ...q, employee: u.userId, page: 1 },
                        )
                      }
                    >
                      Nhật ký thời gian của {u.name}
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          <p>
            Số đã đóng đếm công việc riêng biệt có kết quả được chấp nhận của nhân viên trong kỳ. Mở lại vẫn giữ kết quả hoàn tất và thời gian trước đó.
          </p>
        </>
      )}
      {detail && (
        <section aria-label="Nhật ký thời gian nhân viên">
          <h4>{detail.name}</h4>
          <button
            onClick={() =>
              setQuery((q) => q && { ...q, employee: null, page: 1 })
            }
          >
            Quay lại khối lượng công việc của nhân viên
          </button>
          <table aria-label="Công việc của nhân viên">
            <thead>
              <tr>
                <th>Công việc</th>
                <th>Bộ phận</th>
                <th>Độ ưu tiên</th>
                <th>Thời hạn</th>
                <th>Trạng thái</th>
              </tr>
            </thead>
            <tbody>
              {detail.tasks.map((t) => (
                <tr key={t.taskId}>
                  <td>
                    <Link
                      to={`/operational-tasks?branchId=${branch}&taskId=${t.taskId}`}
                    >
                      {t.code}: {t.title}
                    </Link>
                  </td>
                  <td>{t.department}</td>
                  <td>{t.priority ?? "—"}</td>
                  <td>
                    {t.deadline ?? "—"}
                    {t.overdue && " · Overdue"}
                  </td>
                  <td>{displayCode(t.state)}</td>
                </tr>
              ))}
            </tbody>
          </table>
          <table aria-label="Khoảng thời gian thực hiện công việc">
            <thead>
              <tr>
                <th>Khoảng thời gian</th>
                <th>Công việc</th>
                <th>Thời điểm bắt đầu (UTC)</th>
                <th>Thời điểm kết thúc (UTC)</th>
                <th>Đã kết thúc (giây)</th>
                <th>Tạm tính (giây)</th>
              </tr>
            </thead>
            <tbody>
              {detail.intervals.map((i) => (
                <tr key={i.intervalId}>
                  <td>{i.intervalId}</td>
                  <td>{i.taskId}</td>
                  <td>{i.start}</td>
                  <td>{i.end ?? "Đang mở"}</td>
                  <td>{i.closedSeconds}</td>
                  <td>{i.provisionalSeconds}</td>
                </tr>
              ))}
            </tbody>
          </table>
          <h4>Thời gian ngày làm việc / ca — tách biệt với thời gian thực hiện công việc</h4>
          {detail.shifts === null ? (
            <p>Không có thời gian ca nếu thiếu quyền SHIFT_VIEW hiện tại.</p>
          ) : (
            <table aria-label="Thời gian ngày làm việc riêng">
              <thead>
                <tr>
                  <th>Ngày tại khách sạn</th>
                  <th>Thời điểm bắt đầu (UTC)</th>
                  <th>Thời điểm kết thúc (UTC)</th>
                  <th>Đã kết thúc (giây)</th>
                  <th>Tạm tính (giây)</th>
                </tr>
              </thead>
              <tbody>
                {detail.shifts.map((d) => (
                  <tr key={d.workdayId}>
                    <td>{d.workDate}</td>
                    <td>{d.start}</td>
                    <td>{d.end ?? "Đang làm việc"}</td>
                    <td>{d.closedSeconds}</td>
                    <td>{d.provisionalSeconds}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
          <p>
            Bảng công việc, khoảng làm việc và ngày làm việc phân trang riêng bên dưới; bộ lọc công việc không biến thời gian ca thành thời gian thực hiện.
          </p>
        </section>
      )}
      <p>
        Trang {query.page};{" "}
        {page
          ? `${total} Nhân viên`
          : detail
            ? `${detail.taskCount} tasks, ${detail.intervalCount} intervals, ${detail.shiftCount ?? "unavailable"} workdays`
            : ""}
      </p>
      <button
        disabled={busy || query.page <= 1}
        onClick={() => setQuery((q) => q && { ...q, page: q.page - 1 })}
      >
        Trang khối lượng công việc trước
      </button>
      <button
        disabled={busy || !result || query.page * 20 >= total}
        onClick={() => setQuery((q) => q && { ...q, page: q.page + 1 })}
      >
        Trang khối lượng công việc tiếp theo
      </button>
    </section>
  );
}
