import { displayCode } from "../i18n/vi";
import { StatusBadge } from "../components/design-system";
import { Link } from "react-router-dom";
import { isObject } from "../auth/http";
import { staffDate } from "../internalRequests/staffDate";
import type { Values } from "./contracts";

export function MaterialListView({ data }: { data: Values }) {
  const rows = Array.isArray(data.items) ? data.items.filter(isObject) : [];
  return (
    <section className="ops-read-card" aria-label="Theo dõi yêu cầu vật tư">
      <h2>Danh sách yêu cầu</h2>
      <p>
        {String(data.total)} yêu cầu · Trang {String(data.page)}
      </p>
      {rows.length === 0 ? (
        <p className="ops-empty">
          Không có yêu cầu vật tư phù hợp. Thử thay đổi bộ lọc.
        </p>
      ) : (
        <div
          className="ops-read-scroll"
          role="region"
          aria-label="Bảng yêu cầu vật tư"
          tabIndex={0}
        >
          <table className="ops-read-table">
            <thead>
              <tr>
                <th>Yêu cầu</th>
                <th>Trạng thái</th>
                <th>Vật tư</th>
                <th>Thời gian</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((request) => (
                <tr key={String(request.requestId)}>
                  <td>
                    {request.canOpenDetail === false ? (
                      <>
                        <strong>{String(request.requestNo)}</strong>
                        <small>
                          Hồ sơ lưu trữ cần đối soát dữ liệu chi tiết.
                        </small>
                      </>
                    ) : (
                      <Link
                        to={`/operations/material-requests/${encodeURIComponent(String(request.requestId))}?branchId=${encodeURIComponent(String(request.branchId))}`}
                      >
                        {String(request.requestNo)}
                      </Link>
                    )}
                    <small>Người yêu cầu: {String(request.requesterName ?? "Chưa có tên")}</small>
                    <small>
                      {request.assignedApproverUserId
                        ? `Người duyệt: ${request.assignedApproverName ?? "Chưa có tên"}`
                        : "Chưa ghi nhận người duyệt"}
                    </small>
                  </td>
                  <td>
                    <StatusBadge>{displayCode(request.rawStatus)}</StatusBadge>
                  </td>
                  <td>
                    <details>
                      <summary>
                        {Array.isArray(request.quantities)
                          ? request.quantities.length
                          : 0}{" "}
                        dòng vật tư
                      </summary>
                      <ul className="ops-material-lines">
                        {(Array.isArray(request.quantities)
                          ? request.quantities.filter(isObject)
                          : []
                        ).map((line) => (
                          <li key={String(line.requestLineId)}>
                            <strong>
                              {String(line.itemName)} ({String(line.unitName)})
                            </strong>
                            <small>
                              Yêu cầu: {String(line.requestedQty)} · Duyệt:{" "}
                              {String(line.approvedQty ?? "Chưa duyệt")} · Đã
                              cấp: {String(line.issuedQty ?? "Chưa ghi nhận")}
                            </small>
                          </li>
                        ))}
                      </ul>
                    </details>
                  </td>
                  <td>
                    {staffDate(
                      typeof request.createdAt === "string"
                        ? request.createdAt
                        : null,
                    )}
                    <small>
                      Cần lúc:{" "}
                      {staffDate(
                        typeof request.neededAt === "string"
                          ? request.neededAt
                          : null,
                      )}
                    </small>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </section>
  );
}
