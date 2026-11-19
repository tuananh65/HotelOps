import { SearchableSelect } from "../components/SearchableSelect";
import { useState } from "react";
import { Link } from "react-router-dom";
import { isObject } from "../auth/http";
import type { Values } from "./contracts";

export function MaterialAvailabilityView({ data }: { data: Values }) {
  const warehouses = Array.isArray(data.warehouses) ? data.warehouses.filter(isObject) : [];
  const [selected, setSelected] = useState(String(data.warehouseId ?? ""));
  const warehouse = warehouses.find(w => String(w.warehouseId) === selected) ?? warehouses[0];
  const lines = warehouse && Array.isArray(warehouse.lines) ? warehouse.lines.filter(isObject) : [];
  return <section aria-label="Vật tư khả dụng theo kho">
    <h2>Vật tư khả dụng</h2>
    <p>Chỉ xem trước, không giữ tồn kho. Vật tư đang vận chuyển không thể cấp trước khi nhận.</p>
    <label>Kho
      <SearchableSelect value={String(warehouse?.warehouseId ?? "")} onChange={e => setSelected(e.target.value)} disabled={!warehouses.length}>
        {warehouses.map(w => <option key={String(w.warehouseId)} value={String(w.warehouseId)}>{String(w.warehouseName)}</option>)}
      </SearchableSelect>
    </label>
    {warehouse && String(warehouse.warehouseId) !== String(data.warehouseId) &&
      <p>Đây là kho để so sánh. Kho cấp phát của yêu cầu không đổi.</p>}
    {!warehouses.length ? <p>Chưa có tồn kho khả dụng trả về. Tải lại yêu cầu trước khi cấp.</p> :
      <table>
        <caption>Khả dụng cho {String(warehouse?.warehouseName)}</caption>
        <thead><tr><th scope="col">Vật tư</th><th scope="col">Số lượng đã duyệt còn lại</th><th scope="col">Tồn kho khả dụng</th>
          <th scope="col">Đang vận chuyển</th><th scope="col">Có thể cấp phát</th><th scope="col">Hướng dẫn</th></tr></thead>
        <tbody>{lines.map(line => {
          const shortage = typeof line.fulfillableQty === "number" && typeof line.remainingApprovedQty === "number"
            && line.fulfillableQty < line.remainingApprovedQty;
          return <tr key={String(line.requestLineId)}>
            <th scope="row">{String(line.itemName)}</th>
            <td>{line.remainingApprovedQty == null ? "Chưa có thông tin" : `${line.remainingApprovedQty} ${line.unitName}`}</td>
            <td>{String(line.availableBaseQty)} {String(line.baseUnitName)}</td>
            <td>{line.inTransitBaseQty == null ? "Chưa có thông tin" : `${line.inTransitBaseQty} ${line.baseUnitName}`}
              {line.transitUnavailableReason != null && <p>{String(line.transitUnavailableReason)}</p>}</td>
            <td>{line.fulfillableQty == null ? "Chưa có thông tin" : `${line.fulfillableQty} ${line.unitName}`}</td>
            <td>{line.unavailableReason != null && <p>{String(line.unavailableReason)}</p>}
              {shortage && <p><strong>Không thể cấp đủ toàn bộ.</strong> Cân nhắc cấp một phần hoặc yêu cầu bổ sung.</p>}
              {line.sharedItemStock === true && <p>Tồn kho dùng chung với các dòng khác của vật tư này. Không cộng các số xem trước riêng lẻ; kiểm tra cấp phát dùng tổng số lượng.</p>}</td>
          </tr>;
        })}</tbody>
      </table>}
    <p>Số lượng có thể cấp dùng quy đổi hiện tại. Khi ghi sổ, máy chủ kiểm tra lại tồn kho, quyền và phiên bản yêu cầu.</p>
    {data.requestId != null && <Link to={`/operations/material-requests/${encodeURIComponent(String(data.requestId))}`}>Quay lại yêu cầu để cấp phát</Link>}
  </section>;
}
