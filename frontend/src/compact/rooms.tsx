import { AlertPopup } from "./alert-popup";
import PageTitle from "./page-title";
import { useEffect, useState } from "react";
import { action, api, fields, message, type Row } from "./api";
import { useAuth } from "./auth";
import {
  Badge,
  Dialog,
  Empty,
  History,
  Pager,
  State,
  useQuery,
  label,
} from "./ui";
import { useCatalogRows } from "./use-catalog-rows";
import { SlidersHorizontal } from "lucide-react";
import "./room-actions.css";
export default function Rooms({
  readScope,
  housekeeping = false,
}: {
  readScope?: number;
  housekeeping?: boolean;
}) {
  const { branch, can, profile } = useAuth();
  const [revision, bump] = useState(0),
    [selected, setSelected] = useState<number | null>(
      readScope !== undefined || housekeeping
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
  useEffect(() => {
    if (command || pending) return;
    const timer = window.setInterval(() => bump(x => x + 1), 30000);
    return () => window.clearInterval(timer);
  }, [command, pending]);
  const [table, setTable] = useState(false),
    [cleanliness, setCleanliness] = useState(""),
    [occupancy, setOccupancy] = useState(""),
    [blocked, setBlocked] = useState("");
  const workflow = useQuery(
    selected && housekeeping ? `rooms/${selected}/workflow` : null,
    revision,
  );
  const query = useQuery<Row[]>(
      readScope !== undefined
        ? `insights/owner/rooms?${readScope ? `branchId=${readScope}` : ""}`
        : `rooms?branchId=${branch}`,
      revision,
    ),
    room = query.data?.find((r) => r.roomId === selected),
    history = useQuery(selected ? `rooms/${selected}/history` : null, revision);
  const roomTypes = useCatalogRows(
    readScope === undefined && !housekeeping ? "room-types" : null,
    branch,
    revision,
  );
  const roomType = (r: Row) =>
    r.roomTypeName
      ? { name: r.roomTypeName, capacity: r.capacity }
      : roomTypes.data?.find((t) => t.roomTypeId === r.roomTypeId);
  const typeName = (name?: string) => name ? ({standard: "Tiêu chuẩn", suite: "Phòng cao cấp", deluxe: "Cao cấp", superior: "Nâng cao"}[name.toLowerCase()] || name) : "Chưa có thông tin";
  const reserved = (r: Row) => r.isReserved || r.operationalStatus === "RESERVED";
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
          Hạng phòng: <strong>{typeName(type?.name)}</strong>
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
  const status = (r: Row) =>
    reserved(r) ? "RESERVED" : r.isOccupied
      ? "OCCUPIED"
      : reception &&
          [
            "PENDING_CLEANING",
            "PENDING_INSPECTION",
            "REWORK_REQUIRED",
            "CLEANING_IN_PROGRESS",
          ].includes(r.readiness)
        ? "NEEDS_CLEANING"
        : r.readiness;
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
    reserved(r) ? "RESERVED" : r.isOccupied
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
        (!cleanliness || (r.cleaningStatus || "NONE") === cleanliness) &&
        (!occupancy || r.isOccupied === (occupancy === "occupied")) &&
        (!blocked || r.isBlocked === (blocked === "blocked")) &&
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
        {(readScope !== undefined || housekeeping) && (
          <button aria-expanded={filters} onClick={() => setFilters(!filters)}>
            <SlidersHorizontal size={16} /> Bộ lọc
          </button>
        )}
        {housekeeping && (
          <button aria-pressed={table} onClick={() => setTable(!table)}>
            {table ? "Xem dạng ô" : "Xem dạng bảng"}
          </button>
        )}
        <div className="tabs" aria-label="Lọc phòng">
          {[
            ["ALL", "Tất cả"],
            ["READY", "Sẵn sàng"],
            ["RESERVED", "Đã đặt"],
            ["OCCUPIED", "Có khách"],
            ["TO_SERVICE", reception ? "Cần dọn dẹp" : "Cần phục vụ"],
            ["BLOCKED", "Bảo trì"],
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
      {(readScope !== undefined || housekeeping) && filters && (
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
                      `${r.branchName ? `${r.branchName} · ` : ""}${r.floor || r.locationId}`,
                    ]),
                ).entries(),
              ).map(([id, name]) => (
                <option key={id} value={id}>
                  {name}
                </option>
              ))}
            </select>
          </label>
          {housekeeping && (
            <>
              <label>
                Vệ sinh
                <select
                  value={cleanliness}
                  onChange={(e) => {
                    setCleanliness(e.target.value);
                    setPage(1);
                  }}
                >
                  <option value="">Tất cả</option>
                  {Array.from(
                    new Set(query.data?.map((r) => r.cleaningStatus || "NONE")),
                  ).map((s) => (
                    <option key={s} value={s}>
                      {s === "NONE" ? "Chưa có công việc" : label(s)}
                    </option>
                  ))}
                </select>
              </label>
              <label>
                Có khách
                <select
                  value={occupancy}
                  onChange={(e) => {
                    setOccupancy(e.target.value);
                    setPage(1);
                  }}
                >
                  <option value="">Tất cả</option>
                  <option value="occupied">Có khách</option>
                  <option value="vacant">Trống</option>
                </select>
              </label>
              <label>
                Bảo trì
                <select
                  value={blocked}
                  onChange={(e) => {
                    setBlocked(e.target.value);
                    setPage(1);
                  }}
                >
                  <option value="">Tất cả</option>
                  <option value="blocked">Bảo trì</option>
                  <option value="clear">Không bảo trì</option>
                </select>
              </label>
            </>
          )}
          <button
            onClick={() => {
              setFloor("");
              setCleanliness("");
              setOccupancy("");
              setBlocked("");
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
        <AlertPopup tone="success">
          {notice}
        </AlertPopup>
      )}
      {roomTypes.error && (
        <AlertPopup>
          Chưa tải được hạng phòng và sức chứa.{" "}
          <button onClick={() => bump((x) => x + 1)}>Thử lại</button>
        </AlertPopup>
      )}
      <State {...query} retry={() => bump((x) => x + 1)}>
        {table && housekeeping ? (
          <div className="table-scroll">
            <table>
              <thead>
                <tr>
                  <th>Phòng</th>
                  <th>Tầng</th>
                  <th>Hạng phòng</th>
                  <th>Có khách</th>
                  <th>Vệ sinh / vận hành</th>
                  <th>Thao tác</th>
                </tr>
              </thead>
              <tbody>
                {pageRooms.map((r) => (
                  <tr key={r.roomId}>
                    <td>{r.code}</td>
                    <td>{r.floor}</td>
                    <td>
                      {typeName(r.roomTypeName)} · {r.capacity} người
                    </td>
                    <td>{r.isOccupied ? "Có khách" : "Trống"}</td>
                    <td>
                      <Badge value={r.readiness} />
                      {r.isBlocked && " · Bảo trì"}
                    </td>
                    <td>
                      <button
                        onClick={() => {
                          setSelected(r.roomId);
                          setCommand("");
                          setError("");
                        }}
                      >
                        Chi tiết
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ) : (
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
                {(r.isOccupied || reserved(r)) && r.readiness !== "READY" && (
                  <Badge value={r.readiness} />
                )}
                {roomInfo(r)}
              </button>
            ))}
          </div>
        )}
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
            {(room.isOccupied || reserved(room)) && room.readiness !== "READY" && (
              <Badge value={room.readiness} />
            )}
          </div>
          {roomInfo(room)}
          {reception && room.canOperate !== true && (
            <p role="status">Bạn cần bắt đầu ca và đang trong giờ làm việc để thay đổi trạng thái phòng. <a href="#workday">Mở lịch làm việc</a></p>
          )}
          {housekeeping && (
            <p>
              Vệ sinh:{" "}
              <Badge value={room.cleaningStatus || "Chưa có công việc"} /> ·
              Kiểm tra:{" "}
              <Badge
                value={room.inspectionStatus || "Chưa có phiếu kiểm tra"}
              />
            </p>
          )}
          {housekeeping && (
            <>
              <p>
                {room.floor} · {room.isOccupied ? "Có khách" : "Trống"} ·{" "}
                {room.isBlocked ? "Bảo trì" : "Không bảo trì"}
              </p>
              <State {...workflow} retry={() => bump((x) => x + 1)}>
                {[
                  ["cleaning", "Công việc vệ sinh"],
                  ["inspections", "Kiểm tra gần nhất"],
                  ["incidents", "Sự cố liên quan"],
                ].map(
                  ([key, title]) =>
                    workflow.data?.[key] && (
                      <section key={key}>
                        <h3>{title}</h3>
                        {workflow.data[key].map((w: Row) => (
                          <a
                            className="overview-task"
                            key={w.workId}
                            href={`#work?kind=${w.kind}&id=${w.workId}`}
                          >
                            {w.title} <Badge value={w.status} />
                          </a>
                        ))}
                        {!workflow.data[key].length && <Empty />}
                      </section>
                    ),
                )}
              </State>
            </>
          )}
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
                {room.isBlocked ? "Bảo trì" : "Không bảo trì"}
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
            {reception && room.canOperate === true &&
              !room.isOccupied &&
              !reserved(room) &&
              room.readiness === "READY" &&
              can("ROOM_CHECKIN") && (
                <button
                  className="primary"
                  onClick={() => setCommand("reserve")}
                >
                  Đặt phòng
                </button>
              )}
            {reception && room.canOperate === true && reserved(room) && can("ROOM_CHECKIN") && (
              <>
                <button className="primary" disabled={room.readiness !== "READY"} onClick={() => setCommand("check-in")}>
                  Khách đến nhận phòng
                </button>
                <button className="room-action-cancel" disabled={room.readiness !== "READY"} onClick={() => setCommand("cancel-reservation")}>
                  Hủy đặt phòng
                </button>
              </>
            )}
            {reception && room.canOperate === true && room.isOccupied && can("ROOM_CHECKOUT") && (
              <button
                className="primary"
                onClick={() => setCommand("check-out")}
              >
                Trả phòng
              </button>
            )}
            {reception && room.canOperate === true && (room.isOccupied || reserved(room)) && can("ROOM_OCCUPANCY_MOVE") && (
              <button className="room-action-move" onClick={() => setCommand("move")}>Chuyển phòng</button>
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
            {can("IR_CREATE") && !profile.roles.some(r=>r.code==="OPERATION_MANAGER"&&(r.branchId===branch||r.branchId===null)) && (
              <a
                className="button-link"
                href={`#room-issues?room=${room.roomId}&create=1`}
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
                {command === "reserve" ? "Xác nhận đặt phòng"
                  : command === "cancel-reservation" ? "Xác nhận hủy đặt phòng"
                  : command === "check-in"
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
                          !reserved(r) &&
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
              {["move", "block", "unblock"].includes(command) && <label>
                Ghi chú / lý do
                <textarea
                  name="reason"
                  minLength={10}
                  maxLength={2000}
                  required={["move", "block", "unblock"].includes(command)}
                />
              </label>}
              <p className="hint">
                {command === "check-out"
                  ? "Phòng sẽ chuyển sang cần dọn. Không thể nhận khách mới trước khi hoàn tất kiểm tra."
                  : command === "move" ? (reserved(room)
                    ? "Khách chưa vào ở: phòng nguồn trở về sẵn sàng, phòng đích chuyển sang đã đặt."
                    : "Khách đã sử dụng: phòng nguồn chuyển sang cần dọn, phòng đích chuyển sang có khách.")
                  : command === "reserve" ? "Phòng được giữ cho khách, chưa ghi nhận khách đã vào ở."
                  : command === "cancel-reservation" ? "Khách chưa sử dụng phòng. Hủy đặt sẽ đưa phòng về sẵn sàng."
                  : "Thao tác sẽ được ghi vào lịch sử phòng."}
              </p>
              <button className={command === "cancel-reservation" ? "primary room-action-cancel" : command === "move" ? "primary room-action-move" : "primary"} disabled={pending}>
                Xác nhận
              </button>
            </form>
          )}
          {error && (
            <AlertPopup>
              {error}
            </AlertPopup>
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
