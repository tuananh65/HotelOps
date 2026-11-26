import { useState } from "react";
import { metricClass } from "./metric-tone";
import {
  BedDouble,
  Building2,
  ClipboardList,
  Clock,
  PackageSearch,
  SlidersHorizontal,
} from "lucide-react";
import PageTitle from "./page-title";
import PageToolbar from "./page-toolbar";
import MetricHeading from "./metric-heading";
import StatChart from "./stat-chart";
import { Dialog, State, useQuery } from "./ui";
import { dateKey, timestamp, type Row } from "./api";
import { downloadReportCsv } from "./report-csv";

export const ownerMetrics = [
  ["branches", "Chi nhánh", Building2],
  ["rooms", "Tổng phòng", BedDouble],
  ["ready", "Phòng sẵn sàng", BedDouble],
  ["occupied", "Phòng có khách", BedDouble],
  ["needCleaning", "Phòng cần vệ sinh", BedDouble],
  ["pendingInspection", "Phòng chờ kiểm tra", BedDouble],
  ["blocked", "Phòng bị chặn", BedDouble],
  ["CLEANING", "Cleaning đang mở", ClipboardList],
  ["MAINTENANCE", "Bảo trì chưa đóng", ClipboardList],
  ["INCIDENT_unresolved", "Sự cố chưa xử lý", ClipboardList],
  ["INCIDENT_overdue", "Sự cố chưa đóng quá hạn SLA", Clock],
  ["OPERATIONAL", "Công việc vận hành mở", ClipboardList],
  ["INTERNAL_REQUEST", "Yêu cầu nội bộ mở", ClipboardList],
  ["pendingMaterials", "Vật tư chờ duyệt", PackageSearch],
  ["assets", "Tài sản đang sử dụng", PackageSearch],
  ["stockedItems", "Mặt hàng theo dõi tồn", PackageSearch],
  ["lowStock", "Cặp kho / vật tư dưới định mức", PackageSearch],
  ["emptyStock", "Cặp kho / vật tư hết tồn", PackageSearch],
] as const;
export function OwnerComparison({
  data,
  open,
}: {
  data: Row;
  open?: (b: Row) => void;
}) {
  return (
    <>
      <div className="chart-grid">
        <StatChart
          title="Số phòng theo chi nhánh"
          rows={data.branches
            .filter((b: Row) => b.metrics.rooms != null)
            .map((b: Row) => ({ name: b.name, value: b.metrics.rooms }))}
        />
        <StatChart
          title="Công việc phát sinh trong kỳ"
          rows={data.branches.map((b: Row) => ({
            name: b.name,
            value: b.metrics.created || 0,
          }))}
        />
      </div>
      <div className="table-scroll">
        <table className="mobile-records">
          <thead>
            <tr>
              <th>Chi nhánh</th>
              <th>Phòng</th>
              <th>Có khách</th>
              <th>Bị chặn</th>
              <th>Cleaning mở</th>
              <th>Bảo trì mở</th>
              <th>Sự cố mở</th>
              <th>Phát sinh trong kỳ</th>
              <th>Hoàn thành trong kỳ</th>
              {open && <th>Chi tiết</th>}
            </tr>
          </thead>
          <tbody>
            {data.branches.map((b: Row) => (
              <tr key={b.branchId}>
                <td data-label="Chi nhánh">
                  {b.name}
                  <small>{b.code}</small>
                </td>
                {[
                  "rooms",
                  "occupied",
                  "blocked",
                  "CLEANING",
                  "MAINTENANCE",
                  "INCIDENT",
                  "created",
                  "completed",
                ].map((k) => (
                  <td key={k} data-label={({rooms:"Phòng",occupied:"Có khách",blocked:"Bảo trì",CLEANING:"Dọn phòng mở",MAINTENANCE:"Bảo trì mở",INCIDENT:"Sự cố mở",created:"Phát sinh trong kỳ",completed:"Hoàn thành trong kỳ"} as Record<string,string>)[k]}>{b.metrics[k] ?? "—"}</td>
                ))}
                {open && (
                  <td data-label="Chi tiết">
                    <button onClick={() => open(b)}>Xem chi tiết</button>
                  </td>
                )}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  );
}
export default function OwnerOverview({
  scope,
  branchesOnly = false,
  report = false,
}: {
  scope: number;
  branchesOnly?: boolean;
  report?: boolean;
}) {
  const [group, setGroup] = useState("week");
  const [from, setFrom] = useState(
      dateKey(new Date(Date.now() - 29 * 86400000)),
    ),
    [to, setTo] = useState(dateKey()),
    [filters, setFilters] = useState(false),
    [revision, bump] = useState(0),
    [detail, setDetail] = useState<Row | null>(null);
  const path = `${report ? "insights/owner/reports/overview" : "insights/owner"}?${scope ? `branchId=${scope}&` : ""}from=${from}&to=${to}`;
  const query = useQuery(path, revision),
    data = query.data;
  return (
    <>
      <PageTitle>
        <h1>
          {branchesOnly
            ? "So sánh chi nhánh"
            : report
              ? "Báo cáo tổng quan"
              : "Tổng quan hệ thống"}
        </h1>
        <p>Giám sát vận hành trong phạm vi được cấp quyền</p>
      </PageTitle>
      <PageToolbar
        actions={
          report &&
          data && (
            <button
              disabled={query.loading}
              onClick={() =>
                downloadReportCsv(
                  data.branches.map((b: Row) => ({
                    branch: b.name,
                    code: b.code,
                    from: data.from,
                    to: data.to,
                    updatedAt: data.updatedAt,
                    ...b.metrics,
                  })),
                  "hotelops-owner-overview",
                )
              }
            >
              Xuất bảng so sánh CSV
            </button>
          )
        }
      >
        <button aria-expanded={filters} onClick={() => setFilters(!filters)}>
          <SlidersHorizontal size={16} /> Bộ lọc
        </button>
        <span className="hint">
          Kỳ phát sinh / hoàn thành: {from} – {to}
        </span>
      </PageToolbar>
      {filters && (
        <PageToolbar>
          <label>
            Từ ngày
            <input
              type="date"
              value={from}
              max={to}
              onChange={(e) => setFrom(e.target.value)}
            />
          </label>
          <label>
            Đến ngày
            <input
              type="date"
              value={to}
              min={from}
              onChange={(e) => setTo(e.target.value)}
            />
          </label>
          <label>
            Gộp biểu đồ theo
            <select value={group} onChange={(e) => setGroup(e.target.value)}>
              <option value="day">Ngày</option>
              <option value="week">Tuần</option>
              <option value="month">Tháng</option>
            </select>
          </label>
          <button
            onClick={() => {
              setFrom(dateKey(new Date(Date.now() - 29 * 86400000)));
              setTo(dateKey());
            }}
          >
            Đặt lại
          </button>
        </PageToolbar>
      )}
      <State {...query} retry={() => bump((n) => n + 1)}>
        {data && (
          <>
            <p className="hint">
              Trạng thái hiện tại cập nhật lúc {timestamp(data.updatedAt)}.
              Khoảng ngày áp dụng cho phát sinh và hoàn thành; mỗi chi nhánh
              dùng múi giờ của mình. Hoàn thành là lần gửi kết quả gần nhất,
              chưa đồng nghĩa đã nghiệm thu; các đối tượng vận hành được đếm
              riêng.
            </p>
            {!branchesOnly && (
              <div className="branch-metrics">
                {ownerMetrics
                  .filter(([k]) => data.metrics[k] != null)
                  .map(([k, name, icon]) => (
                    <article className={metricClass(k)} key={k}>
                      <MetricHeading icon={icon}>{name}</MetricHeading>
                      <strong className="branch-metric-value">
                        {data.metrics[k]}
                      </strong>
                    </article>
                  ))}
              </div>
            )}
            {branchesOnly && (
              <div className="branch-metrics">
                {data.branches.map((b: Row) => (
                  <article className="panel" key={b.branchId}>
                    <MetricHeading icon={Building2}>{b.name}</MetricHeading>
                    <p>
                      {b.code} · {b.timezone}
                    </p>
                    <p>
                      Phòng: {b.metrics.rooms ?? "—"} · Có khách:{" "}
                      {b.metrics.occupied ?? "—"}
                    </p>
                    <p>
                      Phát sinh trong kỳ: {b.metrics.created} · Hoàn thành:{" "}
                      {b.metrics.completed}
                    </p>
                    <button onClick={() => setDetail(b)}>Xem chi tiết</button>
                  </article>
                ))}
              </div>
            )}
          </>
        )}
      </State>
      {data && !query.error && (
        <State {...query}>
          <>
            <OwnerComparison data={data} open={setDetail} />
            <div className="chart-grid">
              <StatChart
                title="Tình trạng vệ sinh hiện tại"
                rows={[
                  { name: "Sẵn sàng", value: data.metrics.ready || 0 },
                  {
                    name: "Cần vệ sinh",
                    value: data.metrics.needCleaning || 0,
                  },
                  {
                    name: "Chờ kiểm tra",
                    value: data.metrics.pendingInspection || 0,
                  },
                ]}
                type="bars"
              />
              <StatChart
                title="Tài sản theo tình trạng"
                rows={data.assetStates.map((a: Row) => ({
                  name: a.condition,
                  value: a.count,
                }))}
                type="donut"
              />
            </div>
            <div className="chart-grid">
              <StatChart
                title={`Phát sinh theo ${group === "day" ? "ngày" : group === "week" ? "tuần" : "tháng"} (công việc)`}
                rows={timelineRows(data, group, "created")}
              />
              <StatChart
                title={`Hoàn thành theo ${group === "day" ? "ngày" : group === "week" ? "tuần" : "tháng"} (công việc)`}
                rows={timelineRows(data, group, "completed")}
              />
            </div>

          </>
        </State>
      )}
      {detail && (
        <Dialog
          title={detail.name}
          className="room-drawer"
          close={() => setDetail(null)}
        >
          <p>
            {detail.code} · {detail.timezone}
          </p>
          <p>
            Phát sinh: {detail.metrics.created} · Hoàn thành:{" "}
            {detail.metrics.completed} trong kỳ.
          </p>
          {ownerMetrics
            .filter(([k]) => detail.metrics[k] != null)
            .map(([k, name]) => (
              <p key={k}>
                {name}: <strong>{detail.metrics[k]}</strong>
              </p>
            ))}
          <a className="button-link" href="#reports">
            Xem báo cáo
          </a>
        </Dialog>
      )}
    </>
  );
}
function timelineRows(data: Row, group: string, metric: string) {
  const values = new Map<string, number>();
  for (const row of data.timeline) {
    const day = new Date(`${row.day}T00:00:00Z`);
    if (group === "week")
      day.setUTCDate(day.getUTCDate() - ((day.getUTCDay() + 6) % 7));
    const period =
      group === "month" ? row.day.slice(0, 7) : day.toISOString().slice(0, 10);
    const branch = data.branches.find((b: Row) => b.branchId === row.branchId);
    const key = `${period} · ${branch?.code || branch?.name || row.branchId}`;
    values.set(key, (values.get(key) || 0) + row[metric]);
  }
  return [...values]
    .sort(([a], [b]) => a.localeCompare(b))
    .map(([name, value]) => ({ name, value }));
}
