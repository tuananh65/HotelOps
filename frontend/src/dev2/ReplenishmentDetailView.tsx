import { isObject } from "../auth/http";
import type { Values } from "./contracts";

const rows = (value: unknown): Values[] => Array.isArray(value) ? value.filter(isObject) : [];
const display = (value: unknown) => value == null || value === "" ? "Chưa ghi nhận" : String(value);

export function ReplenishmentDetailView({ record }: { record: Values }) {
  const lines = rows(record.lines);
  const receipts = rows(record.receipts);
  return <section aria-label="Chi tiết yêu cầu bổ sung">
    <h3>{display(record.replenishmentNo)}</h3>
    <p>Trạng thái: {display(record.rawStatus)}; Kho: {display(record.warehouseId)}; Chi nhánh: {display(record.branchId)}</p>
    <p>Nhà cung cấp: {display(record.supplierName)}; dự kiến: {display(record.expectedOn)}; Đã tạo: {display(record.createdAt)}</p>
    <table><caption>Số lượng theo đơn vị cơ sở của từng vật tư</caption>
      <thead><tr><th>Vật tư</th><th>Đơn vị</th><th>Đã yêu cầu</th><th>Thực nhận</th><th>Còn lại</th><th>Phạm vi bao phủ</th><th>Ghi chú</th></tr></thead>
      <tbody>{lines.map(line => <tr key={String(line.replenishmentLineId)}>
        <td>{display(line.itemName)}</td><td>{display(line.unitName)}</td><td>{display(line.requestedBaseQty)}</td>
        <td>{display(line.netReceivedBaseQty)}</td><td>{display(line.remainingBaseQty)}</td>
        <td>{line.isOpenCoverage === true ? "Đang mở" : "Đã đóng"}</td><td>{display(line.note)}</td>
      </tr>)}</tbody>
    </table>
    <h4>Bằng chứng phiếu nhập liên quan</h4>
    <p>Chỉ phiếu nhập đã ghi sổ hợp lệ mới tính vào số đã nhận. Số lượng dưới đây theo đơn vị cơ sở của vật tư liên quan.</p>
    {receipts.length === 0 ? <p>Chưa có phiếu nhập liên quan.</p> : <table>
      <thead><tr><th>Phiếu nhập</th><th>Vật tư</th><th>Trạng thái</th><th>Đã ghi nhận</th><th>Có hiệu lực</th><th>Đảo chứng từ</th><th>Liên kết lúc</th></tr></thead>
      <tbody>{receipts.map(receipt => {
        const line = lines.find(l => l.replenishmentLineId === receipt.replenishmentLineId);
        return <tr key={String(receipt.linkId)}>
          <td>{display(receipt.documentNo)}</td><td>{display(line?.itemName)} ({display(line?.unitName)})</td>
          <td>{display(receipt.receiptStatus)}</td><td>{display(receipt.creditedBaseQty)}</td><td>{display(receipt.effectiveBaseQty)}</td>
          <td>{receipt.reversalDocumentId == null ? "Không có" : display(receipt.reversalDocumentId)}</td><td>{display(receipt.linkedAt)}</td>
        </tr>;
      })}</tbody>
    </table>}
    {record.rawStatus === "CLOSED" && <p>Đóng lúc {display(record.closedAt)} bởi người dùng {display(record.closedBy)}. Reason: {display(record.closeReason)}. Unreceived quantities will not be received under this replenishment.</p>}
  </section>;
}
