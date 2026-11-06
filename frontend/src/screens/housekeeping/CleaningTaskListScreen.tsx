import { useEffect, useState, type FormEvent } from "react";
import { Link, useSearchParams } from "react-router-dom";
import {
  listCleaning,
  monitorError,
  type MonitorPage,
} from "../../api/dev1/cleaningMonitor";
import { PageHeader } from "../../ui";
export default function CleaningTaskListScreen() {
  const [params, setParams] = useSearchParams();
  const key = params.toString(),
    branch = params.get("branchId") || "";
  const [result, setResult] = useState<{
    key: string;
    data: MonitorPage;
  } | null>(null);
  const [error, setError] = useState<{ key: string; message: string } | null>(
    null,
  );
  const [retry, setRetry] = useState(0),
    [loading, setLoading] = useState(false);
  useEffect(() => {
    const abort = new AbortController();
    setResult(null);
    setError(null);
    setLoading(Boolean(branch));
    if (branch)
      listCleaning(branch, new URLSearchParams(key), abort.signal)
        .then((data) => {
          if (!abort.signal.aborted) setResult({ key, data });
        })
        .catch((e) => {
          if (!abort.signal.aborted)
            setError({ key, message: monitorError(e) });
        })
        .finally(() => {
          if (!abort.signal.aborted) setLoading(false);
        });
    return () => abort.abort();
  }, [branch, key, retry]);
  function submit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const next = new URLSearchParams();
    new FormData(e.currentTarget).forEach((v, k) => {
      if (String(v).trim()) next.set(k, String(v).trim());
    });
    next.set("page", "1");
    setParams(next);
    setRetry((v) => v + 1);
  }
  const data = result?.key === key ? result.data : null;
  function changePage(page: number) {
    const next = new URLSearchParams(params);
    next.set("page", String(page));
    setParams(next);
  }
  return (
    <>
      <PageHeader id={24} />
      <Link to={`/cleaning/tasks/new?branchId=${encodeURIComponent(branch)}`}>
        Create Cleaning Task
      </Link>
      <p>
        Cleaning Monitor · dữ liệu máy chủ, theo quyền CLEANING_VIEW. Chi nhánh
        là bộ lọc, không cấp quyền.
      </p>
      <form key={key} className="filter-bar" onSubmit={submit}>
        <label>
          Chi nhánh (ID)
          <input
            name="branchId"
            required
            inputMode="numeric"
            defaultValue={branch}
          />
        </label>
        {[
          ["fromDate", "Từ ngày", "date"],
          ["toDate", "Đến ngày", "date"],
          ["floorId", "Tầng (ID)", "number"],
          ["assigneeId", "Nhân viên (ID)", "number"],
          ["priorityId", "Ưu tiên (ID)", "number"],
          ["status", "Trạng thái (mã)", "text"],
          ["search", "Tìm kiếm", "search"],
        ].map(([name, label, type]) => (
          <label key={name}>
            {label}
            <input
              name={name}
              type={type}
              min={type === "number" ? 1 : undefined}
              defaultValue={params.get(name) || ""}
            />
          </label>
        ))}
        <label>
          Loại
          <select name="kind" defaultValue={params.get("kind") || ""}>
            <option value="">Tất cả</option>
            <option value="cleaning">Cleaning</option>
            <option value="rework">Rework</option>
          </select>
        </label>
        <label>
          Quá hạn
          <select name="overdue" defaultValue={params.get("overdue") || ""}>
            <option value="">Tất cả</option>
            <option value="true">Có</option>
            <option value="false">Không</option>
          </select>
        </label>
        <label>
          Số dòng
          <select name="pageSize" defaultValue={params.get("pageSize") || "20"}>
            {[10, 20, 50, 100].map((n) => (
              <option key={n}>{n}</option>
            ))}
          </select>
        </label>
        <button className="primary">Tải dữ liệu</button>
      </form>
      {!branch && <p>Nhập chi nhánh được cấp quyền để xem công việc.</p>}
      {loading && <p role="status">Đang tải công việc…</p>}
      {error?.key === key && (
        <p role="alert">
          {error.message}{" "}
          <button onClick={() => setRetry((v) => v + 1)}>Thử lại</button>
        </p>
      )}
      {data && (
        <>
          <p>
            {data.total} công việc · {data.fromDate} → {data.toDate} ·{" "}
            {data.timezone} · Chi nhánh {branch}
          </p>
          {data.items.length === 0 ? (
            <p>Không có công việc phù hợp bộ lọc.</p>
          ) : (
            <div className="table-wrap">
              <table>
                <thead>
                  <tr>
                    {[
                      "Công việc",
                      "Phòng",
                      "Loại",
                      "Trạng thái",
                      "Nhân viên",
                      "Ưu tiên",
                      "Hạn UTC",
                      "Quá hạn",
                      "Tiến độ",
                    ].map((s) => (
                      <th key={s}>{s}</th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {data.items.map((r) => (
                    <tr key={`${r.kind}:${r.id}`}>
                      <td>
                        <Link
                          to={`/cleaning/tasks/${r.id}?branchId=${branch}&monitor=1&kind=${r.kind}`}
                        >
                          {r.taskNumber}
                        </Link>
                      </td>
                      <td>{r.roomNumber}</td>
                      <td>{r.kind}</td>
                      <td>{r.workflowState ?? r.recordedStatus}</td>
                      <td>{r.assigneeName ?? "Chưa phân công"}</td>
                      <td>{r.priorityCode ?? "—"}</td>
                      <td>{r.dueAtUtc ?? "—"}</td>
                      <td>
                        {r.isOverdue === null
                          ? "Chưa xác định"
                          : r.isOverdue
                            ? "Có"
                            : "Không"}
                      </td>
                      <td>
                        {r.progressPercent === null
                          ? "—"
                          : `${r.progressPercent}%`}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
          <div className="filter-bar">
            <button
              disabled={data.page <= 1}
              onClick={() => changePage(data.page - 1)}
            >
              Trang trước
            </button>
            <span>Trang {data.page}</span>
            <button
              disabled={data.page * data.pageSize >= data.total}
              onClick={() => changePage(data.page + 1)}
            >
              Trang sau
            </button>
          </div>
        </>
      )}
    </>
  );
}
