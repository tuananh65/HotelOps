import { useState } from "react";
import AttendanceSummary from "./attendance-summary";
import { metricClass } from "./metric-tone";
import {
  Wrench,
  ClipboardCheck,
  Clock,
  Bell,
  RotateCcw,
  CalendarDays,
  UserCheck,
  TriangleAlert,
} from "lucide-react";
import PageTitle from "./page-title";
import PageToolbar from "./page-toolbar";
import MetricHeading from "./metric-heading";
import { useAuth } from "./auth";
import { type Row, timestamp } from "./api";
import { Badge, Empty, State, useQuery } from "./ui";

export default function MaintenancePersonal() {
  const { branch, can } = useAuth();
  const [revision, bump] = useState(0);
  const query = useQuery(
    `insights/personal?branchId=${branch}&maintenance=true`,
    revision,
  );
  const data = query.data;
  const metrics = [
    ["assignedToday", "Được giao hôm nay", UserCheck],
    ["awaitingAcceptance", "Chờ tiếp nhận", Wrench],
    ["inProgress", "Đang thực hiện", Wrench],
    ["completedToday", "Đã gửi hoàn thành hôm nay", ClipboardCheck],
    ["pendingReview", "Chờ nghiệm thu", ClipboardCheck],
    ["accepted", "Đã được nghiệm thu", ClipboardCheck],
    ["maintenanceRework", "Cần xử lý lại", RotateCcw],
    ["overdue", "Quá hạn cần xử lý", Clock],
    ["reportedIncidents", "Sự cố do tôi báo cáo", TriangleAlert],
    ["unreadNotifications", "Thông báo chưa đọc", Bell],
  ] as const;
  const time = (m: number) =>
    `${String(Math.floor(m / 60)).padStart(2, "0")}:${String(m % 60).padStart(2, "0")}`;
  return (
    <>
      <PageTitle>
        <h1>Tổng quan cá nhân</h1>
        <p>Công việc kỹ thuật được giao và kết quả nghiệm thu của bạn</p>
      </PageTitle>
      <PageToolbar>
        <a className="button-link" href="#work">
          Công việc bảo trì
        </a>
        {can("SHIFT_MANAGE", false) && (
          <a className="button-link" href="#workday">
            Lịch làm việc
          </a>
        )}
      </PageToolbar>
      <State {...query} retry={() => bump((n) => n + 1)}>
        {data && (
          <>
            <div className="branch-metrics">
              {metrics
                .filter(([key]) => data.metrics[key] != null)
                .map(([key, name, icon]) => (
                  <article className={metricClass(key)} key={key}>
                    <MetricHeading icon={icon}>{name}</MetricHeading>
                    <strong className="branch-metric-value">
                      {data.metrics[key]}
                    </strong>
                  </article>
                ))}
              {can("SHIFT_MANAGE", false) && (
                <article className={metricClass("shiftsToday")}>
                  <MetricHeading icon={CalendarDays}>Ca hôm nay</MetricHeading>
                  <strong className="branch-metric-value">
                    {
                      (data.shifts || []).filter(
                        (s: Row) => s.workDate === data.today,
                      ).length
                    }
                  </strong>
                </article>
              )}
            </div>
          </>
        )}
      </State>
      <AttendanceSummary />
      {data && !query.error && (
        <State {...query}>
          <>
            <div className="chart-grid">
              <section className="panel">
                <MetricHeading icon={Wrench}>
                  Việc ưu tiên của tôi
                </MetricHeading>
                {data.priority.map((w: Row) => (
                  <a
                    className="overview-task"
                    key={w.workId}
                    href={`#work?kind=MAINTENANCE&id=${w.workId}`}
                  >
                    <span>
                      {w.title}
                      <small>
                        {w.room ? `Phòng ${w.room} · ` : ""}
                        {timestamp(w.dueAt)}
                      </small>
                    </span>
                    <Badge value={w.status} />
                  </a>
                ))}
                {!data.priority.length && (
                  <Empty>
                    Chưa có công việc bảo trì đang mở được giao cho bạn.
                  </Empty>
                )}
                <a
                  className="button-link"
                  href="#work?kind=MAINTENANCE&queue=rework"
                >
                  Việc cần xử lý lại
                </a>
              </section>
              {can("SHIFT_MANAGE", false) && (
                <section className="panel">
                  <MetricHeading icon={CalendarDays}>
                    Ca hôm nay & sắp tới
                  </MetricHeading>
                  <p>
                    {data.today} · {data.timezone}
                  </p>
                  {(data.shifts || []).map((s: Row) => (
                    <article className="personal-shift" key={s.assignmentId}>
                      <strong>{s.name}</strong>
                      <small>
                        {s.workDate} · {time(s.startMinute)}–{time(s.endMinute)}
                      </small>
                      <Badge value={s.status} />
                      {(s.startedAt || s.endedAt) && (
                        <small>
                          Vào: {timestamp(s.startedAt)} · Ra:{" "}
                          {timestamp(s.endedAt)}
                        </small>
                      )}
                    </article>
                  ))}
                  {!data.shifts?.length && (
                    <Empty>
                      Chưa có ca được phân công hôm nay hoặc trong 7 ngày tới.
                    </Empty>
                  )}
                </section>
              )}
            </div>
            <PageToolbar>
              <a className="button-link" href="#history">
                Lịch sử công việc
              </a>
              <a className="button-link" href="#notifications">
                Thông báo & kết quả nghiệm thu
              </a>
            </PageToolbar>
          </>
        </State>
      )}
    </>
  );
}
