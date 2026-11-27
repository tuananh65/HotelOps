import { useState } from "react";
import AttendanceSummary from "./attendance-summary";
import { metricClass } from "./metric-tone";
import {
  Wrench,
  ClipboardCheck,
  Clock,
  TriangleAlert,
  Users,
  CalendarClock,
  CalendarDays,
  ClipboardList,
} from "lucide-react";
import { useAuth } from "./auth";
import { type Row, timestamp } from "./api";
import { Badge, Empty, Pager, State, useQuery, label } from "./ui";
import PageTitle from "./page-title";
import PageToolbar from "./page-toolbar";
import MetricHeading from "./metric-heading";

export default function MaintenanceOverview({
  staff = false,
}: {
  staff?: boolean;
}) {
  const { branch, can, profile } = useAuth();
  const supervisor = profile.roles.some(r=>r.code === "MAINTENANCE_SUPERVISOR" && (r.branchId === null || r.branchId === branch));
  const [revision, bump] = useState(0),
    [search, setSearch] = useState(""),
    [page, setPage] = useState(1);
  const query = useQuery(
    `${staff ? "work/technicians" : "insights/maintenance"}?branchId=${branch}`,
    revision,
  );
  const data = query.data;
  const rows = (data?.items || []).filter((r: Row) =>
    `${r.fullName} ${r.employeeCode}`
      .toLocaleLowerCase()
      .includes(search.toLocaleLowerCase()),
  );
  const metrics = [
    ["open", "Bảo trì đang mở", Wrench],
    ["unassigned", "Chờ tiếp nhận", Users],
    ["inProgress", "Đang thực hiện", Clock],
    ["pendingReview", "Chờ nghiệm thu", ClipboardCheck],
    ["closed", "Đã nghiệm thu", ClipboardCheck],
    ["overdue", "Bảo trì quá hạn", Clock],
    ["incidents", "Sự cố đang mở", TriangleAlert],
    ["severeIncidents", "Sự cố nặng / nghiêm trọng", TriangleAlert],
    ["slaOverdue", "Sự cố vượt hạn SLA", Clock],
    ["pmDue", "Kế hoạch PM đến hạn", CalendarClock],
    ["pmOverdueWork", "Công việc PM quá hạn", CalendarClock],
    ["affectedAssets", "Thiết bị có sự cố mở", Wrench],
  ] as const;
  return (
    <>
      <PageTitle>
        <h1>{staff ? "Nhân viên & Ca trực" : "Tổng quan bảo trì"}</h1>
        <p>
          {staff
            ? "Kỹ thuật viên đủ điều kiện giao việc trong chi nhánh"
            : "Tiếp nhận yêu cầu, phối hợp sửa chữa và theo dõi kết quả kiểm tra"}
        </p>
      </PageTitle>
      <PageToolbar>
        {staff && (
          <label>
            Tìm kỹ thuật viên
            <input
              type="search"
              value={search}
              onChange={(e) => {
                setSearch(e.target.value);
                setPage(1);
              }}
            />
          </label>
        )}
        {can("SHIFT_MANAGE", false) && (
          <a className="button-link" href="#workday">
            <CalendarDays size={16} /> Lịch cá nhân
          </a>
        )}
        {!staff && !supervisor && can("MAINTENANCE_ASSIGN", false) && (
          <a className="button-link" href="#work?kind=MAINTENANCE">
            Phân công bảo trì
          </a>
        )}
      </PageToolbar>
      <State {...query} retry={() => bump((x) => x + 1)}>
        {data &&
          (staff ? (
            <>
              <p>
                Ca trực ngày {data.today}; trạng thái dựa trên ghi nhận thực tế.
              </p>
              <div className="table-scroll">
                <table>
                  <thead>
                    <tr>
                      <th>Kỹ thuật viên</th>
                      <th>Ca hôm nay</th>
                      <th>Đang mở</th>
                      <th>Đã hoàn thành</th>
                      <th>Nhận việc</th>
                    </tr>
                  </thead>
                  <tbody>
                    {rows.slice((page - 1) * 25, page * 25).map((r: Row) => (
                      <tr key={r.userId}>
                        <td>
                          <strong>{r.fullName}</strong>
                          <small>{r.employeeCode}</small>
                        </td>
                        <td>
                          {r.shifts.length
                            ? r.shifts.map((s: Row, i: number) => (
                                <p key={i}>
                                  {s.name} ·{" "}
                                  {String(
                                    Math.floor(s.startMinute / 60),
                                  ).padStart(2, "0")}
                                  :{String(s.startMinute % 60).padStart(2, "0")}
                                  –
                                  {String(
                                    Math.floor(s.endMinute / 60),
                                  ).padStart(2, "0")}
                                  :{String(s.endMinute % 60).padStart(2, "0")}
                                  <small>{label(s.status)}</small>
                                </p>
                              ))
                            : "Chưa có ca"}
                        </td>
                        <td>{r.open}</td>
                        <td>{r.completed}</td>
                        <td>
                          {r.busy
                            ? "Đang thực hiện công việc"
                            : "Không có timer đang chạy"}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              {!rows.length && <Empty />}
              <Pager
                page={page}
                total={rows.length}
                pageSize={25}
                onPage={setPage}
              />
            </>
          ) : (
            <>
              <div className="branch-metrics">
                {metrics
                  .filter(([key]) => data.metrics[key] != null && !(supervisor && ["pmDue", "pmOverdueWork"].includes(key)))
                  .map(([key, title, icon]) => (
                    <article className={metricClass(key)} key={key}>
                      <MetricHeading icon={icon}>{title}</MetricHeading>
                      <strong className="branch-metric-value">
                        {data.metrics[key]}
                      </strong>
                    </article>
                  ))}
              </div>

            </>
          ))}
      </State>
      {!staff && <AttendanceSummary />}
      {!staff && data && !query.error && (
        <State {...query}>
              <div className="chart-grid">
                {[
                  ["unassigned", "Chờ tiếp nhận", Users],
                  ["review", "Chờ nghiệm thu", ClipboardCheck],
                  ["incidents", "Sự cố ưu tiên", TriangleAlert],
                ].map(
                  ([key, title, icon]) =>
                    data[key as string] && (
                      <section className="panel" key={key as string}>
                        <MetricHeading icon={icon as typeof Wrench}>
                          {title as string}
                        </MetricHeading>
                        {data[key as string].map((r: Row) => (
                          <a
                            className="overview-task"
                            key={r.workId}
                            href={`#work?kind=${r.kind}&id=${r.workId}`}
                          >
                            <span>
                              {r.title}
                              <small>
                                {timestamp(r.dueAt || r.completedAt)}
                              </small>
                            </span>
                            <Badge value={r.status} />
                          </a>
                        ))}
                        {!data[key as string].length && <Empty />}
                      </section>
                    ),
                )}
                {!supervisor && data.pm && (
                  <section className="panel">
                    <MetricHeading icon={CalendarClock}>
                      PM đến hạn
                    </MetricHeading>
                    {data.pm.map((r: Row) => (
                      <a
                        className="overview-task"
                        href={`#templates?id=${r.workTemplateId}`}
                        key={r.workTemplateId}
                      >
                        {r.title}
                        <small>{timestamp(r.nextDueAt)}</small>
                      </a>
                    ))}
                    {!data.pm.length && <Empty />}
                  </section>
                )}
                <section className="panel">
                  <MetricHeading icon={ClipboardList}>
                    Tiến độ bảo trì
                  </MetricHeading>
                  {data.progress.map((r: Row) => (
                    <div className="overview-task" key={r.status}>
                      <span>{label(r.status)}</span>
                      <meter
                        min={0}
                        max={Math.max(
                          ...data.progress.map((v: Row) => v.count),
                          1,
                        )}
                        value={r.count}
                      />
                      <strong>{r.count}</strong>
                    </div>
                  ))}
                  {!data.progress.length && <Empty />}
                </section>
              </div>
        </State>
      )}
    </>
  );
}
