import { StatusBadge } from "../components/design-system";
import { Link } from "react-router-dom";
import { isObject } from "../auth/http";
import type { Values } from "./contracts";
import "./incident-list.css";

const positive = (v: unknown): v is number =>
  typeof v === "number" && Number.isSafeInteger(v) && v > 0;
const show = (v: unknown) =>
  typeof v === "string" && v.trim() ? v : "Chưa có thông tin";
const time = (v: unknown) => {
  if (typeof v !== "string" || !/(?:Z|[+-]\d{2}:\d{2})$/.test(v))
    return "Chưa có thời hạn";
  const date = new Date(v);
  return Number.isFinite(date.getTime())
    ? new Intl.DateTimeFormat("vi-VN", {
        timeZone: "Asia/Ho_Chi_Minh",
        dateStyle: "short",
        timeStyle: "medium",
      }).format(date)
    : "Chưa có thời hạn";
};

export function IncidentListView({
  data,
  canDetail,
}: {
  data: Values;
  canDetail: boolean;
}) {
  if (
    !Array.isArray(data.items) ||
    !positive(data.page) ||
    !positive(data.pageSize) ||
    typeof data.totalCount !== "number" ||
    !Number.isSafeInteger(data.totalCount) ||
    data.totalCount < 0 ||
    !data.items.every(
      (r) =>
        isObject(r) &&
        positive(r.incidentId) &&
        positive(r.branchId) &&
        typeof r.incidentNo === "string" &&
        typeof r.incidentTitle === "string" &&
        typeof r.status === "string" &&
        (r.isOverdue === null || typeof r.isOverdue === "boolean"),
    )
  )
    return (
      <p role="alert">Không đọc được danh sách sự cố. Vui lòng tải lại.</p>
    );
  const rows = data.items as Values[];
  const labels: Record<string, string> = {
    branchId: "Chi nhánh",
    departmentId: "Bộ phận thực hiện",
    roomId: "Phòng",
    areaId: "Khu vực",
    assetId: "Tài sản",
    categoryId: "Nhóm",
    severityId: "Mức độ",
    priorityId: "Độ ưu tiên",
    status: "Trạng thái",
    assigneeId: "Người được giao",
    overdue: "Quá hạn",
    detectedFrom: "Phát hiện từ",
    detectedTo: "Phát hiện đến",
    keyword: "Từ khóa",
  };
  const filters = isObject(data.appliedFilters)
    ? Object.entries(data.appliedFilters).filter(
        ([key, value]) =>
          key in labels &&
          value !== null &&
          value !== undefined &&
          value !== "",
      )
    : [];
  const statuses: Record<string, string> = {
    OPEN: "Đang mở",
    IN_PROGRESS: "Đang xử lý",
    RESOLVED: "Đã xử lý",
    CLOSED: "Đã đóng",
    CANCELLED: "Đã hủy",
  };
  return (
    <section className="incident-results" aria-label="Danh sách sự cố">
      <p>
        <strong>{data.totalCount} sự cố</strong> · Trang {data.page} · Giờ Việt
        Nam (UTC+7)
      </p>
      {filters.some(([key]) => key === "keyword") && (
        <p>
          Từ khóa:{" "}
          {String(
            data.appliedFilters && isObject(data.appliedFilters)
              ? data.appliedFilters.keyword
              : "",
          )}
        </p>
      )}
      {rows.length === 0 ? (
        <p>Không có sự cố phù hợp với bộ lọc và phạm vi được xem.</p>
      ) : (
        <>
          <p className="incident-scroll-hint">
            Bấm mã sự cố để xem chi tiết. Cuộn ngang để xem toàn bộ bảng.
          </p>
          <div
            className="incident-table-scroll"
            role="region"
            aria-label="Bảng sự cố, có thể cuộn ngang"
            tabIndex={0}
          >
            <table>
              <thead>
                <tr>
                  <th>Sự cố</th>
                  <th>Vị trí / tài sản</th>
                  <th>Phân loại</th>
                  <th>Trạng thái</th>
                  <th>Phân công</th>
                  <th>Thời hạn</th>
                  <th>Quá hạn</th>
                </tr>
              </thead>
              <tbody>
                {rows.map((r) => (
                  <tr key={String(r.incidentId)}>
                    <td>
                      <span className="incident-reference">
                        {canDetail ? (
                          <Link
                            to={
                              "/operations/incidents/" +
                              r.incidentId +
                              "?branchId=" +
                              r.branchId +
                              "&action=A004A-023"
                            }
                          >
                            {show(r.incidentNo)}
                          </Link>
                        ) : (
                          show(r.incidentNo)
                        )}
                      </span>
                      <p>{show(r.incidentTitle)}</p>
                    </td>
                    <td>
                      {typeof r.roomNumber === "string" && (
                        <strong>Phòng {r.roomNumber}</strong>
                      )}
                      {typeof r.areaName === "string" && (
                        <div>{r.areaName}</div>
                      )}
                      <div>{show(r.assetName)}</div>
                      <small>{show(r.branchName)}</small>
                    </td>
                    <td>
                      <div>Nhóm: {show(r.categoryName)}</div>
                      <div>Mức độ: {show(r.severityName)}</div>
                      <div>Ưu tiên: {show(r.priorityName)}</div>
                    </td>
                    <td>
                      <StatusBadge>
                        {statuses[String(r.status)] ?? show(r.status)}
                      </StatusBadge>
                    </td>
                    <td>
                      <div>
                        {r.assignmentUnavailableReason === "UNASSIGNED"
                          ? "Chưa phân công"
                          : show(r.assigneeName)}
                      </div>
                      <small>{show(r.departmentName)}</small>
                    </td>
                    <td>
                      <div>Phản hồi: {time(r.responseDeadlineAt)}</div>
                      <div>Xử lý: {time(r.resolutionDeadlineAt)}</div>
                      {r.slaUnavailableReason != null && (
                        <small>
                          Thời hạn SLA chưa đầy đủ hoặc chưa khả dụng.
                        </small>
                      )}
                    </td>
                    <td>
                      {r.isOverdue === true
                        ? "Đã quá hạn"
                        : r.isOverdue === false
                          ? "Không"
                          : "Chưa xác định"}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </>
      )}
    </section>
  );
}
