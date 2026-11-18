import { displayCode } from "../i18n/vi";
import { DashboardSourceRecords } from "./DashboardSourceRecords";
import { HomeNavigation } from "../shared-host/HomeNavigation";
import { DashboardFilters } from "./DashboardFilters";
import { dashboardWorkloadLink } from "./dashboardWorkloadLink";
import { ReportAccessLink } from "./ReportsPage";
import { InternalRequestAccessLink } from "../internalRequests/HistoryPage";
import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import type { AuthorityGeneration } from "../auth/authStore";
import {
  DashboardFailure,
  readDashboard,
  type Dashboard,
} from "./dashboardApi";

const sources: Record<string, string> = {
  "rooms.effective_readiness": "Mức sẵn sàng thực tế của phòng",
  "rooms.operational_status": "Trạng thái phòng đã ghi nhận",
  "cleaning_tasks.status": "Công việc vệ sinh",
  "operational_tasks.status/priority_id": "Công việc vận hành / độ ưu tiên",
  "operational_tasks.workflow_status/priority_id": "Quy trình / độ ưu tiên công việc",
  "incidents.status/severity_id": "Sự cố đang mở / mức độ",
  "maintenance_works.status": "Công việc bảo trì",
  "low_stock.warehouse_id": "Tồn thấp theo kho",
  "preventive_occurrences.workflow_status": "Lần bảo trì định kỳ",
  "stock_movements.category": "Biến động kho theo nhóm vật tư / đơn vị cơ sở",
};
const quantities: Record<string, string> = {
  netStockMovement: "Biến động kho ròng",
  stockReceived: "Đã nhập kho", stockIssued: "Đã xuất kho", transferLosses: "Hao hụt điều chuyển",
  outstandingApproved: "Vật tư đã duyệt còn chờ cấp",
  openReplenishment: "Mở bổ sung vật tư",
};
function RoomLinks({ filters, status }: { filters: Dashboard["filters"]; status?: string }) {
  return <>{filters.branchIds.map(branch => {
    const params = new URLSearchParams({ branchId: String(branch), isActive: "true" });
    if (filters.departmentId !== null) params.set("departmentId", String(filters.departmentId));
    if (status) params.set("readiness", status);
    return <span key={branch}> <Link to={"/rooms/status?" + params}>Phòng trong chi nhánh {branch}</Link></span>;
  })}</>;
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
  const metricSources: Record<string, string> = { openIncidents: "incidents", responseHours: "incidents", resolutionHours: "incidents", slaCompliance: "incidents", reopenedIncidents: "incidents", incidentBlockedRooms: "incidents", preventiveDue: "occurrences", preventiveOverdue: "occurrences", lowStockItems: "stock", transferLosses: "transfers" };
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
                : "Dữ liệu tổng quan chưa khả dụng. Không thay dữ liệu thiếu bằng số 0.",
        });
        if (status === 401) actions.invalidateSession(owner);
      });
    return () => controller.abort();
  }, [owner, key, query, actions, roomsOnly]);
  const visible =
    owner && result?.owner === owner && result.key === key ? result : null;
  const data = visible?.data;
  return (
    <main aria-label="Tổng quan vận hành">
      <h1>{roomsOnly ? "Tổng quan mức sẵn sàng của phòng" : "Tổng quan vận hành"}</h1>
      <nav>
        <Link to={roomsOnly ? "/dashboard" : "/dashboard/rooms"}>{roomsOnly ? "Tổng quan vận hành" : "Tổng quan mức sẵn sàng của phòng"}</Link>{" "}
        <ReportAccessLink />{" "}
        <InternalRequestAccessLink />{" "}
        <HomeNavigation />
      </nav>
      {!owner ? (
        <p role="status">
          Dữ liệu tổng quan được ẩn trong khi kiểm tra phiên.
        </p>
      ) : (
        <>
          <DashboardFilters
            owner={owner}
            roomsOnly={roomsOnly}
            invalidate={actions.invalidateSession}
            apply={(query) => {
              setQuery(query);
              setRetry((n) => n + 1);
            }}
          />
          <button onClick={() => setRetry((n) => n + 1)}>Tải lại</button>
          {visible?.error && <p role="alert">{visible.error}</p>}
          {!visible && <p role="status">Đang tải tổng quan…</p>}
          {data && (
            <>
              <p>
                Bản chụp dữ liệu (UTC):{" "}
                <time dateTime={data.snapshot}>{data.snapshot}</time>
              </p>
              <p>
                Chi nhánh: {data.filters.branchIds.join(", ") || "Không có"}; Bộ phận: {data.filters.departmentId ?? "Tất cả trong phạm vi được phép"}.
              </p>
              <p>
                Khoảng thời gian (UTC): {data.filters.from} đến {data.filters.to}. Applies
                to inspection cycles, stock movements, task effort and tasks closed in period. Room readiness and open or overdue
                tasks show current status at the snapshot time.
              </p>
              {data.missingSources.length > 0 && (
                <>
                <p role="status">
                  MSG29: Tổng quan chưa đầy đủ: một số chỉ số chưa khả dụng. Trạng thái phòng đã ghi không thay thế kết luận sẵn sàng thực tế đã xác minh.
                </p>
                <ul aria-label="Nguồn tổng quan chưa khả dụng">
                  {data.missingSources.map((source, index) => <li key={`${index}:${source}`}>{source}</li>)}
                </ul>
                </>
              )}
              {!roomsOnly && <nav aria-label="Chi tiết từ bảng tổng quan">
                {["incidents", "occurrences", "stock", "requests", "replenishment", "transfers"].map(kind =>
                  <button key={kind} onClick={() => { setMetric(undefined); setSource(kind); }}>Xem {kind} Hồ sơ</button>)}
              </nav>}
              {!roomsOnly && source && <DashboardSourceRecords key={source + key + metric} source={source} metric={metric} filters={data.filters} close={() => setSource(null)} />}
              <section aria-label="Chỉ số tổng quan">
                <dl>
                  {data.metrics.map((m) => (
                    <div key={m.key}>
                      <dt>{m.label}</dt>
                      <dd>
                        <span>{m.value === null
                          ? "Chưa có thông tin"
                          : `${m.value.toLocaleString(undefined, { maximumFractionDigits: 2 })} ${m.unit}`}</span>
                        {!roomsOnly && m.value !== null && metricSources[m.key] && <button onClick={() => { setSource(metricSources[m.key]); setMetric(m.key); }}>Xem hồ sơ nguồn</button>}
                        {m.value !== null && ["activeRooms", "readyRooms", "readyRatio"].includes(m.key) && <RoomLinks filters={data.filters} status={m.key === "activeRooms" ? undefined : "READY"} />}
                        {m.value !== null && dashboardWorkloadLink(data.filters, m.key) && (
                          <> <Link to={dashboardWorkloadLink(data.filters, m.key)!}>Xem lại khối lượng công việc</Link></>
                        )}
                      </dd>
                    </div>
                  ))}
                </dl>
              </section>
              <section aria-label="Phân bố trạng thái">
                <h2>Phân bố trạng thái</h2>
                {data.distributions.length === 0 ? (
                  <p>Không có hồ sơ trạng thái phù hợp.</p>
                ) : (
                  <table>
                    <thead>
                      <tr>
                        <th>Nhóm</th>
                        <th>Trạng thái đã ghi nhận / mã</th>
                        <th>Mã độ ưu tiên / mức độ</th>
                        <th>Tỷ lệ phòng đang hoạt động</th><th>Số lượng</th>
                      </tr>
                    </thead>
                    <tbody>
                      {data.distributions.map((d, i) => (
                        <tr key={i}>
                          <td>{sources[d.source] ?? "Hồ sơ ở trạng thái khác"}</td>
                          <td>{displayCode(d.status)}</td>
                          <td>{d.severity ?? "—"}</td>
                          <td>{d.source === "rooms.effective_readiness" && (data.metrics.find(m => m.key === "activeRooms")?.value ?? 0) > 0 ? ((100 * d.count) / data.metrics.find(m => m.key === "activeRooms")!.value!).toFixed(2) + "%" : "Chưa có thông tin"}</td>
                          <td>{d.count}{d.source === "rooms.effective_readiness" && <RoomLinks filters={data.filters} status={d.status} />}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                )}
              </section>
              {!roomsOnly && <section aria-label="Số lượng vật tư">
                <h2>Số lượng vật tư</h2>
                {!!data.categoryQuantities?.length && <table aria-label="Nhập và xuất theo nhóm vật tư"><thead><tr>
                  <th>Nhóm vật tư</th><th>Kho</th><th>Đơn vị cơ sở</th><th>Đã nhận</th><th>Đã cấp</th>
                </tr></thead><tbody>{data.categoryQuantities.map((q, i) => <tr key={i}>
                  <td>{q.categoryId}</td><td>{q.warehouseId}</td><td>{q.uomId}</td><td>{q.received}</td><td>{q.issued}</td>
                </tr>)}</tbody></table>}
                <p>Số lượng được tách riêng theo vật tư, đơn vị và kho.</p>
                {data.quantities.length === 0 ? (
                  <p>Không có số lượng vật tư phù hợp.</p>
                ) : (
                  <table>
                    <thead>
                      <tr>
                        <th>Nhóm</th>
                        <th>Nhóm vật tư</th><th>Mã vật tư</th>
                        <th>Mã đơn vị</th>
                        <th>Mã kho</th>
                        <th>Số lượng</th>
                      </tr>
                    </thead>
                    <tbody>
                      {data.quantities.map((q, i) => (
                        <tr key={i}>
                          <td>{quantities[q.kind] ?? "Số lượng khác"} <button onClick={() => { setSource(q.kind === "outstandingApproved" ? "requests" : q.kind === "openReplenishment" ? "replenishment" : q.kind === "transferLosses" ? "transfers" : "stock"); setMetric(q.kind); }}>Xem nguồn</button></td>
                          <td>{q.categoryId ?? "N/A"}</td><td>{q.itemId}</td>
                          <td>{q.uomId}</td>
                          <td>{q.warehouseId ?? "—"}</td>
                          <td>{q.quantity}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                )}
              </section>}
            </>
          )}
        </>
      )}
    </main>
  );
}
