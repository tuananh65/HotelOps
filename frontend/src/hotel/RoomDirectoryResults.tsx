import { displayCode } from "../i18n/vi";
import { Link } from "react-router-dom";
import type { ReadTypes } from "./contracts";

/** Read navigation only; the target API independently checks access. */
export function RoomDirectoryResults({ data, basePath = "/rooms" }: { data: ReadTypes["rooms"]; basePath?: string }) {
  return (
    <section aria-label="Kết quả danh mục phòng">
      <p>
        Trang {data.page}. {data.totalCount} phòng do máy chủ trả về.
      </p>
      {data.items.length === 0 ? (
        <p>MSG10: Không có phòng phù hợp bộ lọc trên trang này.</p>
      ) : (
        <table>
          <caption>Phòng từ máy chủ</caption>
          <thead>
            <tr>
              <th scope="col">Phòng</th>
              <th scope="col">Chi nhánh</th>
              <th scope="col">Tầng</th>
              <th scope="col">Khu vực</th>
              <th scope="col">Loại phòng</th>
              <th scope="col">Trạng thái đã ghi nhận</th>
              <th scope="col">Mức sẵn sàng thực tế</th>
              <th scope="col">Đang hoạt động</th>
              <th scope="col">Thao tác xem</th>
            </tr>
          </thead>
          <tbody>
            {data.items.map((room) => (
              <tr key={`${room.branchId}:${room.id}`}>
                <th scope="row">{room.roomNumber}</th>
                <td>{room.branchId}</td>
                <td>{room.floor.name}</td>
                <td>{room.area?.name ?? "Chưa có khu vực"}</td>
                <td>{room.roomType.name}</td>
                <td>{room.operationalStatusCode}</td>
                <td>{displayCode(room.effectiveReadiness)}</td>
                <td>{room.isActive ? "Có" : "Không"}</td>
                <td>
                  <Link
                    to={`${basePath}/${room.id}?branchId=${room.branchId}`}
                    aria-label={`Xem phòng ${room.roomNumber} trong chi nhánh ${room.branchId}`}
                  >
                    Xem chi tiết
                  </Link>{" "}
                  <Link
                    to={`${basePath}/${room.id}?branchId=${room.branchId}&view=history`}
                    aria-label={`Xem lịch sử phòng ${room.roomNumber} trong chi nhánh ${room.branchId}`}
                  >
                    Lịch sử trạng thái đã ghi nhận
                  </Link>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
      <p>
        Quyền xem không cho phép cập nhật phòng. Lịch sử trạng thái chỉ chứa sự kiện đã ghi nhận.
      </p>
    </section>
  );
}
