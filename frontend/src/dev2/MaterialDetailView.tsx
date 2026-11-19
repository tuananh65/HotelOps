import { isObject } from "../auth/http";
import { StatusBadge } from "../components/design-system";
import { displayCode } from "../i18n/vi";
import { staffDate } from "../internalRequests/staffDate";
import type { Values } from "./contracts";

const text = (value: unknown) =>
  typeof value === "string" || typeof value === "number"
    ? String(value)
    : "Chưa ghi nhận";
const time = (value: unknown) =>
  typeof value === "string" ? staffDate(value) : "Chưa ghi nhận";
export function MaterialDetailView({ data }: { data: Values }) {
  const lines = Array.isArray(data.lines) ? data.lines.filter(isObject) : [];
  return (
    <section className="ops-read-card" aria-label="Chi tiết yêu cầu vật tư">
      <h2>{text(data.requestNo)}</h2>
      <StatusBadge>{displayCode(data.rawStatus ?? data.status)}</StatusBadge>
      <p className="ops-description">{text(data.purpose)}</p>
      <dl className="ops-read-facts">
        <div>
          <dt>Người yêu cầu</dt>
          <dd>
            {text(
              data.requesterName,
            )}
          </dd>
        </div>
        <div>
          <dt>Chi nhánh</dt>
          <dd>
            {text(
              data.branchName,
            )}
          </dd>
        </div>
        <div>
          <dt>Ngày tạo</dt>
          <dd>{time(data.createdAt)}</dd>
        </div>
        <div>
          <dt>Thời gian cần vật tư</dt>
          <dd>{time(data.neededAt)}</dd>
        </div>
        <div>
          <dt>Ngày gửi duyệt</dt>
          <dd>{time(data.submittedAt)}</dd>
        </div>
        <div>
          <dt>Ngày hoàn tất</dt>
          <dd>{time(data.completedAt)}</dd>
        </div>
      </dl>
      {!!data.cancellationReason && (
        <p>Lý do hủy: {text(data.cancellationReason)}</p>
      )}
      <h3>Chi tiết vật tư</h3>
      {lines.length ? (
        <div
          className="ops-read-scroll"
          role="region"
          aria-label="Chi tiết số lượng vật tư"
          tabIndex={0}
        >
          <table className="ops-read-table">
            <thead>
              <tr>
                <th>Vật tư</th>
                <th>Đơn vị</th>
                <th>Yêu cầu</th>
                <th>Đã duyệt</th>
                <th>Đã cấp</th>
                <th>Còn được cấp</th>
              </tr>
            </thead>
            <tbody>
              {lines.map((line) => (
                <tr key={String(line.requestLineId)}>
                  <td>
                    {text(line.itemName)}
                    {line.isActive === false && <small>Đã ngừng sử dụng</small>}
                    {!!line.note && <small>{text(line.note)}</small>}
                  </td>
                  <td>{text(line.unitName ?? line.uomName)}</td>
                  <td>{text(line.requestedQty)}</td>
                  <td>{text(line.approvedQty)}</td>
                  <td>{text(line.fulfilledQty)}</td>
                  <td>{text(line.openFulfillableQty)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : (
        <p>Chưa có dòng vật tư.</p>
      )}
    </section>
  );
}
