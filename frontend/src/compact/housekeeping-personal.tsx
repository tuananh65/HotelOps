import { useState } from "react";
import AttendanceSummary from "./attendance-summary";
import { metricClass } from "./metric-tone";
import {
  Bell,
  CalendarDays,
  ClipboardCheck,
  ClipboardList,
  Clock,
  Play,
  RotateCcw,
  UserCheck,
} from "lucide-react";
import PageTitle from "./page-title";
import PageToolbar from "./page-toolbar";
import MetricHeading from "./metric-heading";
import { useAuth } from "./auth";
import { timestamp, type Row } from "./api";
import { Badge, Empty, State, useQuery } from "./ui";

const time = (value: number) =>
  `${String(Math.floor(value / 60)).padStart(2, "0")}:${String(value % 60).padStart(2, "0")}`;
export default function HousekeepingPersonal() {
  const { branch, can } = useAuth();
  const [revision, bump] = useState(0);
  const query = useQuery(`insights/personal?branchId=${branch}`, revision);
  const data = query.data;
  const metrics = [
    ["assignedToday", "Được giao hôm nay", UserCheck, "#cleaning"],
    ["awaitingAcceptance", "Chưa nhận", ClipboardList, "#cleaning"],
    ["inProgress", "Đang thực hiện", Play, "#cleaning"],
    [
      "completedToday",
      "Đã gửi hoàn thành hôm nay",
      ClipboardCheck,
      "#cleaning",
    ],
    ["pendingInspection", "Chờ kiểm tra", ClipboardCheck, "#cleaning"],
    ["rework", "Làm lại đang mở", RotateCcw, "#work?kind=REWORK"],
    ["overdue", "Quá hạn cần xử lý", Clock, "#cleaning"],
    ["unreadNotifications", "Thông báo chưa đọc", Bell, "#notifications"],
  ] as const;
  return (
    <>
      <PageTitle>
        <h1>Tổng quan cá nhân</h1>
        <p>Công việc và ca làm việc của bạn</p>
      </PageTitle>
      <PageToolbar>
        {can("CLEANING_VIEW") && (
          <a className="button-link" href="#cleaning">
            Dọn phòng & làm lại
          </a>
        )}
        {can("TASK_VIEW") && (
          <a className="button-link" href="#work">
            Công việc khác
          </a>
        )}
      </PageToolbar>
      <State {...query} retry={() => bump((n) => n + 1)}>
        {data && (
          <>
            <div className="branch-metrics">
              {metrics.map(([key, name, icon, href]) => (
                <article className={metricClass(key)} key={key}>
                  <MetricHeading icon={icon}>{name}</MetricHeading>
                  <strong className="branch-metric-value">
                    {data.metrics[key]}
                  </strong>
                  {(key === "unreadNotifications" ||
                    (key === "rework" && can("CLEANING_VIEW"))) && (
                    <a href={href}>Xem chi tiết →</a>
                  )}
                </article>
              ))}
            </div>
          </>
        )}
      </State>
      <AttendanceSummary />
      {data && !query.error && (
        <State {...query}>
          <>
            <div className="chart-grid personal-hk-details">
              <section className="panel">
                <MetricHeading icon={ClipboardList}>
                  Việc ưu tiên của tôi
                </MetricHeading>
                {data.priority.map((work: Row) => (
                  <a
                    className="overview-task"
                    key={work.workId}
                    href={`#work?kind=${work.kind}&id=${work.workId}`}
                  >
                    <span>
                      {work.title}
                      <small>
                        {work.room ? `Phòng ${work.room} · ` : ""}
                        {timestamp(work.dueAt)}
                      </small>
                    </span>
                    <Badge value={work.status} />
                  </a>
                ))}
                {!data.priority.length && (
                  <Empty>Chưa có công việc đang mở được giao cho bạn.</Empty>
                )}
              </section>
              {can("ATTENDANCE_RECORD") && (
                <section className="panel">
                  <MetricHeading icon={CalendarDays}>
                    Ca làm việc của tôi
                  </MetricHeading>
                  <p>
                    Hôm nay: {data.today} · {data.timezone}
                  </p>
                  {(data.shifts || []).map((shift: Row) => (
                    <article
                      className="personal-shift"
                      key={shift.assignmentId}
                    >
                      <strong>{shift.name}</strong>
                      <small>
                        {shift.workDate} · {time(shift.startMinute)}–
                        {time(shift.endMinute)}
                      </small>
                      <Badge value={shift.status} />
                      {(shift.startedAt || shift.endedAt) && (
                        <small>
                          Vào: {timestamp(shift.startedAt)} · Ra:{" "}
                          {timestamp(shift.endedAt)}
                        </small>
                      )}
                    </article>
                  ))}
                  {!data.shifts?.length && (
                    <Empty>
                      Bạn chưa có ca được phân công hôm nay hoặc trong 7 ngày
                      tới.
                    </Empty>
                  )}
                  <a className="button-link" href="#workday">
                    Mở lịch làm việc
                  </a>
                </section>
              )}
            </div>
          </>
        </State>
      )}
    </>
  );
}
