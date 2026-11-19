import { ActionLink } from "../components/ActionLink";
import { userMessage } from "../i18n/userMessage";
import { CollapsibleFilterPanel } from "../components/workspace-interactions";
import { StatusBadge } from "../components/design-system";
import { displayCode } from "../i18n/vi";
import { CorrectionRequestForm } from "./CorrectionRequestForm";
import { CreateTimeCorrection } from "./CreateTimeCorrection";
import { useEffect, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import { LoadingIndicator } from "../components/LoadingIndicator";
import { readTimeLog, TimeLogFailure, type TimeLog } from "./timeLogApi";
import { useWorkTimeScope, WorkTimeBranchField } from "./useWorkTimeScope";
import "./work-time.css";

const duration = (seconds: number) =>
  `${Math.floor(seconds / 3600)} giờ ${Math.floor((seconds % 3600) / 60)} phút ${seconds % 60} giây`;
const hotelDate = () =>
  new Intl.DateTimeFormat("en-CA", {
    timeZone: "Asia/Ho_Chi_Minh",
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
  }).format(new Date());
const stamp = (value: string) =>
  new Intl.DateTimeFormat("vi-VN", {
    timeZone: "Asia/Ho_Chi_Minh",
    dateStyle: "short",
    timeStyle: "medium",
  }).format(new Date(value));
export function MyTimeLogPage() {
  const [params] = useSearchParams();
  const scope = useWorkTimeScope("log", Number(params.get("branchId")));
  return (
    <main className="time-workspace">
      <header className="time-heading">
        <div>
          <h1>Nhật ký thời gian của tôi</h1>
          <p>
            Xem thời gian đã ghi nhận và đề nghị điều chỉnh khi có sai sót. Giờ
            Việt Nam (UTC+7).
          </p>
        </div>
        <ActionLink to="/time-corrections">
          Theo dõi đề nghị điều chỉnh
        </ActionLink>
      </header>
      <WorkTimeBranchField scope={scope} />
      {scope.owner && scope.branch && (
        <TimeLogBody
          key={`${scope.owner}:${scope.branch.branchId}`}
          branch={scope.branch.branchId}
        />
      )}
      <nav className="time-links">
        <ActionLink to="/workday">Lịch ca &amp; ngày làm việc</ActionLink>
        <ActionLink to="/home">Trang chủ</ActionLink>
      </nav>
    </main>
  );
}
function TimeLogBody({ branch }: { branch: number }) {
  const { state, actions } = useAuth();
  const owner = state.authorityGeneration;
  const [message, setMessage] = useState("");
  const [date, setDate] = useState(hotelDate);
  const [page, setPage] = useState(1);
  const [revision, setRevision] = useState(0);
  const [result, setResult] = useState<{
    key: string;
    data?: TimeLog;
    error?: string;
  } | null>(null);
  const key = `${owner}:${branch}:${date}:${page}:${revision}`;
  useEffect(() => {
    setResult(null);
    if (!/^\d{4}-\d{2}-\d{2}$/.test(date)) return;
    const controller = new AbortController();
    void readTimeLog(branch, date, page, controller.signal)
      .then((data) => {
        if (!controller.signal.aborted) setResult({ key, data });
      })
      .catch((e: unknown) => {
        if (controller.signal.aborted) return;
        if (e instanceof TimeLogFailure && e.status === 401)
          actions.invalidateSession(owner);
        setResult({
          key,
          error:
            e instanceof TimeLogFailure && e.status === 403
              ? "Bạn không còn quyền xem nhật ký thời gian chi nhánh này."
              : "Chưa tải được nhật ký thời gian. Vui lòng thử lại.",
        });
      });
    return () => controller.abort();
  }, [owner, branch, date, page, revision, actions, key]);
  const visible = result?.key === key ? result : null,
    data = visible?.data;
  return (
    <>
      <div className="time-toolbar">
        <CreateTimeCorrection
          branch={branch}
          onSaved={() => setRevision((n) => n + 1)}
        />
      </div>
      <CollapsibleFilterPanel activeCount={Number(!!date)}>
        <form
          className="time-card time-filters"
          onSubmit={(e) => {
            e.preventDefault();
            setRevision((n) => n + 1);
          }}
        >
          <label>
            Ngày làm việc
            <input
              required
              type="date"
              min="1900-01-01"
              value={date}
              onChange={(e) => {
                setDate(e.target.value);
                setPage(1);
                setMessage("");
              }}
            />
          </label>
          <button type="submit" disabled={!date}>
            Tải lại nhật ký
          </button>
        </form>
      </CollapsibleFilterPanel>
      {message && (
        <p className="time-success" role="status">
          {userMessage(message)}
        </p>
      )}
      {date && !visible && (
        <LoadingIndicator label="Đang tải nhật ký thời gian" />
      )}
      {visible?.error && <p role="alert">{userMessage(visible.error)}</p>}
      {data && (
        <section aria-label="Nhật ký thời gian hằng ngày của tôi">
          <dl className="time-summary">
            <div>
              <dt>Thời gian công việc đã ghi nhận</dt>
              <dd>{duration(data.workClosedSeconds)}</dd>
            </div>
            <div>
              <dt>Thời gian công việc đang chạy</dt>
              <dd>{duration(data.workProvisionalSeconds)}</dd>
            </div>
            <div>
              <dt>Thời gian ca đã kết thúc</dt>
              <dd>{duration(data.workdayClosedSeconds)}</dd>
            </div>
            <div>
              <dt>Thời gian ca đang làm</dt>
              <dd>{duration(data.workdayProvisionalSeconds)}</dd>
            </div>
          </dl>
          <p className="time-muted">
            Cập nhật lúc {stamp(data.asOf)}. Thời gian đang chạy được tạm tính
            tại lần tải gần nhất.
          </p>
          {data.total === 0 ? (
            <p className="time-empty">
              Chưa có khoảng thời gian công việc trong ngày này. Chọn ngày khác
              để xem lịch sử.
            </p>
          ) : (
            <>
              <p>
                Chọn <strong>Đề nghị điều chỉnh</strong> ở khoảng thời gian đã
                kết thúc để gửi yêu cầu cho quản lý.
              </p>
              <div
                className="time-table"
                role="region"
                aria-label="Bảng nhật ký thời gian"
                tabIndex={0}
              >
                <table>
                  <thead>
                    <tr>
                      <th>Công việc</th>
                      <th>Bắt đầu</th>
                      <th>Kết thúc</th>
                      <th>Đã ghi nhận</th>
                      <th>Tạm tính</th>
                      <th>Thao tác</th>
                    </tr>
                  </thead>
                  <tbody>
                    {data.items.map((i) => (
                      <tr key={i.intervalId}>
                        <td>{i.workItemName ?? displayCode(i.kind)}</td>
                        <td>{stamp(i.start)}</td>
                        <td>
                          {i.end ? (
                            stamp(i.end)
                          ) : (
                            <StatusBadge>Đang thực hiện</StatusBadge>
                          )}
                        </td>
                        <td>{duration(i.closedSeconds)}</td>
                        <td>{duration(i.provisionalSeconds)}</td>
                        <td>
                          {i.end ? (
                            <CorrectionRequestForm
                              key={`${owner}:${branch}:${i.intervalId}:${i.version}:${revision}`}
                              branch={branch}
                              interval={i}
                              owner={owner}
                              onSaved={() => {
                                setMessage(
                                  "Đã gửi đề nghị điều chỉnh. Bạn có thể xem kết quả tại Theo dõi đề nghị điều chỉnh.",
                                );
                                setRevision((n) => n + 1);
                              }}
                            />
                          ) : (
                            <span className="time-muted">
                              Có thể đề nghị sau khi kết thúc
                            </span>
                          )}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </>
          )}
          <nav className="time-pagination" aria-label="Phân trang nhật ký">
            <button
              disabled={data.page === 1}
              onClick={() => setPage((p) => p - 1)}
            >
              Trang trước
            </button>
            <span>
              Trang {data.page} · {data.total} khoảng thời gian
            </span>
            <button
              disabled={data.page * data.pageSize >= data.total}
              onClick={() => setPage((p) => p + 1)}
            >
              Trang sau
            </button>
          </nav>
        </section>
      )}
    </>
  );
}
