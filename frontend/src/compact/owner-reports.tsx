import { useState } from "react";
import { SlidersHorizontal } from "lucide-react";
import PageTitle from "./page-title";
import PageToolbar from "./page-toolbar";
import OwnerOverview from "./owner-overview";
import { useAuth } from "./auth";
import { dateKey, timestamp, type Row } from "./api";
import { Badge, Dialog, Empty, Pager, State, useQuery } from "./ui";
import { downloadReportCsv } from "./report-csv";

export const ownerReportTypes = [
  ["overview", "Tổng quan & so sánh", "REPORT_VIEW"],
  ["rooms", "Tình trạng phòng hiện tại", "ROOM_VIEW"],
  ["cleaning", "Housekeeping", "CLEANING_VIEW"],
  ["inspection", "Inspection", "INSPECTION_VIEW"],
  ["maintenance", "Bảo trì", "MAINTENANCE_VIEW"],
  ["incidents", "Incident / SLA", "INCIDENT_VIEW"],
  ["tasks", "Công việc vận hành", "TASK_VIEW"],
  ["requests", "Yêu cầu nội bộ", "IR_VIEW"],
  ["materials", "Yêu cầu vật tư", "MR_VIEW"],
  ["assets", "Tài sản hiện tại", "ASSET_VIEW"],
  ["stock", "Tồn kho hiện tại", "INVENTORY_VIEW"],
  ["documents", "Chứng từ kho", "INVENTORY_VIEW"],
  ["preventive", "Kế hoạch bảo trì định kỳ", "MAINTENANCE_VIEW"],
  ["staffing", "Nhân sự & phân ca tổng hợp", "REPORT_VIEW"],
];
export default function OwnerReports({
  scope,
  onlyReport,
}: {
  scope: number;
  onlyReport?: string;
}) {
  const { canGlobal } = useAuth();
  const visible = ownerReportTypes.filter((t) => canGlobal(t[2]));
  const [report, setReport] = useState(
    onlyReport ||
      visible.find(
        (t) =>
          t[0] ===
          new URLSearchParams(location.hash.split("?")[1]).get("report"),
      )?.[0] ||
      visible[0]?.[0] ||
      "overview",
  );
  const [from, setFrom] = useState(
      dateKey(new Date(Date.now() - 29 * 86400000)),
    ),
    [to, setTo] = useState(dateKey()),
    [filters, setFilters] = useState(false),
    [search, setSearch] = useState(""),
    [status, setStatus] = useState(""),
    [sort, setSort] = useState("latest"),
    [page, setPage] = useState(1),
    [revision, bump] = useState(0);
  const [id, setId] = useState<number | null>(
    onlyReport === "preventive"
      ? Number(new URLSearchParams(location.hash.split("?")[1]).get("id")) ||
          null
      : null,
  );
  const query = useQuery(
    report === "overview"
      ? null
      : `insights/owner/reports/${report}?${scope ? `branchId=${scope}&` : ""}from=${from}&to=${to}&search=${encodeURIComponent(search)}&status=${encodeURIComponent(status)}&sort=${sort}&page=${page}`,
    revision,
  );
  const detail = useQuery(id ? `insights/owner/preventive/${id}` : null);
  const snapshot = ["rooms", "assets", "stock", "preventive"].includes(report);
  const staffRows = (query.data?.items || [])
    .filter((r: Row) =>
      `${r.branch} ${r.department}`
        .toLowerCase()
        .includes(search.toLowerCase()),
    )
    .sort((a: Row, b: Row) =>
      sort === "title"
        ? a.department.localeCompare(b.department, "vi")
        : sort === "oldest"
          ? b.branch.localeCompare(a.branch, "vi")
          : a.branch.localeCompare(b.branch, "vi"),
    );
  const href = (r: Row) =>
    r.kind === "ROOM"
      ? `#rooms?room=${r.id}`
      : r.kind === "ASSET"
        ? `#assets?id=${r.id}`
        : [
              "CLEANING",
              "INSPECTION",
              "MAINTENANCE",
              "OPERATIONAL",
              "INCIDENT",
              "INTERNAL_REQUEST",
            ].includes(r.kind)
          ? `#work?kind=${r.kind}&id=${r.id}`
          : r.kind === "STOCK"
            ? "#resources?tab=stock"
            : `#documents?kind=${r.kind}&id=${r.id}`;
  function preset(days: number) {
    setFrom(dateKey(new Date(Date.now() - (days - 1) * 86400000)));
    setTo(dateKey());
    setPage(1);
  }
  return (
    <>
      {report !== "overview" && (
        <PageTitle>
          <h1>
            {visible.find((t) => t[0] === report)?.[1] || "Báo cáo & Phân tích"}
          </h1>
          <p>
            {snapshot
              ? "Số liệu hiện tại, không phải số dư lịch sử"
              : report === "staffing"
                ? "Nhân sự hiện tại và phân ca trong kỳ · Asia/Ho_Chi_Minh"
                : "Lọc theo ngày tạo · các mốc hoàn thành và nghiệm thu được hiển thị riêng · Asia/Ho_Chi_Minh"}
          </p>
        </PageTitle>
      )}
      {!onlyReport && (
        <PageToolbar>
          <label className="search">
            Loại báo cáo
            <select
              value={report}
              onChange={(e) => {
                setReport(e.target.value);
                setPage(1);
                setStatus("");
                setSearch("");
                setId(null);
                setFilters(false);
              }}
            >
              {visible.map(([v, n]) => (
                <option key={v} value={v}>
                  {n}
                </option>
              ))}
            </select>
          </label>
        </PageToolbar>
      )}
      {report === "overview" ? (
        <OwnerOverview scope={scope} report />
      ) : (
        <>
          <PageToolbar
            actions={
              <button
                disabled={
                  query.loading ||
                  !!query.error ||
                  !(report === "staffing"
                    ? staffRows.length
                    : query.data?.items?.length)
                }
                onClick={() =>
                  downloadReportCsv(
                    report === "staffing"
                      ? staffRows.slice((page - 1) * 25, page * 25)
                      : query.data.items,
                    `hotelops-owner-${report}-page-${page}`,
                  )
                }
              >
                Xuất trang CSV
              </button>
            }
          >
            <label className="search">
              Tìm trong báo cáo
              <input
                value={search}
                placeholder={
                  report === "staffing"
                    ? "Tên chi nhánh hoặc bộ phận"
                    : "Mã hoặc tên đối tượng"
                }
                onChange={(e) => {
                  setSearch(e.target.value);
                  setPage(1);
                }}
              />
            </label>
            <button
              aria-expanded={filters}
              onClick={() => setFilters(!filters)}
            >
              <SlidersHorizontal size={16} /> Bộ lọc
            </button>
          </PageToolbar>
          {filters && (
            <PageToolbar>
              {!snapshot && (
                <>
                  <label>
                    Từ ngày
                    <input
                      type="date"
                      value={from}
                      max={to}
                      onChange={(e) => {
                        setFrom(e.target.value);
                        setPage(1);
                      }}
                    />
                  </label>
                  <label>
                    Đến ngày
                    <input
                      type="date"
                      value={to}
                      min={from}
                      onChange={(e) => {
                        setTo(e.target.value);
                        setPage(1);
                      }}
                    />
                  </label>
                  <label>
                    Khoảng nhanh
                    <select
                      defaultValue="30"
                      onChange={(e) => preset(Number(e.target.value))}
                    >
                      <option value="1">Hôm nay</option>
                      <option value="7">7 ngày</option>
                      <option value="30">30 ngày</option>
                    </select>
                  </label>
                </>
              )}
              {report !== "staffing" && (
                <>
                  <label>
                    Trạng thái
                    <input
                      value={status}
                      placeholder="Mã trạng thái, ví dụ CLOSED"
                      onChange={(e) => {
                        setStatus(e.target.value);
                        setPage(1);
                      }}
                    />
                  </label>
                  <label>
                    Sắp xếp
                    <select
                      value={sort}
                      onChange={(e) => {
                        setSort(e.target.value);
                        setPage(1);
                      }}
                    >
                      <option value="latest">Mới nhất</option>
                      <option value="oldest">Cũ nhất</option>
                      <option value="title">Tên</option>
                    </select>
                  </label>
                </>
              )}
              {report === "staffing" && (
                <label>
                  Sắp xếp
                  <select
                    value={sort}
                    onChange={(e) => {
                      setSort(e.target.value);
                      setPage(1);
                    }}
                  >
                    <option value="latest">Chi nhánh A–Z</option>
                    <option value="oldest">Chi nhánh Z–A</option>
                    <option value="title">Bộ phận A–Z</option>
                  </select>
                </label>
              )}
              <button
                onClick={() => {
                  setSearch("");
                  setStatus("");
                  setSort("latest");
                  preset(30);
                }}
              >
                Đặt lại
              </button>
            </PageToolbar>
          )}
          <State {...query} retry={() => bump((n) => n + 1)}>
            {report === "staffing" ? (
              <>
                <p className="hint">
                  Số nhân viên hiện tại; phân ca và số công việc gửi hoàn thành
                  trong {from} – {to}. Không hiển thị hồ sơ cá nhân.
                </p>
                <div className="table-scroll">
                  <table>
                    <thead>
                      <tr>
                        {[
                          "Chi nhánh",
                          "Bộ phận",
                          "Nhân viên",
                          "Được phân ca",
                          "Lượt phân ca",
                          "Sức chứa ca",
                          "Lấp đầy ca",
                          "Ca thiếu người",
                          "Công việc hoàn thành",
                        ].map((n) => (
                          <th key={n}>{n}</th>
                        ))}
                      </tr>
                    </thead>
                    <tbody>
                      {staffRows
                        .slice((page - 1) * 25, page * 25)
                        .map((r: Row) => (
                          <tr key={r.departmentId}>
                            {[
                              r.branch,
                              r.department,
                              r.employees,
                              r.assignedStaff,
                              r.assignedSlots,
                              r.capacity,
                              r.capacity > 0
                                ? `${Math.round((100 * r.assignedSlots) / r.capacity)}% (${r.assignedSlots}/${r.capacity})`
                                : "—",
                              r.underfilledShifts,
                              r.completedTasks,
                            ].map((v, i) => (
                              <td key={i}>{v}</td>
                            ))}
                          </tr>
                        ))}
                    </tbody>
                  </table>
                </div>
                {!staffRows.length && <Empty />}
                <Pager
                  page={page}
                  pageSize={25}
                  total={staffRows.length}
                  onPage={setPage}
                />
              </>
            ) : (
              <>
                <div className="table-scroll">
                  <table>
                    <thead>
                      <tr>
                        <th>Chi nhánh</th>
                        <th>Đối tượng</th>
                        <th>Trạng thái</th>
                        <th>Tạo / Ghi nhận</th>
                        <th>Hoàn thành</th>
                        <th>Nghiệm thu / Duyệt</th>
                        <th>{snapshot ? "Mốc liên quan" : "Hạn / SLA"}</th>
                        <th>Số lượng / đơn vị</th>
                        <th>Thao tác</th>
                      </tr>
                    </thead>
                    <tbody>
                      {query.data?.items?.map((r: Row) => (
                        <tr key={`${r.branchId}-${r.id}-${r.title}`}>
                          <td>{r.branch}</td>
                          <td>
                            {r.title}
                            <small>{r.code}</small>
                          </td>
                          <td>
                            <Badge
                              value={
                                r.kind === "MAINTENANCE" &&
                                r.status === "COMPLETED"
                                  ? "Chờ nghiệm thu"
                                  : r.status
                              }
                            />
                          </td>
                          <td>{timestamp(r.createdAt)}</td>
                          <td>{timestamp(r.completedAt)}</td>
                          <td>{timestamp(r.verifiedAt)}</td>
                          <td>{timestamp(r.dueAt)}</td>
                          <td>
                            {r.quantity != null
                              ? `${r.quantity} ${r.unit || ""}`
                              : "—"}
                          </td>
                          <td>
                            {r.kind === "PREVENTIVE" ? (
                              <button onClick={() => setId(r.id)}>
                                Xem chi tiết
                              </button>
                            ) : (
                              <a className="button-link" href={href(r)}>
                                Xem chi tiết
                              </a>
                            )}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
                {!query.data?.total && <Empty />}
                <Pager
                  page={page}
                  total={query.data?.total || 0}
                  pageSize={25}
                  onPage={setPage}
                />
              </>
            )}
          </State>
        </>
      )}
      {id && (
        <Dialog
          className="room-drawer"
          title="Kế hoạch bảo trì"
          close={() => setId(null)}
        >
          <State {...detail}>
            {detail.data && (
              <>
                <h2>{detail.data.template.title}</h2>
                <p>
                  {detail.data.template.code} · {detail.data.template.frequency}{" "}
                  · {detail.data.template.frequencyInterval}
                </p>
                <p>{detail.data.template.instruction}</p>
                <p>
                  Lần tiếp theo: {timestamp(detail.data.template.nextDueAt)}
                </p>
                <h3>Các công việc đã phát sinh</h3>
                {detail.data.occurrences.map((w: Row) => (
                  <a
                    className="overview-task"
                    key={w.workId}
                    href={`#work?kind=MAINTENANCE&id=${w.workId}`}
                  >
                    <span>{w.title}</span>
                    <Badge value={w.status} />
                  </a>
                ))}
                {!detail.data.occurrences.length && <Empty />}
              </>
            )}
          </State>
        </Dialog>
      )}
    </>
  );
}
