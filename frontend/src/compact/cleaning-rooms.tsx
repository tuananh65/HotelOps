import { useEffect, useState } from "react";
import { type Row } from "./api";
import { useAuth } from "./auth";
import { Badge, Empty, Pager, State, useQuery } from "./ui";
import "./cleaning-rooms.css";

export default function CleaningRooms({
  revision,
  create,
  search,
  roomId,
}: {
  revision: number;
  create: (room: Row) => void;
  search?: string;
  roomId?: number;
}) {
  const { branch } = useAuth();
  const [localSearch, setSearch] = useState("");
  const term = search ?? localSearch;
  const [page, setPage] = useState(1);
  const [retry, bump] = useState(0);
  useEffect(() => setPage(1), [term, branch]);
  const query = useQuery(
    `work/cleaning-rooms?branchId=${branch}&page=${page}&search=${encodeURIComponent(term)}${roomId ? `&roomId=${roomId}` : ""}`,
    revision + retry,
  );
  useEffect(() => {
    const selected = query.data?.items?.find(
      (room: Row) => room.roomId === roomId,
    );
    if (roomId && selected && !query.loading) create(selected);
  }, [roomId, query.data, query.loading, create]);
  return (
    <section aria-label="Phòng cần tạo việc dọn">
      {search === undefined && (
        <label className="search">
          Tìm phòng cần dọn
          <input
            value={localSearch}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Số phòng…"
          />
        </label>
      )}
      <p className="hint">
        Phòng cần dọn, chưa có công việc đang mở. Chọn phòng để tạo yêu cầu và
        phân công nhân viên.
      </p>
      <State {...query} retry={() => bump((n) => n + 1)}>
        <div className="table-scroll">
          <table className="cleaning-room-table">
            <thead>
              <tr>
                <th>Phòng</th>
                <th>Hạng phòng</th>
                <th>Trạng thái</th>
                <th>Thao tác</th>
              </tr>
            </thead>
            <tbody>
              {query.data?.items?.map((room: Row) => (
                <tr key={room.roomId}>
                  <td>
                    <strong>Phòng {room.code}</strong>
                    <small>{room.floor}</small>
                  </td>
                  <td>
                    {room.roomType}
                    <small>{room.capacity} người</small>
                  </td>
                  <td>
                    <Badge value={room.readiness} />
                  </td>
                  <td>
                    <button
                      type="button"
                      className="primary"
                      onClick={() => create(room)}
                    >
                      Tạo yêu cầu
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        {!query.data?.items?.length && (
          <Empty>
            {term
              ? "Không có phòng cần dọn khớp tìm kiếm."
              : "Không có phòng cần tạo việc. Phòng đã có yêu cầu nằm ở các nhóm công việc."}
          </Empty>
        )}
        <Pager
          page={page}
          pageSize={25}
          total={query.data?.total || 0}
          onPage={setPage}
        />
      </State>
    </section>
  );
}
