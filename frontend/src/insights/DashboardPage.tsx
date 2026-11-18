import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import {
  Activity,
  AlertTriangle,
  BedDouble,
  Box,
  ClipboardList,
  ExternalLink,
  RefreshCw,
  Wrench,
} from "lucide-react";
import { displayCode } from "../i18n/vi";
import { useAuth } from "../auth/AuthProvider";
import type { AuthorityGeneration } from "../auth/authStore";
import {
  AppButton,
  AppDrawer,
  ContentCard,
  DataTable,
  EmptyState,
  ErrorState,
  LoadingSkeleton,
  WorkspacePageHeader,
} from "../components/design-system";
import { DashboardSourceRecords } from "./DashboardSourceRecords";
import { HomeNavigation } from "../shared-host/HomeNavigation";
import { DashboardFilters } from "./DashboardFilters";
import { dashboardWorkloadLink } from "./dashboardWorkloadLink";
import { ReportAccessLink } from "./ReportsPage";
import { InternalRequestAccessLink } from "../internalRequests/HistoryPage";
import {
  DashboardFailure,
  readDashboard,
  type Dashboard,
} from "./dashboardApi";
import "./dashboard.css";

const sources: Record<string, string> = {
  "rooms.effective_readiness": "Mức sẵn sàng thực tế của phòng",
  "rooms.operational_status": "Trạng thái phòng đã ghi nhận",
  "cleaning_tasks.status": "Công việc vệ sinh",
  "operational_tasks.status/priority_id": "Công việc vận hành / độ ưu tiên",
  "operational_tasks.workflow_status/priority_id":
    "Quy trình / độ ưu tiên công việc",
  "incidents.status/severity_id": "Sự cố đang mở / mức độ",
  "maintenance_works.status": "Công việc bảo trì",
  "low_stock.warehouse_id": "Tồn thấp theo kho",
  "preventive_occurrences.workflow_status": "Bảo trì định kỳ",
  "stock_movements.category": "Biến động kho theo nhóm vật tư / đơn vị cơ sở",
};
const sourceLabels: Record<string, string> = {
  incidents: "Sự cố",
  occurrences: "Bảo trì định kỳ",
  stock: "Tồn kho",
  requests: "Yêu cầu vật tư",
  replenishment: "Bổ sung vật tư",
  transfers: "Điều chuyển kho",
};
const quantityLabels: Record<string, string> = {
  netStockMovement: "Biến động kho ròng",
  stockReceived: "Đã nhập kho",
  stockIssued: "Đã xuất kho",
  transferLosses: "Hao hụt điều chuyển",
  outstandingApproved: "Vật tư đã duyệt còn chờ cấp",
  openReplenishment: "Bổ sung vật tư đang mở",
};
const metricLabels: Record<string, string> = {
  activeRooms: "Phòng đang hoạt động",
  readyRooms: "Phòng sẵn sàng",
  readyRatio: "Tỷ lệ sẵn sàng",
  openIncidents: "Sự cố đang mở",
  lowStockItems: "Vật tư tồn thấp",
  preventiveDue: "Bảo trì đến hạn",
  preventiveOverdue: "Bảo trì quá hạn",
  responseHours: "Thời gian phản hồi",
  resolutionHours: "Thời gian xử lý",
  slaCompliance: "Đáp ứng SLA",
  reopenedIncidents: "Sự cố mở lại",
  incidentBlockedRooms: "Phòng bị khóa do sự cố",
};
const metricSources: Record<string, string> = {
  openIncidents: "incidents",
  responseHours: "incidents",
  resolutionHours: "incidents",
  slaCompliance: "incidents",
  reopenedIncidents: "incidents",
  incidentBlockedRooms: "incidents",
  preventiveDue: "occurrences",
  preventiveOverdue: "occurrences",
  lowStockItems: "stock",
  transferLosses: "transfers",
};
const formatTime = (value: string) =>
  new Intl.DateTimeFormat("vi-VN", {
    dateStyle: "short",
    timeStyle: "short",
    timeZone: "Asia/Ho_Chi_Minh",
  }).format(new Date(value));
