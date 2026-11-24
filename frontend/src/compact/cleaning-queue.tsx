import { useEffect, useState, type ReactNode } from "react";
import { type Row, timestamp } from "./api";
import { useAuth } from "./auth";
import { Badge, Empty, Pager, State, useQuery } from "./ui";
const groups = [
  ["all", "Tất cả", []],
  ["rooms", "Phòng cần dọn", ["PENDING_CLEANING"]],
  ["assigned", "Đã giao", ["ASSIGNED"]],
  ["doing", "Đang dọn", ["IN_PROGRESS", "ON_HOLD"]],
  ["inspect", "Chờ kiểm tra", ["PENDING_INSPECTION", "PENDING_INSPECTION_SC", "PENDING_INSPECTION_DL"]],
  ["incidents", "Kiểm tra phòng gặp sự cố", ["INCIDENT_INSPECTION"]],
  ["rework", "Cần dọn lại", ["REWORK_REQUIRED"]],
] as const;
export default function CleaningQueue({
  revision,
  open,
  create,
  search,
  sort = "due",
  onSort,
  onSearch,
  actions,
}: {
  revision: number;
  open: (row: Row) => void;
  create: (room: Row) => void;
  search: string;
  sort?: string;
  onSort?: (value: string) => void;
  onSearch?: (value: string) => void;
  actions?: ReactNode;
}) {
  const { branch, can } = useAuth();
  const [queue, setQueue] = useState("all"),
    [page, setPage] = useState(1),
    [retry, bump] = useState(0);
  const query = useQuery(
    `work/cleaning-queue?branchId=${branch}&queue=${queue}&page=${page}&search=${encodeURIComponent(search)}&sort=${sort}`,
    revision + retry,
  );
  useEffect(() => setPage(1), [search, sort, branch]);
  return (
    <section aria-label="Điều phối buồng phòng">
      <div className="tabs housekeeping-tabs" role="group" aria-label="Lọc hàng đợi">
        {groups.map(([key, name, statuses]) => (
          <button
            key={key}
            aria-pressed={queue === key}
            onClick={() => {
              setQueue(key);
              setPage(1);
            }}
          >
            {name}{" "}
            <b>
              {query.data?.counts
                ?.filter(
                  (r: Row) =>
                    key === "all" ||
                    (statuses as readonly string[]).includes(r.status),
                )
                .reduce((sum: number, r: Row) => sum + r.count, 0) ?? "—"}
            </b>
          </button>
        ))}
      </div>
      <div className="housekeeping-filter-panel">
      <label>Tìm phòng / công việc<input type="search" value={search} placeholder="Số phòng hoặc tên công việc…" onChange={e=>onSearch?.(e.target.value)}/></label>
      {onSort && (
        <label>
          Sắp xếp công việc
          <select value={sort} onChange={(e) => onSort(e.target.value)}>
            <option value="latest">Mới nhất</option>
            <option value="due">Hạn gần nhất</option>
            <option value="title">Tên công việc</option>
          </select>
        </label>
      )}
      {actions}
      </div>
      {(
        <State {...query} retry={() => bump((n) => n + 1)}>
          <div className="table-scroll">
            <table>
              <thead>
                <tr>
                  <th>Phòng</th>
                  <th>Người dọn</th>
                  <th>Trạng thái</th>
                  <th>Thao tác</th>
                </tr>
              </thead>
              <tbody>
                {query.data?.items?.map((r: Row) => (
                  <tr key={r.isRoom?`room-${r.roomId}`:`work-${r.workId}`}>
                    <td>
                      <strong>Phòng {r.room}</strong>
                      <small>
                        {r.isRoom?`${r.roomType} · ${r.capacity} người`: `${r.title} · hạn ${timestamp(r.dueAt)}`}
                      </small>
                    </td>
                    <td>{r.assignee || "Chưa giao"}</td>
                    <td>
                      <Badge value={r.inspectionVariant?`Chờ kiểm tra - ${r.inspectionVariant.toLowerCase()}`:r.hasRoomIssue&&["PENDING_INSPECTION","REWORK_REQUIRED"].includes(r.status)?"Kiểm tra phòng gặp sự cố":r.status} />
                    </td>
                    <td>
                      <button
                        className={
                          [
                            "PENDING_CLEANING",
                            "PENDING_ASSIGNMENT",
                            "PENDING_INSPECTION",
                            "REWORK_REQUIRED",
                          ].includes(r.status)
                            ? "primary"
                            : ""
                        }
                        disabled={r.isRoom&&!can("CLEANING_ASSIGN")}
                        onClick={() => r.isRoom?create({...r,code:r.room}):open(r)}
                      >
                        {r.isRoom ? "Tạo yêu cầu" : r.inspectionVariant ? "Kiểm tra" : r.status === "PENDING_ASSIGNMENT"
                          ? "Phân công"
                          : r.status === "PENDING_INSPECTION"
                            ? "Kiểm tra"
                            : r.status === "REWORK_REQUIRED"
                              ? "Xử lý dọn lại"
                              : r.status === "ASSIGNED" ? "Phân công lại" : "Xem chi tiết"}
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {!query.data?.items?.length && (
            <Empty>Hàng đợi trống. Không có phòng ở nhóm này.</Empty>
          )}
          <Pager page={page} total={query.data?.total || 0} onPage={setPage} />
        </State>
      )}
    </section>
  );
}
