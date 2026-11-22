import { useState } from "react";
import { type Row, timestamp } from "./api";
import { useAuth } from "./auth";
import { Badge, Empty, Pager, State, useQuery } from "./ui";
const groups = [
  ["all", "Tất cả", []],
  ["unassigned", "Chưa phân công", ["PENDING_ASSIGNMENT"]],
  ["doing", "Đang dọn", ["ASSIGNED", "IN_PROGRESS", "ON_HOLD"]],
  ["inspect", "Chờ kiểm tra", ["PENDING_INSPECTION"]],
  ["rework", "Cần dọn lại", ["REWORK_REQUIRED"]],
] as const;
export default function CleaningQueue({
  revision,
  open,
}: {
  revision: number;
  open: (row: Row) => void;
}) {
  const { branch } = useAuth();
  const [queue, setQueue] = useState("all"),
    [page, setPage] = useState(1),
    [retry, bump] = useState(0);
  const query = useQuery(
    `work/cleaning-queue?branchId=${branch}&queue=${queue}&page=${page}`,
    revision + retry,
  );
  return (
    <section aria-label="Điều phối buồng phòng">
      <div className="tabs" role="group" aria-label="Lọc hàng đợi">
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
                <tr key={r.workId}>
                  <td>
                    <strong>Phòng {r.room}</strong>
                    <small>
                      {r.title} · hạn {timestamp(r.dueAt)}
                    </small>
                  </td>
                  <td>{r.assignee || "Chưa giao"}</td>
                  <td>
                    <Badge value={r.status} />
                  </td>
                  <td>
                    <button
                      className={
                        [
                          "PENDING_ASSIGNMENT",
                          "PENDING_INSPECTION",
                          "REWORK_REQUIRED",
                        ].includes(r.status)
                          ? "primary"
                          : ""
                      }
                      onClick={() => open(r)}
                    >
                      {r.status === "PENDING_ASSIGNMENT"
                        ? "Phân công"
                        : r.status === "PENDING_INSPECTION"
                          ? "Kiểm tra"
                          : r.status === "REWORK_REQUIRED"
                            ? "Xử lý dọn lại"
                            : "Phân công lại"}
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
    </section>
  );
}
