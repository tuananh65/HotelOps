import { useEffect, useState, type FormEvent } from "react";
import { Link, useSearchParams } from "react-router-dom";
import {
  roomStatusCodes,
  type RoomDirectoryResponse,
} from "../../api/dev1/rooms";
import {
  listRoomBoard,
  readRoomDetail,
  readRoomHistory,
  roomPage,
  roomBoardError,
  type RoomDetail,
  type RoomHistory,
} from "../../api/dev1/roomBoard";

type Loaded = {
  key: string;
  board?: RoomDirectoryResponse;
  detail?: RoomDetail;
  history?: RoomHistory;
};
export default function RoomStatusBoardScreen() {
  const [params, setParams] = useSearchParams();
  const [retry, setRetry] = useState(0);
  const query = params.toString(),
    key = `${query}|${retry}`;
  const branch = params.get("branchId") || "",
    room = params.get("roomId") || "";
  const [loaded, setLoaded] = useState<Loaded | null>(null);
  const [failure, setFailure] = useState<{
    key: string;
    message: string;
  } | null>(null);
  useEffect(() => {
    const abort = new AbortController();
    if (!branch) return () => abort.abort();
    async function load() {
      const p = new URLSearchParams(query);
      try {
        let next: Loaded;
        if (room) {
          const page = roomPage(p.get("historyPage"));
          const [detail, history] = await Promise.all([
            readRoomDetail(branch, room, abort.signal),
            readRoomHistory(branch, room, page, abort.signal),
          ]);
          next = { key, detail, history };
        } else next = { key, board: await listRoomBoard(p, abort.signal) };
        if (!abort.signal.aborted) setLoaded(next);
      } catch (error) {
        if (!abort.signal.aborted)
          setFailure({ key, message: roomBoardError(error) });
      }
    }
    void load();
    return () => abort.abort();
  }, [branch, room, query, key]);
  const data = loaded?.key === key ? loaded : null;
  const error = failure?.key === key ? failure.message : "";
  const board = data?.board,
    detail = data?.detail,
    history = data?.history;
  function submit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const next = new URLSearchParams();
    new FormData(e.currentTarget).forEach((v, k) => {
      if (String(v).trim()) next.set(k, String(v).trim());
    });
    next.set("page", "1");
    setParams(next);
    setRetry((v) => v + 1);
  }
  function href(id?: number) {
    const next = new URLSearchParams(params);
    next.delete("roomId");
    next.delete("historyPage");
    if (id) next.set("roomId", String(id));
    return `/rooms/status?${next}`;
  }
  function paginate(name: string, page: number) {
    const next = new URLSearchParams(params);
    next.set(name, String(page));
    setParams(next);
  }
  const floors = board
    ? [...new Map(board.items.map((r) => [r.floor.id, r.floor])).values()]
    : [];
  return (
    <>
      <div className="page-header">
        <div>
          <span className="eyebrow">FE-03</span>
          <h1>Trạng thái vận hành phòng</h1>
          <p>Trạng thái phòng đã ghi nhận từ máy chủ.</p>
        </div>
      </div>
      <form key={query} className="filter-bar" onSubmit={submit}>
        {[
          ["branchId", "Chi nhánh (ID)"],
          ["floorId", "Tầng (ID)"],
          ["areaId", "Khu vực (ID)"],
          ["roomTypeId", "Loại phòng (ID)"],
        ].map(([name, label]) => (
          <label key={name}>
            {label}
            <input
              name={name}
              inputMode="numeric"
              required={name === "branchId"}
              pattern="[1-9][0-9]*"
              defaultValue={params.get(name) || ""}
            />
          </label>
        ))}
        <label>
          Trạng thái vận hành
          <select name="status" defaultValue={params.get("status") || ""}>
            <option value="">Tất cả</option>
            {roomStatusCodes.map((s) => (
              <option key={s}>{s}</option>
            ))}
          </select>
        </label>
        <label>
          Số phòng
          <input
            name="search"
            maxLength={200}
            defaultValue={params.get("search") || ""}
          />
        </label>
        <button>Lọc phòng</button>
      </form>
      <p role="note">
        Effective Readiness: Chưa có nguồn được xác nhận. Trạng thái dọn phòng
        và kết quả kiểm tra được theo dõi riêng.
      </p>
      {!branch && <p>Chọn chi nhánh để tải các phòng được phép xem.</p>}
      {branch && !data && !error && <p role="status">Đang tải phòng…</p>}
      {error && (
        <p role="alert">
          {error}{" "}
          <button onClick={() => setRetry((v) => v + 1)}>Thử lại</button>
        </p>
      )}
      {board && (
        <>
          <h2>Bộ đếm theo bộ lọc</h2>
          <p>
            {board.totalCount} phòng phù hợp · Trang {board.page}
          </p>
          <ul aria-label="Bộ đếm trạng thái">
            {board.statusCounts?.map((c) => (
              <li key={c.operationalStatusCode}>
                {c.operationalStatusCode}: {c.count}
              </li>
            ))}
          </ul>
          {!board.items.length && <p>Không có phòng phù hợp trên trang này.</p>}
          {floors.map((f) => (
            <section key={f.id} aria-label={`Tầng ${f.name}`}>
              <h2>
                {f.name} ({f.code})
              </h2>
              <div className="table-wrap">
                <table>
                  <thead>
                    <tr>
                      <th>Phòng</th>
                      <th>Trạng thái vận hành</th>
                      <th>Hoạt động</th>
                      <th>Khu vực</th>
                      <th>Loại phòng</th>
                    </tr>
                  </thead>
                  <tbody>
                    {board.items
                      .filter((r) => r.floor.id === f.id)
                      .map((r) => (
                        <tr key={r.id}>
                          <td>
                            <Link to={href(r.id)}>{r.roomNumber}</Link>
                          </td>
                          <td>{r.operationalStatusCode}</td>
                          <td>{r.isActive ? "Có" : "Không"}</td>
                          <td>{r.area?.name || "—"}</td>
                          <td>{r.roomType.name}</td>
                        </tr>
                      ))}
                  </tbody>
                </table>
              </div>
            </section>
          ))}
          <button
            disabled={board.page <= 1}
            onClick={() => paginate("page", board.page - 1)}
          >
            Trang trước
          </button>{" "}
          <button
            disabled={board.page * board.pageSize >= board.totalCount}
            onClick={() => paginate("page", board.page + 1)}
          >
            Trang sau
          </button>
        </>
      )}
      {detail && (
        <section aria-label="Chi tiết trạng thái phòng">
          <Link to={href()}>Quay lại danh sách phòng</Link>
          <h2>Phòng {detail.room.roomNumber}</h2>
          <p>
            Chi nhánh {detail.room.branchId} · {detail.room.floor.name} ·{" "}
            {detail.room.roomType.name}
          </p>
          <p>
            Trạng thái vận hành: {detail.room.operationalStatusCode} · Hoạt
            động: {detail.room.isActive ? "Có" : "Không"}
          </p>
          <p>{detail.description}</p>
          <h3>Bằng chứng trạng thái hiện tại</h3>
          <p>
            {detail.currentStatusEvidence.availability} ·{" "}
            {detail.currentStatusEvidence.reason || "Chưa ghi nhận lý do"} ·{" "}
            {detail.currentStatusEvidence.recordedAtUtc || "—"}
          </p>
          {detail.currentStatusEvidence.source && (
            <p>
              Nguồn: {detail.currentStatusEvidence.source.type || "—"}{" "}
              {detail.currentStatusEvidence.source.id ?? ""} ·{" "}
              {detail.currentStatusEvidence.source.availability}
            </p>
          )}
          <h3>RoomBlock chưa release</h3>
          <p>
            Các bản ghi chưa release; thời điểm dự kiến không xác nhận phòng đã
            được mở lại.
          </p>
          {!detail.unreleasedBlocks.length && (
            <p>Không có RoomBlock chưa release được ghi nhận.</p>
          )}
          <ul>
            {detail.unreleasedBlocks.map((b) => (
              <li key={b.id}>
                {b.reason} · Bắt đầu UTC: {b.blockedAtUtc} · Dự kiến UTC:{" "}
                {b.expectedReleaseAtUtc || "—"}
              </li>
            ))}
          </ul>
        </section>
      )}
      {history && (
        <section aria-label="Lịch sử trạng thái">
          <h2>Lịch sử trạng thái đã ghi nhận</h2>
          {!history.items.length && <p>Chưa có sự kiện trên trang này.</p>}
          <ol>
            {history.items.map((e) => (
              <li key={e.id}>
                <time>{e.changedAtUtc}</time> · {e.fromStatusCode || "—"} →{" "}
                {e.toStatusCode} · {e.reason || "—"} · Nguồn{" "}
                {e.source.type || "—"} {e.source.id ?? ""} (
                {e.source.availability})
              </li>
            ))}
          </ol>
          <p>
            Trang lịch sử {history.page} · {history.totalCount} sự kiện
          </p>
          <button
            disabled={history.page <= 1}
            onClick={() => paginate("historyPage", history.page - 1)}
          >
            Lịch sử trước
          </button>{" "}
          <button
            disabled={history.page * history.pageSize >= history.totalCount}
            onClick={() => paginate("historyPage", history.page + 1)}
          >
            Lịch sử sau
          </button>
        </section>
      )}
    </>
  );
}
