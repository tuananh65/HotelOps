import PageTitle from "./page-title";
import { useState } from "react";
import { action, api, fields, message, type Row } from "./api";
import { useAuth } from "./auth";
import { Badge, Dialog, Empty, History, Pager, State, useQuery } from "./ui";
import { useCatalogRows } from "./use-catalog-rows";
import { SlidersHorizontal } from "lucide-react";
export default function Rooms({ readScope }: { readScope?: number }) {
  const { branch, can, profile } = useAuth();
  const [revision, bump] = useState(0),
    [selected, setSelected] = useState<number | null>(
      readScope !== undefined
        ? Number(
            new URLSearchParams(location.hash.split("?")[1]).get("room"),
          ) || null
        : null,
    ),
    [search, setSearch] = useState(""),
    [group, setGroup] = useState("ALL"),
    [page, setPage] = useState(1),
    [command, setCommand] = useState(""),
    [error, setError] = useState(""),
    [pending, setPending] = useState(false),
    [notice, setNotice] = useState("");
  const [filters, setFilters] = useState(false),
    [floor, setFloor] = useState("");
  const query = useQuery<Row[]>(
      readScope !== undefined
        ? `insights/owner/rooms?${readScope ? `branchId=${readScope}` : ""}`
        : `rooms?branchId=${branch}`,
      revision,
    ),
    room = query.data?.find((r) => r.roomId === selected),
    history = useQuery(selected ? `rooms/${selected}/history` : null, revision);
  const roomTypes = useCatalogRows(
    readScope === undefined ? "room-types" : null,
    branch,
    revision,
  );
  const roomType = (r: Row) =>
    r.roomTypeName
      ? { name: r.roomTypeName, capacity: r.capacity }
      : roomTypes.data?.find((t) => t.roomTypeId === r.roomTypeId);
  const links = useQuery(
    selected && readScope !== undefined && can("INCIDENT_VIEW")
      ? `insights/owner/room-links/${selected}`
      : null,
  );
  const roomInfo = (r: Row) => {
    const type = roomType(r);
    return (
      <div className="room-facts">
        <span>
          Hạng phòng: <strong>{type?.name || "Chưa có thông tin"}</strong>
        </span>
        <span>
          Sức chứa:{" "}
          <strong>
            {type?.capacity != null
              ? `${type.capacity} người`
              : "Chưa có thông tin"}
          </strong>
        </span>
      </div>
    );
  };
  const status = (r: Row) => (r.isOccupied ? "OCCUPIED" : r.readiness);
  const reception = profile.roles.some(
    (r) => r.code === "RECEPTION" && r.branchId === branch,
  );
  const staff =
    profile.roles.length > 0 &&
    profile.roles
      .filter((r) => r.branchId === null || r.branchId === branch)
      .every((r) => r.code === "HK_STAFF");
  const floors = useCatalogRows(
    staff && selected ? "locations" : null,
    branch,
    revision,
  );
  const category = (r: Row) =>
    r.isOccupied
      ? "OCCUPIED"
      : r.readiness === "READY"
        ? "READY"
        : r.readiness === "BLOCKED"
          ? "BLOCKED"
          : "TO_SERVICE";
  const matchesGroup = (r: Row, value: string) =>
    value === "ALL" ||
    (value === "BLOCKED"
      ? r.isBlocked || r.readiness === "BLOCKED"
      : category(r) === value);
  const filteredRooms =
    query.data?.filter(
      (r) =>
        r.code.toLowerCase().includes(search.toLowerCase()) &&
        (!floor || r.locationId === Number(floor)) &&
        matchesGroup(r, group),
    ) || [];
  const pageSize = 15;
  const pages = Math.max(1, Math.ceil(filteredRooms.length / pageSize));
  const currentPage = Math.min(page, pages);
  const pageRooms = filteredRooms.slice(
    (currentPage - 1) * pageSize,
    currentPage * pageSize,
  );
  async function submit(form: HTMLFormElement) {
    if (!room || pending) return;
    setPending(true);
    setError("");
    const f = new FormData(form);
    const target = query.data?.find(
      (r) => r.roomId === Number(f.get("target")),
    );
    try {
      await api(
        `rooms/${room.roomId}/actions/${command}`,
        action(room.rowVersion, {
          reason: f.get("reason") || null,
          targetRoomId: target?.roomId,
          targetRowVersion: target?.rowVersion,
        }),
      );
      setCommand("");
      setNotice("Đã cập nhật phòng.");
      bump((x) => x + 1);
    } catch (e) {
      setError(message(e));
      bump((x) => x + 1);
    } finally {
      setPending(false);
    }
  }
  return (
    <>
      <div className="page-heading">
        <PageTitle>
          <h1>Tình trạng phòng</h1>
          <p>Theo dõi khách lưu trú và mức sẵn sàng của phòng</p>
        </PageTitle>
      </div>
      <div className="toolbar filters room-board-filters">
        <label className="search">
          Tìm phòng
          <input
            placeholder="Số phòng…"
            value={search}
            onChange={(e) => {
              setSearch(e.target.value);
              setPage(1);
            }}
          />
        </label>
        {readScope !== undefined && (
          <button aria-expanded={filters} onClick={() => setFilters(!filters)}>
            <SlidersHorizontal size={16} /> Bộ lọc
          </button>
        )}
        <div className="tabs" aria-label="Lọc phòng">
          {[
            ["ALL", "Tất cả"],
            ["READY", "Sẵn sàng"],
            ["OCCUPIED", "Có khách"],
            ["TO_SERVICE", "Cần phục vụ"],
            ["BLOCKED", "Đang chặn"],
          ].map(([v, n]) => (
            <button
              key={v}
              aria-pressed={group === v}
              onClick={() => {
                setGroup(v);
                setPage(1);
              }}
            >
              {n}{" "}
              <span>
                {query.data?.filter((r) => matchesGroup(r, v)).length || 0}
              </span>
            </button>
          ))}
        </div>
      </div>
      {readScope !== undefined && filters && (
        <div className="toolbar filters">
          <label>
            Tầng / khu vực
            <select
              value={floor}
              onChange={(e) => {
                setFloor(e.target.value);
                setPage(1);
              }}
            >
              <option value="">Tất cả tầng</option>
              {Array.from(
                new Map(
                  query.data
                    ?.filter((r) => r.locationId)
                    .map((r) => [
                      r.locationId,
                      `${r.branchName} · ${r.floor || r.locationId}`,
                    ]),
                ).entries(),
              ).map(([id, name]) => (
                <option key={id} value={id}>
                  {name}
                </option>
              ))}
            </select>
          </label>
          <button
            onClick={() => {
              setFloor("");
              setSearch("");
              setGroup("ALL");
              setPage(1);
            }}
          >
            Đặt lại
          </button>
        </div>
      )}
      {notice && (
        <p className="notice" role="status">
          {notice}
        </p>
      )}
      {roomTypes.error && (
        <p className="alert" role="alert">
          Chưa tải được hạng phòng và sức chứa.{" "}
          <button onClick={() => bump((x) => x + 1)}>Thử lại</button>
        </p>
      )}
      <State {...query} retry={() => bump((x) => x + 1)}>
        <div className="room-grid room-board-grid">
          {pageRooms.map((r) => (
            <button
              key={r.roomId}
              className={`room-tile ${category(r).toLowerCase()}`}
              onClick={() => {
                setSelected(r.roomId);
                setCommand("");
                setError("");
              }}
            >
              <span>Phòng</span>
              <strong>{r.code}</strong>
              {readScope !== undefined && (
                <small>
                  {r.branchName} · {r.floor}
                </small>
              )}
              <Badge value={status(r)} />
              {r.isOccupied && r.readiness !== "READY" && (
                <Badge value={r.readiness} />
              )}
              {roomInfo(r)}
            </button>
          ))}
        </div>
        {!filteredRooms.length && (
          <Empty>
            <p>Không tìm thấy phòng phù hợp.</p>
            {(search || group !== "ALL") && (
              <button
                onClick={() => {
                  setSearch("");
                  setGroup("ALL");
                  setPage(1);
                }}
              >
                Xóa bộ lọc
              </button>
            )}
          </Empty>
        )}
        <nav className="room-pagination" aria-label="Phân trang phòng">
          <span aria-live="polite">
            {filteredRooms.length ? (currentPage - 1) * pageSize + 1 : 0}–
            {Math.min(currentPage * pageSize, filteredRooms.length)} /{" "}
            {filteredRooms.length} phòng · Trang {currentPage} / {pages}
          </span>
          <Pager
            page={currentPage}
            total={filteredRooms.length}
            pageSize={pageSize}
            onPage={setPage}
          />
        </nav>
      </State>
      {selected && room && (
        <Dialog
          className="room-drawer"
          title={`Phòng ${room.code}`}
          close={() => {
            if (!pending) setSelected(null);
          }}
        >
          <div className="toolbar">
            <Badge value={status(room)} />
            {room.isOccupied && room.readiness !== "READY" && (
              <Badge value={room.readiness} />
            )}
          </div>
          {roomInfo(room)}
          {readScope !== undefined && (
            <>
              <p>
                {room.branchName} · {room.floor}
              </p>
              <p>
                Công việc vệ sinh:{" "}
                <Badge
                  value={room.cleaningStatus || "Chưa có công việc hiện tại"}
                />
              </p>
              <p>
                Kiểm tra:{" "}
                <Badge value={room.inspectionStatus || "Chưa có kết quả"} />
              </p>
              <p>
                Mức sẵn sàng: <Badge value={room.readiness} /> ·{" "}
                {room.isBlocked ? "Đang chặn" : "Không bị chặn"}
              </p>
              <h3>Sự cố liên quan</h3>
              <State {...links}>
                {links.data?.map((w: Row) => (
                  <a
                    className="overview-task"
                    key={w.workId}
                    href={`#work?kind=INCIDENT&id=${w.workId}`}
                  >
                    {w.title} · <Badge value={w.status} />
                  </a>
                ))}
                {!links.data?.length && <Empty />}
              </State>
            </>
          )}
          {staff && (
            <State {...floors} retry={() => bump((n) => n + 1)}>
              <p>
                Tầng / khu vực:{" "}
                {floors.data?.find(
                  (floor) => floor.locationId === room.locationId,
                )?.name || "Chưa có thông tin"}
              </p>
            </State>
          )}
          <div className="toolbar">
            {staff && can("CLEANING_VIEW") && (
              <a
                className="button-link"
                href={`#work?kind=CLEANING&search=${encodeURIComponent(room.code)}`}
              >
                Việc dọn phòng & checklist của tôi
              </a>
            )}
            {reception &&
              !room.isOccupied &&
              room.readiness === "READY" &&
              can("ROOM_CHECKIN") && (
                <button
                  className="primary"
                  onClick={() => setCommand("check-in")}
                >
                  Nhận phòng
                </button>
              )}
            {reception && room.isOccupied && can("ROOM_CHECKOUT") && (
              <button
                className="primary"
                onClick={() => setCommand("check-out")}
              >
                Trả phòng
              </button>
            )}
            {reception && room.isOccupied && can("ROOM_OCCUPANCY_MOVE") && (
              <button onClick={() => setCommand("move")}>Chuyển phòng</button>
            )}
            {can("ROOM_BLOCK_MANAGE") && (
              <button
                disabled={history.loading || !!history.error || pending}
                onClick={() =>
                  setCommand(
                    fields(history.data?.blocks).some((b) => !b.releasedAt)
                      ? "unblock"
                      : "block",
                  )
                }
              >
                {fields(history.data?.blocks).some((b) => !b.releasedAt)
                  ? "Gỡ chặn phòng"
                  : "Chặn phòng"}
              </button>
            )}
            {can("CLEANING_ASSIGN") &&
              !room.isOccupied &&
              ["DIRTY", "PENDING_CLEANING"].includes(room.readiness) && (
                <a
                  className="button-link"
                  href={`#work?kind=CLEANING&room=${room.roomId}&create=1`}
                >
                  Tạo việc dọn phòng
                </a>
              )}
            {can("INCIDENT_CREATE") && (
              <a
                className="button-link"
                href={`#work?kind=INCIDENT&room=${room.roomId}&create=1`}
              >
                Báo sự cố
              </a>
            )}
          </div>
          {command && (
            <form
              onSubmit={(e) => {
                e.preventDefault();
                submit(e.currentTarget);
              }}
            >
              <h3>
                {command === "check-in"
                  ? "Xác nhận nhận phòng"
                  : command === "check-out"
                    ? "Xác nhận trả phòng"
                    : command === "block"
                      ? "Xác nhận chặn phòng"
                      : command === "unblock"
                        ? "Xác nhận gỡ chặn"
                        : "Chọn phòng chuyển đến"}
              </h3>
              {command === "move" && (
                <label>
                  Phòng sẵn sàng
                  <select name="target" required defaultValue="">
                    <option value="" disabled>
                      Chọn phòng
                    </option>
                    {query.data
                      ?.filter(
                        (r) =>
                          !r.isOccupied &&
                          r.readiness === "READY" &&
                          r.roomId !== room.roomId,
                      )
                      .map((r) => (
                        <option key={r.roomId} value={r.roomId}>
                          {r.code}
                        </option>
                      ))}
                  </select>
                </label>
              )}
              <label>
                Ghi chú / lý do
                <textarea
                  name="reason"
                  minLength={10}
                  maxLength={2000}
                  required={["move", "block", "unblock"].includes(command)}
                />
              </label>
              <p className="hint">
                {command === "check-out"
                  ? "Phòng sẽ chuyển sang cần dọn. Không thể nhận khách mới trước khi hoàn tất kiểm tra."
                  : "Thao tác sẽ được ghi vào lịch sử phòng."}
              </p>
              <button className="primary" disabled={pending}>
                Xác nhận
              </button>
            </form>
          )}
          {error && (
            <p className="alert" role="alert">
              {error}
            </p>
          )}
          <h3>Lịch sử phòng</h3>
          <State {...history}>
            <History rows={fields(history.data?.events)} />
          </State>
        </Dialog>
      )}
    </>
  );
}
