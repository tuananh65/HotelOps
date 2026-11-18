import { Link } from "react-router-dom";
import type { ReadTypes } from "./contracts";

export function RoomTypeDirectoryResults({ data }: { data: ReadTypes["roomTypes"] }) {
  return <section aria-label="Kết quả danh mục loại phòng">
    <p aria-label="Bộ lọc loại phòng đã áp dụng">
      Chi nhánh: {data.branchId ?? "Tất cả chi nhánh được phép truy cập"}; Trạng thái: {data.isActive === null ? "Tất cả" : data.isActive ? "Đang hoạt động" : "Ngừng hoạt động"}; Tìm kiếm: {data.search || "Không có"}; Sắp xếp: {data.sort} ({data.descending ? "descending" : "ascending"}).
    </p>
    <p>Trang {data.page}; {data.pageSize} mỗi trang; {data.total} loại phòng phù hợp.</p>
    {data.items.length === 0 ? <p>MSG10: Không có loại phòng phù hợp bộ lọc trên trang này.</p> :
      <table>
        <caption>Loại phòng từ máy chủ</caption>
        <thead><tr>{["Mã", "Tên", "Chi nhánh", "Phòng đang hoạt động", "Trạng thái", "Chi tiết"].map(label => <th scope="col" key={label}>{label}</th>)}</tr></thead>
        <tbody>{data.items.map(({ details: t, activeRooms }) => <tr key={`${t.branchId}:${t.id}`}>
          <th scope="row">{t.code}</th><td>{t.name}</td><td>{t.branchId ?? "Toàn hệ thống"}</td>
          <td>{activeRooms}</td><td>{t.isActive ? "Đang hoạt động" : "Ngừng hoạt động"}</td>
          <td>{t.branchId !== null ? <Link to={`/structure/room-types/${t.id}?branchId=${t.branchId}`}>Đang mở {t.name} ({t.code})</Link> :
            <details><summary>Xem loại phòng dùng chung</summary>
              <p>{t.description ?? "Chưa có mô tả"}</p>
              <p>Thời gian vệ sinh tiêu chuẩn (phút): {t.standardCleaningMinutes ?? "Chưa khả dụng"}</p>
              <p>Sức chứa tiêu chuẩn: {t.standardCapacity ?? "Chưa khả dụng"}</p>
              <p>Danh sách kiểm tra mặc định: {t.defaultChecklistId ?? "Không có"}</p>
            </details>}</td>
        </tr>)}</tbody>
      </table>}
  </section>;
}