function RoomLinks({
  filters,
  status,
}: {
  filters: Dashboard["filters"];
  status?: string;
}) {
  return (
    <>
      {filters.branchIds.map((branch) => {
        const params = new URLSearchParams({
          branchId: String(branch),
          isActive: "true",
        });
        if (filters.departmentId !== null)
          params.set("departmentId", String(filters.departmentId));
        if (status) params.set("readiness", status);
        return (
          <Link key={branch} to={"/rooms/status?" + params}>
            Xem phòng · chi nhánh {branch}
            <ExternalLink size={12} aria-hidden="true" />
          </Link>
        );
      })}
    </>
  );
}
export function DashboardPage({ roomsOnly = false }: { roomsOnly?: boolean }) {
  const { state, actions } = useAuth();
  const owner =
    state.status === "AUTHENTICATED" &&
    state.session &&
    state.pending === null &&
    !state.session.mustChangePassword
      ? state.authorityGeneration
      : null;
  const [query, setQuery] = useState("");
  const [source, setSource] = useState<string | null>(null);
  const [metric, setMetric] = useState<string | undefined>();
  const [retry, setRetry] = useState(0);
  const key = `${roomsOnly}:${query}:${retry}`;
  const [result, setResult] = useState<{
    owner: AuthorityGeneration;
    key: string;
    data?: Dashboard;
    error?: string;
  } | null>(null);
  useEffect(() => {
    if (!owner) return;
    const controller = new AbortController();
    setResult(null);
    void readDashboard(query, controller.signal, roomsOnly)
      .then((data) => {
        if (!controller.signal.aborted) setResult({ owner, key, data });
      })
      .catch((error: unknown) => {
        if (controller.signal.aborted) return;
        const status = error instanceof DashboardFailure ? error.status : 0;
        setResult({
          owner,
          key,
          error:
            status === 403
              ? "Bạn không có quyền xem phạm vi tổng quan này."
              : status === 400
                ? "Kiểm tra bộ lọc đã chọn."
                : "Dữ liệu tổng quan chưa khả dụng. Vui lòng thử lại.",
        });
        if (status === 401) actions.invalidateSession(owner);
      });
    return () => controller.abort();
  }, [owner, key, query, actions, roomsOnly]);
  const visible =
    owner && result?.owner === owner && result.key === key ? result : null;
  const data = visible?.data;
  const roomStates =
    data?.distributions.filter(
      (item) => item.source === "rooms.effective_readiness",
    ) ?? [];
  const roomCount = data?.metrics.find(
    (item) => item.key === "activeRooms",
  )?.value;
  const totalShown = roomStates.reduce((sum, item) => sum + item.count, 0);
  const openSource = (kind: string, metric?: string) => {
    setMetric(metric);
    setSource(kind);
  };
  return (
    <main className="ds-dashboard" aria-label="Tổng quan vận hành">
      <WorkspacePageHeader
        title={
          roomsOnly ? "Tổng quan mức sẵn sàng của phòng" : "Tổng quan vận hành"
        }
        description="Theo dõi tình trạng phòng, công việc và nguồn lực trong phạm vi được phân quyền."
        actions={
          <AppButton
            icon={RefreshCw}
            disabled={!owner || !visible}
            onClick={() => setRetry((value) => value + 1)}
          >
            Làm mới
          </AppButton>
        }
      />
      <nav className="ds-dashboard-tabs" aria-label="Loại tổng quan">
        <Link
          className={!roomsOnly ? "is-selected" : undefined}
          to="/dashboard"
        >
          Vận hành
        </Link>
        <Link
          className={roomsOnly ? "is-selected" : undefined}
          to="/dashboard/rooms"
        >
          Mức sẵn sàng của phòng
        </Link>
        <ReportAccessLink />
        <InternalRequestAccessLink />
        <HomeNavigation />
      </nav>
      {!owner ? (
        <LoadingSkeleton rows={3} label="Đang kiểm tra phiên" />
      ) : (
        <>
          <DashboardFilters
            owner={owner}
            roomsOnly={roomsOnly}
            invalidate={actions.invalidateSession}
            apply={(value) => {
              setQuery(value);
              setSource(null);
              setRetry((number) => number + 1);
            }}
          />
          {visible?.error && (
            <ErrorState
              message={visible.error}
              retry={() => setRetry((value) => value + 1)}
            />
          )}
          {!visible && <LoadingSkeleton rows={5} label="Đang tải tổng quan" />}
          {data && (
            <>
              <div className="ds-snapshot">
                <span>
                  <Activity size={14} aria-hidden="true" /> Cập nhật:{" "}
                  <time dateTime={data.snapshot}>
                    {formatTime(data.snapshot)}
                  </time>{" "}
                  · Giờ Việt Nam
                </span>
                <span>
                  {data.filters.branchIds.length} chi nhánh trong phạm vi ·{" "}
                  {data.filters.departmentId === null
                    ? "Tất cả bộ phận được phép"
                    : `Bộ phận ${data.filters.departmentId}`}
                </span>
              </div>
              {data.missingSources.length > 0 && (
                <details className="ds-missing">
                  <summary>
                    <AlertTriangle size={16} aria-hidden="true" />
                    Một số chỉ số chưa khả dụng
                  </summary>
                  <p>
                    Dữ liệu thiếu được hiển thị là chưa có thông tin, không thay
                    bằng số 0. Trạng thái phòng đã ghi nhận không thay thế kết
                    luận sẵn sàng thực tế.
                  </p>
                  <ul>
                    {data.missingSources.map((item, index) => (
                      <li key={`${index}:${item}`}>{sources[item] ?? item}</li>
                    ))}
                  </ul>
                </details>
              )}
              <section className="ds-kpi-grid" aria-label="Chỉ số tổng quan">
                {data.metrics.map((item) => {
                  const Icon = /room|ready/i.test(item.key)
                    ? BedDouble
                    : /incident/i.test(item.key)
                      ? AlertTriangle
                      : /stock|material|replenishment/i.test(item.key)
                        ? Box
                        : /preventive|maintenance/i.test(item.key)
                          ? Wrench
                          : ClipboardList;
                  const workload = dashboardWorkloadLink(
                    data.filters,
                    item.key,
                  );
                  return (
                    <article className="ds-kpi" key={item.key}>
                      <div className="ds-kpi-label">
                        <span>{metricLabels[item.key] ?? item.label}</span>
                        <Icon size={20} aria-hidden="true" />
                      </div>
                      <strong className="ds-kpi-value">
                        {item.value === null ? (
                          <span className="ds-kpi-unavailable">
                            Chưa có thông tin
                          </span>
                        ) : (
                          <>
                            {item.value.toLocaleString("vi-VN", {
                              maximumFractionDigits: 2,
                            })}
                            <small>{item.unit}</small>
                          </>
                        )}
                      </strong>
                      <div className="ds-kpi-links">
                        {!roomsOnly &&
                          item.value !== null &&
                          metricSources[item.key] && (
                            <button
                              type="button"
                              onClick={() =>
                                openSource(metricSources[item.key], item.key)
                              }
                            >
                              Xem hồ sơ nguồn
                              <ExternalLink size={12} aria-hidden="true" />
                            </button>
                          )}
                        {item.value !== null &&
                          ["activeRooms", "readyRooms", "readyRatio"].includes(
                            item.key,
                          ) && (
                            <RoomLinks
                              filters={data.filters}
                              status={
                                item.key === "activeRooms" ? undefined : "READY"
                              }
                            />
                          )}
                        {item.value !== null && workload && (
                          <Link to={workload}>
                            Xem khối lượng công việc
                            <ExternalLink size={12} aria-hidden="true" />
                          </Link>
                        )}
                      </div>
                    </article>
                  );
                })}
              </section>
              <div className="ds-dashboard-summaries">
                <ContentCard title="Mức sẵn sàng của phòng">
                  {roomStates.length ? (
                    <>
                      <div className="ds-readiness-heading">
                        <BedDouble size={20} aria-hidden="true" />
                        <strong>
                          {roomCount === null || roomCount === undefined
                            ? "Chưa có tổng số phòng"
                            : `${roomCount} phòng đang hoạt động`}
                        </strong>
                      </div>
                      <div className="ds-readiness-bar" aria-hidden="true">
                        {roomStates.map((item, index) => (
                          <span
                            key={index}
                            className={`ds-chart-segment ds-chart-segment--${index % 4}`}
                            style={{ flexGrow: item.count }}
                          />
                        ))}
                      </div>
                      <ul className="ds-state-list">
                        {roomStates.map((item, index) => (
                          <li key={index}>
                            <span>
                              <i
                                className={`ds-chart-segment ds-chart-segment--${index % 4}`}
                              />
                              {displayCode(item.status)}
                            </span>
                            <strong>{item.count}</strong>
                            <span>
                              {roomCount && roomCount > 0
                                ? `${((100 * item.count) / roomCount).toFixed(1)}%`
                                : "—"}
                            </span>
                          </li>
                        ))}
                      </ul>
                      {roomCount !== null &&
                        roomCount !== undefined &&
                        totalShown !== roomCount && (
                          <p className="ds-caption">
                            Phân bố hiện có chưa bao gồm toàn bộ phòng. Xem chi
                            tiết để đối chiếu.
                          </p>
                        )}
                    </>
                  ) : (
                    <EmptyState
                      icon={BedDouble}
                      title="Chưa có phân bố phòng"
                      description="Dữ liệu sẽ xuất hiện khi nguồn mức sẵn sàng của phòng khả dụng."
                    />
                  )}
                </ContentCard>
                <ContentCard title="Theo dõi nghiệp vụ">
                  {roomsOnly ? (
                    <p className="ds-caption">
                      Mức sẵn sàng được xác định từ lệnh khóa hiệu lực và chu kỳ
                      vệ sinh. Chọn chỉ số phòng để xem hồ sơ chi tiết.
                    </p>
                  ) : (
                    <div className="ds-source-grid">
                      {Object.entries(sourceLabels).map(([kind, label]) => (
                        <button
                          type="button"
                          key={kind}
                          onClick={() => openSource(kind)}
                        >
                          <span className="ds-source-icon">
                            {kind === "incidents" ? (
                              <AlertTriangle size={18} aria-hidden="true" />
                            ) : kind === "occurrences" ? (
                              <Wrench size={18} aria-hidden="true" />
                            ) : (
                              <Box size={18} aria-hidden="true" />
                            )}
                          </span>
                          <span>{label}</span>
                          <ExternalLink size={14} aria-hidden="true" />
                        </button>
                      ))}
                    </div>
                  )}
                  <p className="ds-caption">
                    Các bản ghi chi tiết được kiểm tra quyền theo từng nghiệp vụ
                    và chi nhánh.
                  </p>
                </ContentCard>
              </div>
              <ContentCard title="Phân bố trạng thái">
                <DataTable
                  label="Phân bố trạng thái"
                  empty={!data.distributions.length}
                >
                  <thead>
                    <tr>
                      <th scope="col">Nhóm</th>
                      <th scope="col">Trạng thái</th>
                      <th scope="col">Ưu tiên / mức độ</th>
                      <th scope="col">Tỷ lệ phòng hoạt động</th>
                      <th scope="col">Số lượng</th>
                    </tr>
                  </thead>
                  <tbody>
                    {data.distributions.map((item, index) => (
                      <tr key={index}>
                        <td>
                          {sources[item.source] ?? "Hồ sơ ở trạng thái khác"}
                        </td>
                        <td>{displayCode(item.status)}</td>
                        <td>{item.severity ?? "—"}</td>
                        <td>
                          {item.source === "rooms.effective_readiness" &&
                          roomCount &&
                          roomCount > 0
                            ? `${((100 * item.count) / roomCount).toFixed(2)}%`
                            : "—"}
                        </td>
                        <td>
                          {item.count}
                          {item.source === "rooms.effective_readiness" && (
                            <div className="ds-kpi-links">
                              <RoomLinks
                                filters={data.filters}
                                status={item.status}
                              />
                            </div>
                          )}
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </DataTable>
              </ContentCard>
              {!roomsOnly && (
                <ContentCard title="Số lượng vật tư">
                  <p className="ds-caption">
                    Số lượng được tách riêng theo vật tư, đơn vị và kho. Khoảng
                    thời gian lọc: {data.filters.from} đến {data.filters.to}{" "}
                    (UTC).
                  </p>
                  {!!data.categoryQuantities?.length && (
                    <DataTable label="Nhập và xuất theo nhóm vật tư">
                      <thead>
                        <tr>
                          <th>Nhóm vật tư</th>
                          <th>Kho</th>
                          <th>Đơn vị cơ sở</th>
                          <th>Đã nhận</th>
                          <th>Đã cấp</th>
                        </tr>
                      </thead>
                      <tbody>
                        {data.categoryQuantities.map((item, index) => (
                          <tr key={index}>
                            <td>{item.categoryId}</td>
                            <td>{item.warehouseId}</td>
                            <td>{item.uomId}</td>
                            <td>{item.received}</td>
                            <td>{item.issued}</td>
                          </tr>
                        ))}
                      </tbody>
                    </DataTable>
                  )}
                  <DataTable
                    label="Số lượng vật tư"
                    empty={!data.quantities.length}
                  >
                    <thead>
                      <tr>
                        <th>Nhóm</th>
                        <th>Nhóm vật tư</th>
                        <th>Vật tư</th>
                        <th>Đơn vị</th>
                        <th>Kho</th>
                        <th>Số lượng</th>
                        <th>Chi tiết</th>
                      </tr>
                    </thead>
                    <tbody>
                      {data.quantities.map((item, index) => (
                        <tr key={index}>
                          <td>
                            {quantityLabels[item.kind] ?? "Số lượng khác"}
                          </td>
                          <td>{item.categoryId ?? "—"}</td>
                          <td>{item.itemId}</td>
                          <td>{item.uomId}</td>
                          <td>{item.warehouseId ?? "—"}</td>
                          <td>{item.quantity}</td>
                          <td>
                            <AppButton
                              variant="ghost"
                              icon={ExternalLink}
                              onClick={() =>
                                openSource(
                                  item.kind === "outstandingApproved"
                                    ? "requests"
                                    : item.kind === "openReplenishment"
                                      ? "replenishment"
                                      : item.kind === "transferLosses"
                                        ? "transfers"
                                        : "stock",
                                  item.kind,
                                )
                              }
                            >
                              Xem nguồn
                            </AppButton>
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </DataTable>
                </ContentCard>
              )}
              <details className="ds-dashboard-notes">
                <summary>Thông tin về phạm vi dữ liệu</summary>
                <p>
                  Khoảng thời gian UTC áp dụng cho chu kỳ kiểm tra, biến động
                  kho, thời gian thực hiện và công việc đã đóng. Trạng thái
                  phòng, công việc đang mở hoặc quá hạn được ghi nhận tại thời
                  điểm cập nhật dữ liệu.
                </p>
              </details>
              <AppDrawer
                open={!!source}
                title={
                  source
                    ? `Chi tiết ${sourceLabels[source]?.toLowerCase() ?? "nghiệp vụ"}`
                    : "Chi tiết"
                }
                onClose={() => setSource(null)}
                size="large"
              >
                {source && (
                  <DashboardSourceRecords
                    key={source + key + metric}
                    source={source}
                    metric={metric}
                    filters={data.filters}
                    close={() => setSource(null)}
                  />
                )}
              </AppDrawer>
            </>
          )}
        </>
      )}
    </main>
  );
}
