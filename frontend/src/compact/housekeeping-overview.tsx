import { useState } from "react";
import AttendanceSummary from "./attendance-summary";
import { metricClass } from "./metric-tone";
import {
  BedDouble,
  ClipboardCheck,
  ClipboardList,
  Clock,
  TriangleAlert,
  Users,
} from "lucide-react";
import { type Row, timestamp } from "./api";
import { useAuth } from "./auth";
import { Badge, Empty, State, useQuery, label } from "./ui";
import PageTitle from "./page-title";
import PageToolbar from "./page-toolbar";
import MetricHeading from "./metric-heading";
import StatChart from "./stat-chart";

export default function HousekeepingOverview() {
  const { branch, can } = useAuth();
  const [revision, bump] = useState(0);
  const query = useQuery(`insights/housekeeping?branchId=${branch}`, revision),
    data = query.data;
  const metrics = [
    ["roomsNeedCleaning", "Phòng cần dọn", BedDouble],
    ["roomsInProgress", "Phòng đang dọn", BedDouble],
    ["roomsPendingInspection", "Phòng chờ kiểm tra", ClipboardCheck],
    ["roomsReady", "Phòng đạt vệ sinh", ClipboardCheck],
    ["unassigned", "Việc dọn chưa giao", Users],
    ["inProgress", "Việc dọn đang thực hiện", ClipboardList],
    ["overdue", "Việc dọn quá hạn", Clock],
    ["pendingInspection", "Phiếu kiểm tra đang mở", ClipboardCheck],
    ["rework", "Công việc làm lại", ClipboardList],
    ["incidents", "Sự cố phòng đang mở", TriangleAlert],
    ["defects", "Lỗi kiểm tra chưa xử lý", TriangleAlert],
  ] as const;
  return (
    <>
      <PageTitle>
        <h1>Tổng quan Housekeeping</h1>
        <p>Điều phối vệ sinh và nghiệm thu trong chi nhánh</p>
      </PageTitle>
      <PageToolbar>
        {can("CLEANING_ASSIGN") && (
          <a className="button-link" href="#cleaning">
            Phòng cần tạo việc
          </a>
        )}
        {can("INSPECTION_VIEW") && (
          <a className="button-link" href="#inspection">
            Phòng chờ kiểm tra
          </a>
        )}
        {can("SHIFT_MANAGE", false) && (
          <a className="button-link" href="#workday">
            Lịch cá nhân
          </a>
        )}
      </PageToolbar>
      <State {...query} retry={() => bump((x) => x + 1)}>
        {data && (
          <>
            <small>
              Cập nhật {timestamp(data.updatedAt)} · Mức sẵn sàng vệ sinh độc
              lập với tình trạng có khách và chặn vận hành.
            </small>
            <div className="branch-metrics">
              {metrics
                .filter(([key]) => data.metrics[key] != null)
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
        )}
      </State>
      <AttendanceSummary />
      {data && !query.error && (
        <State {...query}>
          <>
            <div className="chart-grid">
              {data.turnover && (
                <section className="panel">
                  <h2>Tình trạng vệ sinh phòng</h2>
                  {data.turnover.map((r: Row) => (
                    <p key={`${r.status}-${r.occupied}`}>
                      <Badge value={r.status} /> ·{" "}
                      {r.occupied ? "Có khách" : "Trống"}:{" "}
                      <strong>{r.count} phòng</strong>
                    </p>
                  ))}
                  {!data.turnover.length && <Empty />}
                </section>
              )}
              {data.workload && (
                <StatChart
                  title="Khối lượng dọn phòng (công việc)"
                  rows={data.workload.map((r: Row) => ({
                    name: label(r.status),
                    value: r.count,
                  }))}
                  href="#cleaning"
                />
              )}
              {[
                ["priority", "Nhiệm vụ ưu tiên"],
                ["waiting", "Phòng chờ kiểm tra"],
                ["overdue", "Công việc quá hạn"],
              ].map(
                ([key, title]) =>
                  data[key] && (
                    <section className="panel" key={key}>
                      <h2>{title}</h2>
                      {data[key].map((r: Row) => (
                        <a
                          className="overview-task"
                          key={r.workId}
                          href={`#work?kind=${r.kind}&id=${r.workId}`}
                        >
                          {r.room && `Phòng ${r.room} · `}
                          {r.title}
                          <Badge value={r.status} />
                          {r.dueAt && <small>Hạn {timestamp(r.dueAt)}</small>}
                        </a>
                      ))}
                      {!data[key].length && <Empty />}
                    </section>
                  ),
              )}
            </div>
          </>
        </State>
      )}
    </>
  );
}
