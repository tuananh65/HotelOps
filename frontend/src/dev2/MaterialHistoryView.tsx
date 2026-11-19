import { displayCode } from "../i18n/vi";
import { staffDate } from "../internalRequests/staffDate";
import { Link } from "react-router-dom";
import { isObject } from "../auth/http";
import type { Values } from "./contracts";
import { useContext } from "react";
import { OperationBranchesContext } from "./AuthorizedOperations";

type Event = {
  key: string;
  title: string;
  at: unknown;
  actor: unknown;
  note?: unknown;
  lines?: Values[];
  fulfillment?: unknown;
  returnLines?: Values[];
};
const rows = (v: unknown): Values[] =>
  Array.isArray(v) ? v.filter(isObject) : [];
const stamp = (v: unknown) =>
  typeof v === "string" && Number.isFinite(Date.parse(v))
    ? Date.parse(v)
    : null;

export function MaterialHistoryView({ record }: { record: Values }) {
  const { paths = [] } = useContext(OperationBranchesContext);
  const cycles = rows(record.cycles);
  const events: Event[] = [
    {
      key: "created",
      title: "Đã tạo yêu cầu",
      at: record.createdAt,
      actor: record.requesterId,
    },
  ];
  for (const cycle of cycles)
    events.push({
      key: `cycle-${cycle.approvalCycleId}`,
      title: `Đã gửi xét duyệt — phiên bản ${cycle.revisionNo}`,
      at: cycle.submittedAt,
      actor: cycle.submittedBy,
    });
  for (const decision of rows(record.decisions))
    events.push({
      key: `decision-${decision.approvalId}`,
      title: String(decision.decision),
      at: decision.decidedAt,
      actor: decision.approverId,
      note: decision.note,
      lines: rows(
        cycles.find((c) => c.approvalCycleId === decision.approvalCycleId)
          ?.lines,
      ),
    });
  for (const batch of rows(record.fulfillments))
    events.push({
      key: `issue-${batch.fulfillmentId}`,
      title: `Xuất kho ${batch.fulfillmentNo} (${displayCode(batch.status)})`,
      at: batch.fulfilledAt,
      actor: batch.fulfilledBy,
      fulfillment: batch.fulfillmentId,
      lines: rows(batch.lines),
      note:
        batch.receiverName == null
          ? "Tên người nhận: Chưa ghi nhận"
          : `Tên người nhận: ${batch.receiverName}`,
    });
  const returns = new Map<unknown, Values[]>();
  for (const line of rows(record.returns))
    returns.set(line.documentId, [
      ...(returns.get(line.documentId) ?? []),
      line,
    ]);
  for (const [id, lines] of returns) {
    const receipt = lines[0];
    events.push({
      key: `return-created-${id}`,
      title: `Hoàn trả ${receipt.documentNo} đã tạo (${displayCode(receipt.status)})`,
      at: receipt.createdAt,
      actor: null,
    });
    if (
      receipt.postedAt != null ||
      receipt.status === "POSTED" ||
      receipt.status === "REVERSED"
    )
      events.push({
        key: `return-posted-${id}`,
        title: `Hoàn trả ${receipt.documentNo} ghi sổ (${displayCode(receipt.status)})`,
        at: receipt.postedAt,
        actor: receipt.postedBy,
        returnLines: lines,
      });
  }
  if (record.remainderClosedAt != null)
    events.push({
      key: "remainder",
      title: "Đã đóng phần còn lại chưa cấp",
      at: record.remainderClosedAt,
      actor: record.remainderClosedBy,
      note: record.remainderCloseReason,
    });
  events.sort(
    (a, b) =>
      (stamp(a.at) ?? Infinity) - (stamp(b.at) ?? Infinity) ||
      a.key.localeCompare(b.key),
  );
  return (
    <section aria-label="Lịch sử xử lý yêu cầu vật tư">
      <h2>Lịch sử quy trình</h2>
      <p>
        Trạng thái hiện tại: <strong>{displayCode(record.rawStatus)}</strong>.
        Thời gian hiển thị theo giờ Việt Nam.
      </p>
      <ol>
        {events.map((event) => (
          <li key={event.key}>
            <strong>{displayCode(event.title)}</strong>
            {" — "}
            {stamp(event.at) == null ? (
              <span>Chưa có thời điểm</span>
            ) : (
              <time dateTime={String(event.at)}>
                {staffDate(String(event.at))}
              </time>
            )}
            {event.note != null && <p>{String(event.note)}</p>}
            {event.returnLines && (
              <ul>
                {event.returnLines.map((line) => (
                  <li
                    key={String(line.returnLineId ?? line.originalIssueLineId)}
                  >
                    {String(
                      line.itemName ?? "Chưa có tên vật tư",
                    )}
                    : số lượng đã ghi nhận {String(line.actualQty ?? "N/A")}, Hư
                    hỏng {String(line.damagedQty ?? "N/A")}{" "}
                    {String(line.unitName ?? "Chưa có tên đơn vị")}.
                  </li>
                ))}
              </ul>
            )}
            {event.lines && event.lines.length > 0 && (
              <ul>
                {event.lines.map((line, index) => (
                  <li key={`${line.requestLineId}:${index}`}>
                    {String(rows(record.lines).find(l => l.requestLineId === line.requestLineId)?.itemName ?? "Chưa có tên vật tư")} ({String(rows(record.lines).find(l => l.requestLineId === line.requestLineId)?.unitName ?? "Chưa có tên đơn vị")}):{" "}
                    {event.fulfillment != null
                      ? `Đã cấp ${line.issuedQty}`
                      : `Đã yêu cầu ${line.requestedQty}; Đã duyệt ${line.approvedQty ?? "Chưa ghi nhận"}`}
                  </li>
                ))}
              </ul>
            )}
            {event.fulfillment != null && (
              <Link
                to={`/operations/material-requests/${encodeURIComponent(String(record.requestId))}?action=A004A-042&fulfillmentId=${encodeURIComponent(String(event.fulfillment))}`}
              >
                Mở biên nhận cấp phát
              </Link>
            )}
          </li>
        ))}
      </ol>
      {record.rawStatus === "CANCELLED" && (
        <p>
          Đã hủy yêu cầu:{" "}
          {String(record.cancellationReason ?? "Lý do chưa khả dụng")}. A
          separate cancellation timestamp is not available in this response.
        </p>
      )}
      <h3>Số lượng hiện tại</h3>
      <ul>
        {rows(record.lines)
          .filter((l) => l.isActive === true)
          .map((line) => (
            <li key={String(line.requestLineId)}>
              {String(line.itemName ?? "Chưa có tên vật tư")}: Đã duyệt{" "}
              {String(line.approvedQty ?? "N/A")}, Đã cấp{" "}
              {String(line.fulfilledQty)}, Còn lại{" "}
              {String(line.historicalRemainingQty ?? "N/A")}{" "}
              {String(line.unitName ?? "Chưa có tên đơn vị")}; hiện đang mở để cấp phát{" "}
              {String(line.openFulfillableQty)}.
            </li>
          ))}
      </ul>
      <h3>Chứng từ hoàn trả</h3>
      <ul>
        {rows(record.fulfillments)
          .filter(
            (f) =>
              f.status === "ISSUED" && typeof f.stockDocumentId === "number",
          )
          .map((f) => (
            <li key={String(f.fulfillmentId)}>
              <Link to={`/inventory/issues/${f.stockDocumentId}/return`}>
                Trả vật tư chưa dùng từ {String(f.fulfillmentNo)}
              </Link>{" "}
              (cần quyền hoàn trả hiện tại và người nhận đủ điều kiện)
            </li>
          ))}
      </ul>
      <p>
        Số lượng hoàn trả phản ánh chứng từ nguồn. Phiếu nháp, đã hủy hoặc đã
        đảo không có nghĩa là tồn khả dụng và không giảm số đã cấp trong lịch
        sử.
      </p>
      {rows(record.returns).length ? (
        <ul>
          {rows(record.returns).map((receipt, index) => (
            <li
              key={`${receipt.documentId}:${receipt.originalIssueLineId}:${index}`}
            >
              <Link
                to={`/inventory/returns/${encodeURIComponent(String(receipt.documentId))}`}
              >
                {String(receipt.documentNo)}
              </Link>{" "}
              — {displayCode(receipt.status)}.
            </li>
          ))}
        </ul>
      ) : (
        <p>Chưa có chứng từ hoàn trả liên quan đã ghi nhận.</p>
      )}
      {paths.includes("/inventory/return-requests") && (
        <p>
          <Link to="/inventory/return-requests">
            Hoàn trả của tôi và hàng đợi kho được phép truy cập
          </Link>
        </p>
      )}
      {record.incidentId != null && (
        <section aria-label="Sự cố liên quan">
          <h3>Sự cố liên quan</h3>
          {isObject(record.linkedIncident) ? (
            <p>
              <Link
                to={`/operations/incidents/${encodeURIComponent(String(record.linkedIncident.incidentId))}`}
              >
                {String(record.linkedIncident.incidentNo)}
              </Link>{" "}
              {String(record.linkedIncident.title)} —{" "}
              {displayCode(record.linkedIncident.status)}
            </p>
          ) : (
            <p>
              Không xem được chi tiết sự cố trong phạm vi hiện tại. Liên kết
              lịch sử vẫn giữ lại.
            </p>
          )}
        </section>
      )}
      <h3>Yêu cầu nội bộ liên quan</h3>
      {record.linkedWorkAvailable !== true ? (
        <p>Chưa có thông tin yêu cầu nội bộ liên quan.</p>
      ) : rows(record.linkedInternalRequests).length === 0 ? (
        <p>Không có yêu cầu nội bộ liên quan trong phạm vi xem hiện tại.</p>
      ) : (
        <ul>
          {rows(record.linkedInternalRequests).map((request) => (
            <li key={String(request.requestId)}>
              <Link
                to={`/internal-requests?requestId=${encodeURIComponent(String(request.requestId))}`}
              >
                {String(request.requestNo)}
              </Link>{" "}
              {displayCode(request.status)};{" "}
              {request.mandatory === true
                ? "ràng buộc vật tư bắt buộc"
                : "ràng buộc vật tư không bắt buộc"}
              .
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
