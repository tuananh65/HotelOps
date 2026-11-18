import {
  ActionDialog,
  CollapsibleFilterPanel,
} from "../components/workspace-interactions";
import { displayCode } from "../i18n/vi";
import { StructureDeactivationPanel } from "../structure-deactivation/StructureDeactivationPanel";
import { DepartmentDeactivationPanel } from "./DepartmentDeactivationPanel";
import { RoomTypeDirectoryResults } from "./RoomTypeDirectoryResults";
import { RoomDirectoryResults } from "./RoomDirectoryResults";
import "./room-directory-layout.css";
import { useRef, useState } from "react";
import { Link, Navigate, useLocation, useParams } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import { decimalId, object } from "./contracts";
import type {
  Context,
  Filters,
  MutationKind,
  MutationRequests,
  ReadTypes,
} from "./contracts";
import {
  draftRequest,
  fields,
  mutationFor,
  readFor,
  titles,
} from "./operations";
import type { Action, Screen } from "./operations";
import { unknownHotelOutcome, useHotelFlow } from "./useHotelFlow";
import type { HotelSnapshot } from "./useHotelFlow";

function Values({ value }: { value: unknown }) {
  if (value === null) return <span>—</span>;
  if (Array.isArray(value))
    return value.length ? (
      <ol>
        {value.map((item, i) => (
          <li key={i}>
            <Values value={item} />
          </li>
        ))}
      </ol>
    ) : (
      <p>Không có hồ sơ trên trang này.</p>
    );
  if (object(value))
    return (
      <dl>
        {Object.entries(value)
          .filter(([key]) => !key.endsWith("Url"))
          .map(([key, item]) => (
            <div key={key}>
              <dt>{key.replace(/([A-Z])/g, " $1")}</dt>
              <dd>
                <Values value={item} />
              </dd>
            </div>
          ))}
      </dl>
    );
  return <span>{String(value)}</span>;
}
function MutationForm({
  kind,
  context,
  snapshot,
  save,
}: {
  kind: MutationKind;
  context: Context;
  snapshot: HotelSnapshot | null;
  save(
    kind: MutationKind,
    c: Context,
    request: MutationRequests[MutationKind],
    snapshot: HotelSnapshot | null,
  ): Promise<void>;
}) {
  const raw: unknown = snapshot?.data;
  const source: Record<string, unknown> = object(raw)
    ? object(raw.details)
      ? raw.details
      : raw
    : {};
  const [draft, setDraft] = useState<Record<string, string>>(() =>
    Object.fromEntries(
      fields[kind].map((f) => [
        f.key,
        (kind === "updateType" && f.key === "code") || source[f.key] == null
          ? ""
          : String(source[f.key]),
      ]),
    ),
  );
  const [review, setReview] = useState<MutationRequests[MutationKind] | null>(
    null,
  );
  const reviewed = useRef<typeof review>(null);
  const [error, setError] = useState("");
  function preview() {
    reviewed.current = null;
    setReview(null);
    const request = draftRequest(kind, draft, context);
    if (!request) {
      setError(
        "Kiểm tra trường bắt buộc, độ dài và mã số. Mã chỉ nhận số nguyên dương trong giới hạn cho phép.",
      );
      return;
    }
    setError("");
    reviewed.current = request;
    setReview(request);
  }
  function confirm() {
    const request = reviewed.current;
    if (!request) return;
    reviewed.current = null;
    setReview(null);
    void save(kind, context, request, snapshot);
  }
  return (
    <form
      aria-label="Thay đổi cấu trúc khách sạn"
      noValidate
      onSubmit={(e) => e.preventDefault()}
    >
      {fields[kind].map((f) => (
        <label key={f.key}>
          {f.label}
          {f.type === "longtext" ? (
            <textarea
              name={f.key}
              required={f.required}
              maxLength={f.max}
              value={draft[f.key]}
              onChange={(e) => {
                setDraft({ ...draft, [f.key]: e.target.value });
                reviewed.current = null;
                setReview(null);
                setError("");
              }}
            />
          ) : (
            <input
              name={f.key}
              type="text"
              inputMode={
                f.type === "id" || f.type === "integer" ? "numeric" : "text"
              }
              autoComplete="off"
              required={f.required}
              maxLength={f.max}
              value={draft[f.key]}
              onChange={(e) => {
                setDraft({ ...draft, [f.key]: e.target.value });
                reviewed.current = null;
                setReview(null);
                setError("");
              }}
            />
          )}
        </label>
      ))}
      {kind.includes("Type") && (
        <p>
          Dùng mã danh sách kiểm tra đã công bố hợp lệ do quản trị viên cung
          cấp. Màn này không tạo hoặc sửa danh sách kiểm tra.
        </p>
      )}
      {kind === "deactivateType" && (
        <p>
          MSG33: Vô hiệu hóa cần xác nhận rõ ràng. Máy chủ kiểm tra phòng đang
          hoạt động liên quan.
        </p>
      )}
      <button type="button" onClick={preview}>
        Xem lại thay đổi
      </button>
      {review && (
        <section aria-label="Xem lại thay đổi cấu trúc khách sạn">
          <p>Mã chi nhánh: {context.branchId}</p>
          {context.id && <p>Mã hồ sơ: {context.id}</p>}
          <Values value={review} />
          <p>
            Máy chủ kiểm tra lại quyền và ràng buộc. Tải hồ sơ không cấp quyền
            lưu.
          </p>
          <button type="button" onClick={confirm}>
            Xác nhận thay đổi
          </button>
        </section>
      )}
      {error && <p role="alert">{error}</p>}
    </form>
  );
}
export function HotelWorkspace({
  screen = "structure",
  readOnly = false,
  directoryBasePath = "/rooms",
  canonicalStructure = false,
}: {
  screen?: Screen;
  readOnly?: boolean;
  directoryBasePath?: string;
  canonicalStructure?: boolean;
}) {
  const { state, actions } = useAuth();
  const route = useLocation(),
    params = useParams();
  const search = new URLSearchParams(route.search);
  const [branch, setBranch] = useState(
    params.branchId ?? search.get("branchId") ?? "",
  );
  const [record, setRecord] = useState(params.id ?? "");
  const [action, setAction] = useState<Action>(
    canonicalStructure && search.get("action") === "create"
      ? "create"
      : params.id
        ? screen === "rooms" && search.get("view") === "history"
          ? "history"
          : "detail"
        : "browse",
  );
  const [query, setQuery] = useState("");
  const [active, setActive] = useState("");
  const [page, setPage] = useState("1");
  const [sort, setSort] = useState(screen === "rooms" ? "floor" : "code");
  const [descending, setDescending] = useState(false);
  const [allBranches, setAllBranches] = useState(false);
  const [collection, setCollection] = useState("floors");
  const [nodeType, setNodeType] = useState("branch"),
    [nodeId, setNodeId] = useState("");
  const [floorFilter, setFloorFilter] = useState(""),
    [areaFilter, setAreaFilter] = useState(""),
    [typeFilter, setTypeFilter] = useState(""),
    [statusFilter, setStatusFilter] = useState("");
  const branchId = decimalId(branch),
    id = decimalId(record),
    pageNumber = decimalId(page);
  const kind = mutationFor(screen, action);
  const readKind = readFor(screen, action);
  const key = JSON.stringify([
    route.pathname,
    route.search,
    screen,
    action,
    branch,
    record,
    query,
    active,
    page,
    sort,
    descending,
    allBranches,
    collection,
    nodeType,
    nodeId,
    floorFilter,
    areaFilter,
    typeFilter,
    statusFilter,
  ]);
  const flow = useHotelFlow(key);
  if (state.status === "ANONYMOUS" || state.status === "EXPIRED_INVALID")
    return <Navigate to="/login" replace />;
  if (state.status === "RESTRICTED")
    return <Navigate to="/change-password" replace />;
  if (state.status !== "AUTHENTICATED" || !state.session)
    return (
      <main>
        <p role="status">Chưa xác minh được phiên. Dữ liệu tài khoản đã ẩn.</p>
        {state.status !== "INITIALIZING" && (
          <button type="button" onClick={() => void actions.revalidate()}>
            Kiểm tra phiên đăng nhập
          </button>
        )}
      </main>
    );
  const needsId = !["browse", "create"].includes(action);
  const globalList =
    (screen === "rooms" || screen === "roomTypes") &&
    action === "browse" &&
    allBranches;
  const valid =
    (globalList || branchId !== null) &&
    (!needsId || id !== null) &&
    (screen !== "structure" ||
      nodeType === "branch" ||
      decimalId(nodeId) !== null) &&
    pageNumber !== null &&
    [floorFilter, areaFilter, typeFilter].every(
      (v) => v === "" || decimalId(v) !== null,
    );
  const context: Context | null = valid
    ? { branchId: globalList ? 0 : branchId!, ...(needsId ? { id: id! } : {}) }
    : null;
  const filters: Filters = {
    page: pageNumber ?? 1,
    pageSize: 20,
    isActive: active,
    search: query || undefined,
  };
  if (globalList) filters.allBranches = true;
  if (
    screen === "structure" ||
    screen === "floors" ||
    screen === "areas" ||
    screen === "departments"
  )
    filters.collection = screen === "structure" ? collection : screen;
  if (screen === "structure") {
    filters.nodeType = nodeType;
    filters.nodeId =
      nodeType === "branch" ? undefined : (decimalId(nodeId) ?? undefined);
    if (nodeType === "room" || nodeType === "department")
      filters.collection = undefined;
  }
  if (screen === "roomTypes" || screen === "rooms") {
    filters.sort = sort;
    filters.descending = descending;
  }
  if (screen === "rooms") {
    filters.floorId = decimalId(floorFilter) ?? undefined;
    filters.areaId = decimalId(areaFilter) ?? undefined;
    filters.roomTypeId = decimalId(typeFilter) ?? undefined;
    filters.operationalStatusCode = statusFilter || undefined;
  }
  const snapshot = flow.snapshot;
  const typeReadOnly =
    readKind === "roomType" &&
    snapshot &&
    (!(snapshot.data as ReadTypes["roomType"]).canUpdate ||
      (snapshot.data as ReadTypes["roomType"]).details.branchId !== branchId);
  const snapshotValue: unknown = snapshot?.data;
  const inactive =
    object(snapshotValue) &&
    (snapshotValue.isActive === false ||
      (object(snapshotValue.details) &&
        snapshotValue.details.isActive === false));
  const formAllowed =
    !readOnly &&
    !(
      action === "deactivate" &&
      snapshot &&
      readKind === "roomType" &&
      (snapshot.data as ReadTypes["roomType"]).activeRooms > 0
    ) &&
    context &&
    kind &&
    !flow.unknown &&
    !["pending", "reading", "confirmed", "stale", "denied"].includes(
      flow.phase,
    ) &&
    (action === "create" || (snapshot && !typeReadOnly && !inactive));
  const pending = flow.phase === "pending";
  return (
    <main
      className={`hotel-workspace${readOnly && screen === "rooms" ? " room-directory-workspace" : ""}`}
    >
      <h1>Cấu trúc khách sạn — {titles[screen]}</h1>
      {readOnly && screen === "rooms" && (
        <p className="room-directory-intro">
          Tra cứu phòng theo chi nhánh, vị trí và trạng thái. Chọn phạm vi rồi
          tải danh sách để bắt đầu.
        </p>
      )}
      <nav aria-label="Điều hướng cấu trúc khách sạn">
        <Link to="/profile">Hồ sơ cá nhân</Link>
        {screen === "structure" && (
          <Link to="/branches/new">Tạo chi nhánh</Link>
        )}
        {screen === "structure" && branchId && (
          <Link to={`/branches/${branchId}/edit`}>
            Cập nhật thông tin chi nhánh
          </Link>
        )}
        {Object.entries(
          canonicalStructure
            ? {
                "/hotel": "Cấu trúc chi nhánh",
                "/structure/floors": "Tầng",
                "/structure/areas": "Khu vực",
                "/structure/departments": "Bộ phận",
                "/structure/room-types": "Loại phòng",
                "/structure/rooms": "Phòng",
              }
            : readOnly
              ? {
                  "/hotel": "Cấu trúc chi nhánh",
                  [directoryBasePath]: "Danh mục phòng",
                }
              : {
                  "/hotel": "Cấu trúc chi nhánh",
                  "/floors": "Tầng",
                  "/areas": "Khu vực",
                  "/room-types": "Loại phòng",
                  "/rooms": "Phòng",
                  "/departments": "Bộ phận",
                },
        ).map(([to, label]) => (
          <Link key={to} to={branchId ? `${to}?branchId=${branchId}` : to}>
            {label}
          </Link>
        ))}
      </nav>
      {!readOnly && !canonicalStructure && (
        <p>
          Dữ liệu và quyết định từ máy chủ. Không dùng danh tính hoặc quyền giả
          lập.
        </p>
      )}
      {!readOnly && !canonicalStructure && (
        <p>
          Danh sách chi nhánh và thao tác vô hiệu hóa chi nhánh, tầng, khu vực,
          phòng, bộ phận chưa được tích hợp trong phần này.
        </p>
      )}
      <CollapsibleFilterPanel
        activeCount={
          snapshot
            ? [
                query,
                floorFilter,
                areaFilter,
                typeFilter,
                statusFilter,
                active,
              ].filter(Boolean).length
            : 0
        }
      >
        <section aria-label="Chọn cấu trúc khách sạn">
          {readOnly && screen === "rooms" && (
            <header className="room-directory-filter-heading">
              <h2>Bộ lọc danh mục phòng</h2>
              <p>Phạm vi truy cập được kiểm tra theo tài khoản của bạn.</p>
            </header>
          )}
          <label>
            Mã chi nhánh
            <input
              name="branchId"
              type="text"
              inputMode="numeric"
              value={branch}
              disabled={pending || globalList}
              onChange={(e) => {
                setBranch(e.target.value);
                setPage("1");
              }}
            />
          </label>
          <label>
            Thao tác
            <select
              aria-label="Thao tác"
              value={action}
              disabled={pending}
              onChange={(e) => setAction(e.target.value as Action)}
            >
              <option value="browse">Danh sách</option>
              {screen !== "structure" && (
                <>
                  <option value="detail">Xem hồ sơ</option>
                  {!readOnly && <option value="create">Tạo mới</option>}
                  {!readOnly && <option value="edit">Cập nhật</option>}
                </>
              )}
              {!readOnly && screen === "roomTypes" && (
                <option value="deactivate">Vô hiệu hóa loại phòng</option>
              )}
              {screen === "rooms" && (
                <option value="history">Lịch sử trạng thái đã ghi nhận</option>
              )}
            </select>
          </label>
          {needsId && (
            <label>
              Mã hồ sơ
              <input
                name="recordId"
                type="text"
                inputMode="numeric"
                value={record}
                disabled={pending}
                onChange={(e) => setRecord(e.target.value)}
              />
            </label>
          )}
          {action === "history" && (
            <label>
              Trang lịch sử
              <input
                name="page"
                value={page}
                inputMode="numeric"
                onChange={(e) => setPage(e.target.value)}
              />
            </label>
          )}
          {action === "browse" && (
            <>
              {(screen === "rooms" || screen === "roomTypes") && (
                <label>
                  <input
                    type="checkbox"
                    checked={allBranches}
                    onChange={(e) => {
                      setAllBranches(e.target.checked);
                      setPage("1");
                    }}
                  />
                  Tất cả chi nhánh được máy chủ cho phép
                </label>
              )}
              <label>
                Bộ lọc hoạt động
                <select
                  aria-label="Bộ lọc hoạt động"
                  value={active}
                  onChange={(e) => {
                    setActive(e.target.value);
                    setPage("1");
                  }}
                >
                  <option value="">Tất cả</option>
                  <option value="true">Đang hoạt động</option>
                  <option value="false">Ngừng hoạt động</option>
                </select>
              </label>
              <label>
                Trang
                <input
                  name="page"
                  inputMode="numeric"
                  value={page}
                  onChange={(e) => setPage(e.target.value)}
                />
              </label>
              {screen === "structure" && (
                <>
                  <label>
                    Mục cấu trúc
                    <select
                      aria-label="Mục cấu trúc"
                      value={nodeType}
                      onChange={(e) => {
                        const v = e.target.value;
                        setNodeType(v);
                        setNodeId("");
                        setCollection(
                          v === "branch"
                            ? "floors"
                            : v === "floor"
                              ? "areas"
                              : "rooms",
                        );
                        setPage("1");
                      }}
                    >
                      {["branch", "floor", "area", "room", "department"].map(
                        (v) => (
                          <option key={v} value={v}>
                            {displayCode(v)}
                          </option>
                        ),
                      )}
                    </select>
                  </label>
                  {nodeType !== "branch" && (
                    <label>
                      Mã mục
                      <input
                        name="nodeId"
                        inputMode="numeric"
                        value={nodeId}
                        onChange={(e) => setNodeId(e.target.value)}
                      />
                    </label>
                  )}
                  {["branch", "floor", "area"].includes(nodeType) && (
                    <label>
                      Bộ sưu tập
                      <select
                        aria-label="Bộ sưu tập"
                        value={collection}
                        onChange={(e) => {
                          setCollection(e.target.value);
                          setPage("1");
                        }}
                      >
                        {(nodeType === "branch"
                          ? ["floors", "areas", "departments"]
                          : nodeType === "floor"
                            ? ["areas", "rooms"]
                            : ["rooms"]
                        ).map((v) => (
                          <option key={v} value={v}>
                            {displayCode(v)}
                          </option>
                        ))}
                      </select>
                    </label>
                  )}
                </>
              )}
              {(screen === "roomTypes" || screen === "rooms") && (
                <>
                  <label>
                    Tìm kiếm
                    <input
                      name="search"
                      value={query}
                      maxLength={screen === "rooms" ? 200 : 150}
                      onChange={(e) => {
                        setQuery(e.target.value);
                        setPage("1");
                      }}
                    />
                  </label>
                  <label>
                    Sắp xếp
                    <select
                      aria-label="Sắp xếp"
                      value={sort}
                      onChange={(e) => setSort(e.target.value)}
                    >
                      {(screen === "rooms"
                        ? ["floor", "roomNumber", "roomType", "status"]
                        : ["code", "name", "status"]
                      ).map((v) => (
                        <option key={v} value={v}>
                          {displayCode(v)}
                        </option>
                      ))}
                    </select>
                  </label>
                  <label>
                    <input
                      type="checkbox"
                      checked={descending}
                      onChange={(e) => setDescending(e.target.checked)}
                    />
                    Giảm dần
                  </label>
                </>
              )}
              {screen === "rooms" && (
                <>
                  <label>
                    Lọc theo mã tầng
                    <input
                      name="floorFilter"
                      value={floorFilter}
                      onChange={(e) => setFloorFilter(e.target.value)}
                    />
                  </label>
                  <label>
                    Lọc theo mã khu vực
                    <input
                      name="areaFilter"
                      value={areaFilter}
                      onChange={(e) => setAreaFilter(e.target.value)}
                    />
                  </label>
                  <label>
                    Lọc theo mã loại phòng
                    <input
                      name="typeFilter"
                      value={typeFilter}
                      onChange={(e) => setTypeFilter(e.target.value)}
                    />
                  </label>
                  <label>
                    Trạng thái đã ghi nhận
                    <select
                      aria-label="Trạng thái đã ghi nhận"
                      value={statusFilter}
                      onChange={(e) => setStatusFilter(e.target.value)}
                    >
                      <option value="">Tất cả</option>
                      {[
                        "AVAILABLE",
                        "OCCUPIED",
                        "DIRTY",
                        "CLEANING",
                        "INSPECTION",
                        "READY",
                        "OUT_OF_SERVICE",
                      ].map((v) => (
                        <option key={v} value={v}>
                          {displayCode(v)}
                        </option>
                      ))}
                    </select>
                  </label>
                </>
              )}
            </>
          )}
          {action !== "create" && (
            <button
              type="button"
              disabled={
                !context ||
                pending ||
                flow.phase === "reading" ||
                flow.phase === "confirmed"
              }
              onClick={() =>
                context && void flow.load(readKind, context, filters)
              }
            >
              Tải dữ liệu
            </button>
          )}
          {!valid && (
            <p role="status">
              Nhập mã chi nhánh, mã hồ sơ khi cần và số trang là số nguyên
              dương. Các ô mã chỉ nhận chữ số thập phân.
            </p>
          )}
        </section>
      </CollapsibleFilterPanel>
      {readOnly &&
        screen === "rooms" &&
        !snapshot &&
        !flow.message &&
        !flow.unknown &&
        flow.phase !== "reading" && (
          <section
            className="room-directory-initial"
            aria-label="Hướng dẫn tải danh sách"
          >
            <h2>Danh sách phòng</h2>
            <p>
              Chưa tải dữ liệu. Nhập Branch ID hoặc chọn All branches, sau đó
              bấm Load from server.
            </p>
          </section>
        )}
      {flow.message && <p role="alert">{flow.message}</p>}
      {flow.phase === "reading" && (
        <p role="status">Đang tải dữ liệu từ máy chủ...</p>
      )}
      {pending && <p role="status">Thay đổi đang chờ...</p>}
      {flow.unknown && <p role="alert">{unknownHotelOutcome}</p>}
      {action === "deactivate" &&
        snapshot &&
        readKind === "roomType" &&
        (snapshot.data as ReadTypes["roomType"]).activeRooms > 0 && (
          <section aria-label="Ràng buộc loại phòng">
            <p>
              MSG34: Có phòng đang hoạt động dùng loại phòng này. Đổi loại của
              các phòng đó trước khi vô hiệu hóa.
            </p>
            <ul>
              {(
                snapshot.data as ReadTypes["roomType"]
              ).activeRoomReferences?.map((r) => (
                <li key={r.id}>
                  {r.roomNumber} (#{r.id})
                </li>
              ))}
            </ul>
          </section>
        )}
      {snapshot && screen === "rooms" && context?.id && (
        <Link
          to={`/structure/rooms/${context.id}/lifecycle?branchId=${context.branchId}`}
        >
          Ngừng hoạt động / Kích hoạt lại phòng
        </Link>
      )}
      {snapshot && screen === "rooms" && context?.id && (
        <Link
          to={`/rooms/${context.id}/departments?branchId=${context.branchId}`}
        >
          Quản lý bộ phận phụ trách phòng
        </Link>
      )}
      {snapshot && (
        <section aria-label="Dữ liệu khách sạn từ máy chủ">
          <h2>Bản chụp dữ liệu từ máy chủ</h2>
          {screen === "rooms" && action === "browse" ? (
            <RoomDirectoryResults
              data={snapshot.data as ReadTypes["rooms"]}
              basePath={directoryBasePath}
            />
          ) : canonicalStructure &&
            screen === "roomTypes" &&
            action === "browse" ? (
            <RoomTypeDirectoryResults
              data={snapshot.data as ReadTypes["roomTypes"]}
            />
          ) : canonicalStructure && action === "browse" ? (
            <>
              <Values value={snapshot.data} />
              {(() => {
                const page = (snapshot.data as ReadTypes["structure"])[
                  screen as "floors" | "areas" | "departments"
                ];
                return page?.items.length ? (
                  <ul>
                    {page.items.map((node) => (
                      <li key={node.id}>
                        <Link
                          to={`/structure/${screen}/${node.id}?branchId=${branchId}`}
                        >
                          Đang mở {node.name} ({node.code})
                        </Link>
                      </li>
                    ))}
                  </ul>
                ) : (
                  <p>MSG10: Không có hồ sơ phù hợp bộ lọc này.</p>
                );
              })()}
            </>
          ) : (
            <Values value={snapshot.data} />
          )}
        </section>
      )}
      {canonicalStructure &&
        screen === "roomTypes" &&
        action === "detail" &&
        context?.id &&
        snapshot &&
        (snapshot.data as ReadTypes["roomType"]).canUpdate && (
          <nav aria-label="Thao tác loại phòng">
            <Link
              to={`/structure/room-types/${context.id}?branchId=${context.branchId}&action=edit`}
            >
              Cập nhật loại phòng
            </Link>
            <Link
              to={`/structure/room-types/${context.id}?branchId=${context.branchId}&action=deactivate`}
            >
              Xem lại vô hiệu hóa
            </Link>
          </nav>
        )}
      {canonicalStructure &&
        (screen === "floors" || screen === "areas") &&
        action === "detail" &&
        context?.id &&
        snapshot && (
          <StructureDeactivationPanel
            key={`${screen}:${context.branchId}:${context.id}`}
            kind={screen === "floors" ? "floor" : "area"}
            branchId={context.branchId}
            id={context.id}
            dispatchEnabled
          />
        )}
      {canonicalStructure &&
        screen === "departments" &&
        action === "detail" &&
        context?.id &&
        snapshot && (
          <DepartmentDeactivationPanel
            key={`${context.branchId}:${context.id}`}
            branchId={context.branchId}
            departmentId={context.id}
            onSaved={() => void flow.load(readKind, context, filters)}
          />
        )}
      {!canonicalStructure &&
        snapshot &&
        context?.id &&
        (screen === "floors" || screen === "areas") &&
        action === "detail" && (
          <Link
            to={`/${screen}/${context.id}/deactivation-review?branchId=${context.branchId}`}
          >
            Kiểm tra ràng buộc vô hiệu hóa
          </Link>
        )}
      {(typeReadOnly || inactive) && (
        <p>Hồ sơ từ máy chủ chỉ được xem trong quy trình này.</p>
      )}
      {formAllowed && (
        <>
          {canonicalStructure && screen === "departments" && (
            <p>
              Đổi trưởng bộ phận đồng thời chuyển thẩm quyền chỉ định trong cùng
              giao dịch. Phân công độc lập và công việc đã giao được giữ nguyên.
              Trưởng bộ phận mới phải có đủ vai trò, quyền chi nhánh và quyền
              nghiệp vụ.
            </p>
          )}
          <ActionDialog
            title="Thay đổi cấu trúc khách sạn"
            busy={pending}
            error={flow.message}
            successKey={flow.phase === "confirmed"}
          >
            <MutationForm
              key={`${key}:${snapshot?.revision ?? 0}`}
              kind={kind}
              context={context}
              snapshot={snapshot}
              save={flow.save}
            />
          </ActionDialog>
        </>
      )}
      {flow.phase === "confirmed" && (
        <section role="status" aria-label="Thay đổi khách sạn đã xác nhận">
          <h2>Máy chủ đã xác nhận thay đổi</h2>
          {canonicalStructure && <p>MSG08: Đã lưu thay đổi.</p>}
          <Values value={flow.receipt} />
          <p>Không gửi lại yêu cầu nào. Chọn thao tác khác để tiếp tục.</p>
        </section>
      )}
    </main>
  );
}
