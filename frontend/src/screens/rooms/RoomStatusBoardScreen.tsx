import { referenceLabel } from "../../i18n/referenceLabel";
import { SearchInput, searchSuggestions } from "../../components/SearchInput";
import { userMessage } from "../../i18n/userMessage";
import { SearchableSelect } from "../../components/SearchableSelect";
import { useWorkspacePermissions } from "../housekeeping/useWorkspacePermissions";
import { FilterToggleButton } from "../../components/workspace-interactions";
import {
  useRoomWorkspace,
  useRoomChoices,
  RoomBranchField,
  NamedFilter,
  uniqueOptions,
  roomLabel,
  hotelTime,
} from "../housekeeping/RoomWorkspace";
import { LoadingIndicator } from "../../components/LoadingIndicator";
import { displayCode } from "../../i18n/vi";
import { useEffect, useMemo, useState, type FormEvent } from "react";
import { Link, useLocation, useSearchParams } from "react-router-dom";
import {
  positiveId,
  readinessCodes,
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
  type RoomSource,
} from "../../api/dev1/roomBoard";

import { RoomStatusDetails } from "./RoomStatusDetails";
import { operationalLabel } from "../housekeeping/OperationalReadView";
import "./room-status-board.css";

type Loaded = {
  key: symbol;
  board?: RoomDirectoryResponse;
  detail?: RoomDetail;
  history?: RoomHistory;
};
export function parseRoomContext(params: URLSearchParams) {
  for (const name of ["branchId", "roomId"]) {
    const values = params.getAll(name);
    if (values.length > 1) throw new Error("Ngữ cảnh phòng bị trùng");
    if (values.length) positiveId(values[0]);
  }
  const branch = params.get("branchId") ?? "";
  const room = params.get("roomId") ?? "";
  if (room && !branch) throw new Error("Phòng cần ngữ cảnh chi nhánh");
  return { branch, room };
}
// URL context is a navigation hint; the destination reauthorizes every read.
export function recordedCleaningHref(
  branchId: number,
  source: RoomSource | null,
): string | null {
  if (
    !Number.isSafeInteger(branchId) ||
    branchId <= 0 ||
    source?.availability !== "VERIFIED_SAME_ROOM" ||
    source.type !== "CLEANING_TASK" ||
    source.id === null ||
    !Number.isSafeInteger(source.id) ||
    source.id <= 0
  )
    return null;
  return `/cleaning/tasks/${source.id}?${new URLSearchParams({ branchId: String(branchId), monitor: "1", kind: "cleaning" })}`;
}
function RecordedSourceLink({
  branchId,
  source,
  canMonitor,
}: {
  branchId: number;
  source: RoomSource | null;
  canMonitor: boolean;
}) {
  const access = useWorkspacePermissions(branchId);
  const permission =
    source?.type === "CLEANING_TASK"
      ? "CLEANING_VIEW"
      : source?.type === "INSPECTION"
        ? "INSPECTION_VIEW"
        : "MAINTENANCE_VIEW";
  const scopedSource =
    source?.availability === "VERIFIED_SAME_ROOM" &&
    source.id !== null &&
    Number.isSafeInteger(source.id) &&
    source.id > 0 &&
    Number.isSafeInteger(branchId) &&
    branchId > 0;
  const href =
    recordedCleaningHref(branchId, source) ??
    (scopedSource && source?.type === "INSPECTION"
      ? `/inspection-history/${source.id}?branchId=${branchId}`
      : null) ??
    (scopedSource && source?.type === "MAINTENANCE"
      ? `/operations/maintenance/${source.id}?branchId=${branchId}&action=A004A-036`
      : null);
  return href &&
    access.allows(permission) &&
    (source?.type !== "CLEANING_TASK" || canMonitor) ? (
    <Link to={href}>
      Xem {operationalLabel(source?.type)} {referenceLabel(source, "id")}
    </Link>
  ) : null;
}
export default function RoomStatusBoardScreen() {
  const [params, setParams] = useSearchParams();
  const location = useLocation();
  const scope = useRoomWorkspace("rooms");
  const [advanced, setAdvanced] = useState(false);
  const [draftFloor, setDraftFloor] = useState(params.get("floorId") || "");
  const [draftArea, setDraftArea] = useState(params.get("areaId") || "");
  useEffect(() => {
    setDraftFloor(params.get("floorId") || "");
    setDraftArea(params.get("areaId") || "");
  }, [params]);
  const choices = useRoomChoices({
    ...scope,
    branch: scope.branch,
  });
  const [retry, setRetry] = useState(0);
  const query = params.toString(),
    key = useMemo(
      () => Symbol("room-read"),
      [location.key, query, retry, scope.owner],
    );
  let branch = "",
    room = "",
    contextError = "";
  try {
    ({ branch, room } = parseRoomContext(params));
  } catch (error) {
    contextError = roomBoardError(error);
  }
  const [loaded, setLoaded] = useState<Loaded | null>(null);
  const [failure, setFailure] = useState<{
    key: symbol;
    message: string;
  } | null>(null);
  useEffect(() => {
    const abort = new AbortController();
    if (contextError || !branch || !scope.branch || !scope.owner)
      return () => abort.abort();
    const timeout = window.setTimeout(() => {
      setFailure({
        key,
        message: "Tải danh sách phòng quá lâu. Vui lòng thử lại.",
      });
      abort.abort();
    }, 25000);
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
      } finally {
        window.clearTimeout(timeout);
      }
    }
    void load();
    return () => {
      window.clearTimeout(timeout);
      abort.abort();
    };
  }, [branch, room, query, key, contextError, scope.branch?.id, scope.owner]);
  const data =
    scope.branch && scope.owner && !contextError && loaded?.key === key
      ? loaded
      : null;
  const error = contextError || (failure?.key === key ? failure.message : "");
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
    <div className="room-workspace room-status-board">
      <div className="page-header">
        <div>
          <span className="eyebrow">Buồng phòng</span>
          <h1>Trạng thái vận hành phòng</h1>
          <p>
            Theo dõi tình trạng phòng, chọn số phòng để xem chi tiết và xử lý
            công việc.
          </p>
        </div>
      </div>
      <RoomBranchField scope={scope} />
      <form key={query} className="room-board-filters" onSubmit={submit}>
        <input type="hidden" name="branchId" value={scope.branch?.id ?? ""} />
        <div className="room-board-primary-filters">
          <label>
            Số phòng
            <SearchInput
              suggestions={searchSuggestions(data)}
              name="search"
              type="search"
              maxLength={200}
              placeholder="Nhập số phòng cần tìm"
              defaultValue={params.get("search") || ""}
            />
          </label>
          <label>
            Trạng thái vận hành
            <SearchableSelect
              aria-label="Trạng thái vận hành"
              name="readiness"
              defaultValue={params.get("readiness") || ""}
            >
              <option value="">Tất cả</option>
              {readinessCodes.map((s) => (
                <option key={s} value={s}>
                  {roomLabel(s)}
                </option>
              ))}
            </SearchableSelect>
          </label>
          <button disabled={!scope.branch}>Lọc phòng</button>
          <FilterToggleButton
            open={advanced}
            count={
              ["floorId", "areaId", "roomTypeId", "isActive"].filter(
                (name) => !!params.get(name),
              ).length
            }
            controls="room-board-advanced"
            onClick={() => {
              if (advanced) {
                setDraftFloor(params.get("floorId") || "");
                setDraftArea(params.get("areaId") || "");
              }
              if (advanced)
                document
                  .querySelectorAll<HTMLSelectElement>(
                    "#room-board-advanced select",
                  )
                  .forEach((select) => {
                    select.value = params.get(select.name) || "";
                  });
              setAdvanced((v) => !v);
            }}
          />
        </div>
        <div
          id="room-board-advanced"
          className="room-board-advanced"
          hidden={!advanced}
        >
          <div
            key={choices.items ? "ready" : "pending"}
            className="room-board-advanced-fields"
          >
            <label>
              Tầng
              <SearchableSelect
                name="floorId"
                aria-label="Tầng"
                value={draftFloor}
                onChange={(e) => {
                  setDraftFloor(e.target.value);
                  setDraftArea("");
                }}
              >
                <option value="">Tất cả</option>
                {uniqueOptions((choices.items ?? []).map((r) => r.floor)).map(
                  (f) => (
                    <option key={f.id} value={f.id}>
                      {f.name}
                    </option>
                  ),
                )}
              </SearchableSelect>
            </label>
            <label>
              Khu vực
              <SearchableSelect
                name="areaId"
                aria-label="Khu vực"
                value={draftArea}
                onChange={(e) => setDraftArea(e.target.value)}
              >
                <option value="">Tất cả khu vực thuộc tầng</option>
                {uniqueOptions(
                  (choices.items ?? [])
                    .filter(
                      (r) => !draftFloor || String(r.floor.id) === draftFloor,
                    )
                    .flatMap((r) => (r.area ? [r.area] : [])),
                ).map((a) => (
                  <option key={a.id} value={a.id}>
                    {a.name}
                  </option>
                ))}
              </SearchableSelect>
            </label>
            <NamedFilter
              name="roomTypeId"
              label="Loại phòng"
              value={params.get("roomTypeId") || ""}
              items={uniqueOptions(
                (choices.items ?? []).map((r) => r.roomType),
              )}
            />
            <label>
              Bộ lọc hoạt động
              <SearchableSelect
                name="isActive"
                defaultValue={params.get("isActive") || ""}
              >
                <option value="">Tất cả</option>
                <option value="true">Đang hoạt động</option>
                <option value="false">Ngừng hoạt động</option>
              </SearchableSelect>
            </label>
          </div>
        </div>
        {[
          "search",
          "readiness",
          "floorId",
          "areaId",
          "roomTypeId",
          "isActive",
        ].some((name) => params.has(name)) && (
          <button
            type="button"
            className="room-clear-filters"
            onClick={() => {
              if (scope.branch)
                setParams({ branchId: String(scope.branch.id) });
            }}
          >
            Xóa bộ lọc
          </button>
        )}
      </form>
      {choices.error && (
        <p role="alert">
          {userMessage(choices.error)}{" "}
          <button onClick={choices.reload}>Tải lại danh mục</button>
        </p>
      )}
      <div className="room-board-results-heading">
        <div>
          <h2>{room ? "Thông tin phòng" : "Danh sách phòng"}</h2>
          {board && (
            <span>
              {board.totalCount} phòng phù hợp · Trang {board.page}
            </span>
          )}
        </div>
        <button
          disabled={!scope.branch || (!data && !error)}
          onClick={() => setRetry((v) => v + 1)}
        >
          Tải lại trạng thái phòng
        </button>
      </div>
      {!branch && !error && (
        <p>Chọn chi nhánh để tải các phòng được phép xem.</p>
      )}
      {scope.branch && branch && !data && !error && (
        <LoadingIndicator label="Đang tải phòng" />
      )}
      {error && (
        <p role="alert">
          {userMessage(error)}{" "}
          <button onClick={() => setRetry((v) => v + 1)}>Thử lại</button>
        </p>
      )}
      {board && (
        <>
          <ul className="room-stats" aria-label="Bộ đếm trạng thái">
            {Object.entries(board.effectiveReadinessCounts ?? {}).map(
              ([state, count]) => (
                <li key={`effective-${state}`}>
                  {roomLabel(state)}: {count}
                </li>
              ),
            )}
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
                      <th>Trạng thái sử dụng</th>
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
                          <td>
                            {roomLabel(r.effectiveReadiness ?? "UNKNOWN")}
                          </td>
                          <td>{r.isActive ? "Có" : "Không"}</td>
                          <td>{roomLabel(r.operationalStatusCode)}</td>
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
        <RoomStatusDetails
          detail={detail}
          onChanged={() => setRetry(value => value + 1)}
          canMonitor={scope.branch?.canMonitor === true}
          history={history}
          branchName={scope.branch?.name ?? "Chi nhánh hiện tại"}
          close={() => {
            const next = new URLSearchParams(params);
            next.delete("roomId");
            next.delete("historyPage");
            setParams(next);
          }}
          paginate={(page) => paginate("historyPage", page)}
          sourceLink={(source) => (
            <RecordedSourceLink
              branchId={detail.room.branchId}
              source={source}
              canMonitor={scope.branch?.canMonitor === true}
            />
          )}
        />
      )}{" "}
    </div>
  );
}
