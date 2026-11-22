import PageTitle from "./page-title";
import { useState, useEffect, useCallback, type ReactNode } from "react";
import { api, fields, message, type Page, type Row } from "./api";
import { useAuth } from "./auth";
import { Dialog, Empty, Pager, State, useQuery } from "./ui";
import { catalogs, type Field } from "./catalog-specs";
import RolePermissions from "./role-permissions";
import HotelSelector from "./hotel-selector";
import { useCatalogRows } from "./use-catalog-rows";
import { permissionText } from "./permission-copy";
import StatusLight from "./status-light";
import { SlidersHorizontal } from "lucide-react";
function Lookup({
  field,
  value,
  exclude,
  onReady,
}: {
  field: Field;
  value: unknown;
  exclude?: number;
  onReady: (key: string, ready: boolean) => void;
}) {
  const { branch } = useAuth();
  const workspace = ["references", "departments"].includes(field.lookup!);
  const query = useQuery(
    workspace
      ? `workspace/options?branchId=${branch}`
      : `catalog/${field.lookup}?branchId=${branch}&pageSize=100`,
  );
  const rows: Row[] = workspace
    ? query.data?.[field.lookup!] || []
    : fields(query.data?.items);
  useEffect(
    () => onReady(field.key, !query.loading && !query.error),
    [field.key, query.loading, query.error, onReady],
  );
  const key =
    field.lookup === "references"
      ? "referenceId"
      : catalogs.find((c) => c.id === field.lookup)?.key || "id";
  return (
    <State {...query}>
      <select
        name={field.key}
        required={field.required}
        defaultValue={String(value ?? "")}
      >
        <option value="">Chọn {field.label.toLocaleLowerCase()}</option>
        {rows
          .filter(
            (r) =>
              r[key] !== exclude &&
              (!field.filter || r.kind === field.filter) &&
              r.isActive !== false,
          )
          .map((r) => (
            <option key={r[key]} value={r[key]}>
              {r.name || r.code} {r.name && r.code ? `· ${r.code}` : ""}
            </option>
          ))}
      </select>
    </State>
  );
}
export default function Catalog({
  mode = "hotels",
  embedded = false,
  dialogOnly = false,
  initialEditing,
  resourceOverride,
  onFinish,
  toolbarAction,
  inventoryMode = false,
}: {
  mode?: "hotels" | "settings" | "supplies";
  embedded?: boolean;
  dialogOnly?: boolean;
  initialEditing?: Row;
  resourceOverride?: string;
  onFinish?: (saved?: boolean) => void;
  toolbarAction?: ReactNode;
  inventoryMode?: boolean;
}) {
  const { branch, can, canGlobal, refresh, setBranch } = useAuth();
  const visible = catalogs.filter(
    (c) =>
      (can(c.read, false) || (c.write && can(c.write, false))) &&
      (mode === "hotels"
        ? ["branches", "locations", "rooms"].includes(c.id)
        : mode === "settings"
          ? ["roles", "room-types", "departments"].includes(c.id)
          : ["items", "stock-policies", "warehouses", "assets"].includes(c.id)),
  );
  const [resource, setResource] = useState(
    resourceOverride ||
      new URLSearchParams(location.hash.split("?")[1]).get("resource") ||
      (inventoryMode ? "items" : null) ||
      (mode === "settings" && visible.some((c) => c.id === "roles")
        ? "roles"
        : null) ||
      (visible.some((c) => c.id === "branches") ? "branches" : null) ||
      visible[0]?.id ||
      "branches",
  );
  const catalog =
    visible.find((c) => c.id === resource) ||
    (mode === "settings" ? visible.find((c) => c.id === "roles") : undefined) ||
    visible[0];
  const spec = catalog;
  const loadAll = embedded || mode === "settings" || inventoryMode;
  const [inventoryFilters, setInventoryFilters] = useState(false),
    [inventorySort, setInventorySort] = useState("name"),
    [inventoryStatus, setInventoryStatus] = useState("");
  const [selectedHotel, setSelectedHotel] = useState(branch);
  const [inspectingItem, setInspectingItem] = useState<Row | null>(null);
  const inventoryOptions = useQuery(
    inventoryMode ? `workspace/options?branchId=${branch}` : null,
  );
  function inventoryValue(field: Field, row: Row) {
    if (!field.lookup) return String(row[field.key] ?? "—");
    const key =
      field.lookup === "references"
        ? "referenceId"
        : field.lookup === "warehouses"
          ? "warehouseId"
          : "itemId";
    return (
      inventoryOptions.data?.[field.lookup]?.find(
        (value: Row) => value[key] === row[field.key],
      )?.name || (row[field.key] == null ? "—" : "Đang tải…")
    );
  }
  const [page, setPage] = useState(1),
    [revision, bump] = useState(0),
    [editing, setEditing] = useState<Row | null>(initialEditing || null),
    [roleEditing, setRoleEditing] = useState<Row | null>(null),
    [lookupReady, setLookupReady] = useState<Record<string, boolean>>({}),
    [search, setSearch] = useState(""),
    [pending, setPending] = useState(false),
    [error, setError] = useState(""),
    [notice, setNotice] = useState(""),
    [locationFilter, setLocationFilter] = useState<number | null>(
      Number(
        new URLSearchParams(location.hash.split("?")[1]).get("location"),
      ) || null,
    );
  const pagedQuery = useQuery<Page>(
    spec && !loadAll && (!spec.scoped || branch > 0)
      ? `catalog/${spec.id}?${branch ? `branchId=${branch}&` : ""}page=${page}`
      : null,
    revision,
  );
  const allQuery = useCatalogRows(
    loadAll && (!spec?.scoped || branch > 0) ? spec?.id : null,
    branch,
    revision,
  );
  const roomLocations = useCatalogRows(
    embedded && spec?.id === "rooms" ? "locations" : null,
    branch,
  );
  const roomTypes = useCatalogRows(
    embedded && spec?.id === "rooms" ? "room-types" : null,
    branch,
  );
  const query = loadAll
    ? {
        ...allQuery,
        data: allQuery.data
          ? {
              items: allQuery.data.map((fields) => ({ type: spec.id, fields })),
              total: allQuery.data.length,
              page: 1,
              pageSize: 100,
            }
          : undefined,
      }
    : pagedQuery;
  const onReady = useCallback(
    (key: string, ready: boolean) =>
      setLookupReady((previous) =>
        previous[key] === ready ? previous : { ...previous, [key]: ready },
      ),
    [],
  );
  const lookups =
    spec?.fields.filter(
      (f) =>
        f.lookup &&
        !(spec.id === "assets" && editing?.[spec.key] && f.key === "roomId"),
    ) || [];
  const writable =
    !!spec?.write &&
    (!spec.scoped || branch > 0) &&
    (spec.scoped || spec.id === "items"
      ? can(spec.write, false)
      : canGlobal(spec.write));
  if (!spec) return <Empty>Không có danh mục trong phạm vi được cấp.</Empty>;
  const simpleStructureList = ["room-types", "departments"].includes(spec.id);
  const showStatus =
    spec.id !== "roles" &&
    !simpleStructureList &&
    (!inventoryMode || spec.fields.some((f) => f.key === "isActive"));
  const roomList = embedded && spec.id === "rooms";
  const columns = spec.fields
    .filter(
      (f) =>
        f.key !== "isActive" &&
        !(simpleStructureList && ["code", "description"].includes(f.key)) &&
        !(roomList && f.key === "departmentId") &&
        !(spec.id === "roles" && f.key === "code") &&
        !(embedded && locationFilter && f.key === "locationId"),
    )
    .slice(0, 5);
  const matching = fields(query.data?.items)
    .filter(
      (r) =>
        !inventoryMode ||
        !inventoryStatus ||
        String(r.isActive) === inventoryStatus,
    )
    .filter(
      (r) =>
        spec.id !== "rooms" ||
        !locationFilter ||
        r.locationId === locationFilter,
    )
    .filter((r) =>
      (spec.id === "permissions"
        ? Object.values(permissionText(r.code, r.description))
        : Object.values(r)
      ).some((v) =>
        String(v ?? "")
          .toLocaleLowerCase()
          .includes(search.toLocaleLowerCase()),
      ),
    );
  if (inventoryMode)
    matching.sort((a, b) =>
      String(a[inventorySort] ?? "").localeCompare(
        String(b[inventorySort] ?? ""),
        "vi",
      ),
    );
  const rows = loadAll ? matching.slice((page - 1) * 50, page * 50) : matching;
  async function save(form: HTMLFormElement) {
    if (pending || !editing) return;
    if (form.querySelector('[aria-busy="true"], .spinner')) {
      setError("Đang tải danh mục lựa chọn. Vui lòng chờ.");
      return;
    }
    setPending(true);
    setError("");
    const values = new FormData(form),
      data: Row = {};
    for (const f of spec.fields) {
      if (spec.id === "references" && editing[spec.key] && f.key === "kind")
        continue;
      if (spec.id === "roles" && editing[spec.key] && f.key === "code")
        continue;
      if (
        spec.id === "assets" &&
        editing[spec.key] &&
        ["roomId", "conditionCode"].includes(f.key)
      )
        continue;
      const value = values.get(f.key);
      data[f.key] =
        f.type === "checkbox"
          ? value === "on"
          : value === "" || value === null
            ? null
            : f.type === "number" || f.lookup
              ? Number(value)
              : String(value).trim();
    }
    try {
      await api(
        `catalog/${spec.id}${editing[spec.key] ? `/${editing[spec.key]}` : ""}${spec.scoped || spec.id === "items" ? `?branchId=${branch}` : ""}`,
        { fields: data, rowVersion: editing.rowVersion },
        editing[spec.key] ? "PUT" : "POST",
      );
      setEditing(null);
      bump((n) => n + 1);
      setNotice("Đã lưu danh mục.");
      if (spec.id === "branches") await refresh();
      onFinish?.(true);
    } catch (e) {
      setError(message(e));
    } finally {
      setPending(false);
    }
  }
  const createAction = writable && !roomList && (
    <button
      className="primary"
      onClick={() => {
        setEditing({});
        setLookupReady({});
        setError("");
      }}
    >
      + Thêm {spec.name.toLocaleLowerCase()}
    </button>
  );
  return (
    <>
      <div hidden={dialogOnly}>
        <div className="page-heading">
          {embedded ? (
            !roomList && <h2>{spec.name}</h2>
          ) : !dialogOnly ? (
            <PageTitle>
              <h1>
                {mode === "hotels"
                  ? "Cấu trúc khách sạn"
                  : mode === "settings"
                    ? "Cấu hình hệ thống"
                    : inventoryMode
                      ? "Danh mục vật tư"
                      : "Danh mục vật tư & tài sản"}
              </h1>
              {mode !== "settings" && (
                <p>
                  {spec.id === "branches"
                    ? "Chọn khách sạn để quản lý tầng, phòng và bộ phận."
                    : spec.name}
                </p>
              )}
            </PageTitle>
          ) : null}
        </div>
        <div
          className={embedded || mode === "settings" ? "" : "catalog-layout"}
        >
          {!embedded && mode !== "settings" && !inventoryMode && (
            <nav
              className="catalog-sections"
              aria-label={
                mode === "hotels" ? "Danh mục khách sạn" : "Danh mục cấu hình"
              }
            >
              {visible.map((c) => (
                <button
                  key={c.id}
                  aria-pressed={spec.id === c.id}
                  onClick={() => {
                    if (
                      mode === "hotels" &&
                      c.scoped &&
                      rows.some(
                        (r) => r.branchId === selectedHotel && r.isActive,
                      )
                    )
                      setBranch(selectedHotel);
                    location.hash = `${mode === "hotels" ? "catalog" : "supplies"}?resource=${c.id}`;
                    setResource(c.id);
                    setPage(1);
                    setSearch("");
                    setLocationFilter(null);
                    setNotice("");
                  }}
                >
                  {c.name}
                </button>
              ))}
            </nav>
          )}
          <div className="catalog-content">
            {!embedded && locationFilter && (
              <button onClick={() => setLocationFilter(null)}>
                Bỏ lọc tầng
              </button>
            )}
            <div
              className={`catalog-toolbar${roomList ? " room-list-toolbar" : ""}${mode === "settings" ? " settings-toolbar" : ""}`}
            >
              {toolbarAction}
              {(mode === "settings" || inventoryMode) && visible.length > 1 && (
                <label className="settings-select">
                  {inventoryMode ? "Nội dung danh mục" : "Nội dung cấu hình"}
                  <select
                    aria-label="Nội dung cấu hình"
                    value={spec.id}
                    onChange={(e) => {
                      location.hash = `${inventoryMode ? "supplies" : "settings"}?resource=${e.target.value}`;
                    }}
                  >
                    {visible.map((c) => (
                      <option key={c.id} value={c.id}>
                        {c.name}
                      </option>
                    ))}
                  </select>
                </label>
              )}
              {spec.id !== "branches" && (
                <label className="search">
                  {roomList
                    ? "Tìm phòng"
                    : loadAll
                      ? "Tìm trong danh mục"
                      : "Tìm trong trang"}
                  <input
                    placeholder={roomList ? "Nhập số phòng…" : undefined}
                    value={search}
                    onChange={(e) => {
                      setSearch(e.target.value);
                      setPage(1);
                    }}
                  />
                </label>
              )}
              {inventoryMode && (
                <button
                  aria-expanded={inventoryFilters}
                  onClick={() => setInventoryFilters(!inventoryFilters)}
                >
                  <SlidersHorizontal size={16} /> Bộ lọc
                </button>
              )}
              {createAction}
            </div>
            {inventoryMode && inventoryFilters && (
              <div className="toolbar">
                <label>
                  Sắp xếp
                  <select
                    value={inventorySort}
                    onChange={(e) => {
                      setInventorySort(e.target.value);
                      setPage(1);
                    }}
                  >
                    <option value="name">Tên</option>
                    <option value="code">Mã</option>
                  </select>
                </label>
                {spec.id !== "stock-policies" && (
                  <label>
                    Trạng thái
                    <select
                      value={inventoryStatus}
                      onChange={(e) => {
                        setInventoryStatus(e.target.value);
                        setPage(1);
                      }}
                    >
                      <option value="">Tất cả</option>
                      <option value="true">Hoạt động</option>
                      <option value="false">Ngừng sử dụng</option>
                    </select>
                  </label>
                )}
                <button
                  onClick={() => {
                    setSearch("");
                    setInventorySort("name");
                    setInventoryStatus("");
                    setPage(1);
                  }}
                >
                  Đặt lại
                </button>
              </div>
            )}
            {notice && (
              <p role="status" className="notice">
                {notice}
              </p>
            )}
            {spec.scoped && !branch ? (
              <Empty>
                Chưa có khách sạn trong phạm vi được cấp để cấu hình danh mục
                này.
              </Empty>
            ) : (
              <State {...query} retry={() => bump((n) => n + 1)}>
                {spec.id === "branches" ? (
                  <>
                    <HotelSelector
                      hotels={rows.map((r) => ({
                        id: r.branchId,
                        name: r.name,
                        active: r.isActive,
                      }))}
                      selected={selectedHotel}
                      onSelect={setSelectedHotel}
                    />
                    {rows.some((r) => r.branchId === selectedHotel) && (
                      <div className="hotel-actions">
                        {rows.find((r) => r.branchId === selectedHotel)
                          ?.isActive && (
                          <a
                            className="button-link primary-link"
                            href="#catalog?resource=locations"
                            onClick={() => setBranch(selectedHotel)}
                          >
                            Mở tầng & phòng
                          </a>
                        )}
                        {writable && (
                          <button
                            onClick={() => {
                              setEditing(
                                rows.find((r) => r.branchId === selectedHotel)!,
                              );
                              setLookupReady({});
                              setError("");
                            }}
                          >
                            Chỉnh sửa khách sạn
                          </button>
                        )}
                      </div>
                    )}
                  </>
                ) : (
                  <div className="table-scroll">
                    <table
                      className={
                        spec.id === "roles" ? "roles-table" : undefined
                      }
                    >
                      <thead>
                        <tr>
                          {spec.id === "permissions" ? (
                            <>
                              <th>Quyền</th>
                              <th>Mô tả</th>
                            </>
                          ) : (
                            columns.map((f) => <th key={f.key}>{f.label}</th>)
                          )}
                          {showStatus && <th>Trạng thái</th>}
                          {(writable ||
                            (inventoryMode && spec.id === "items")) && (
                            <th>Thao tác</th>
                          )}
                        </tr>
                      </thead>
                      <tbody>
                        {rows.map((r) => (
                          <tr key={r[spec.key]}>
                            {spec.id === "permissions" ? (
                              <>
                                <td>
                                  <strong>
                                    {permissionText(r.code, r.description).name}
                                  </strong>
                                </td>
                                <td className="permission-description">
                                  {
                                    permissionText(r.code, r.description)
                                      .description
                                  }
                                </td>
                              </>
                            ) : (
                              columns.map((f) => (
                                <td key={f.key}>
                                  {f.type === "checkbox"
                                    ? r[f.key]
                                      ? "Có"
                                      : "Không"
                                    : embedded &&
                                        spec.id === "rooms" &&
                                        f.lookup
                                      ? (f.key === "locationId"
                                          ? roomLocations.data?.find(
                                              (x) => x.locationId === r[f.key],
                                            )?.name
                                          : roomTypes.data?.find(
                                              (x) => x.roomTypeId === r[f.key],
                                            )?.name) ||
                                        (r[f.key] == null ? "—" : "Đang tải…")
                                      : inventoryMode
                                        ? inventoryValue(f, r)
                                        : f.choices?.find(
                                            (c) => c[0] === r[f.key],
                                          )?.[1] || String(r[f.key] ?? "—")}
                                </td>
                              ))
                            )}
                            {showStatus && (
                              <td>
                                <StatusLight active={r.isActive} />
                              </td>
                            )}
                            {(writable ||
                              (inventoryMode && spec.id === "items")) && (
                              <td>
                                <div
                                  className={
                                    spec.id === "roles"
                                      ? "role-actions"
                                      : undefined
                                  }
                                >
                                  {spec.id === "branches" && r.isActive && (
                                    <a
                                      className="button-link"
                                      href="#catalog?resource=locations"
                                      onClick={() => setBranch(r.branchId)}
                                    >
                                      Mở tầng / phòng
                                    </a>
                                  )}
                                  {spec.id === "locations" && (
                                    <button
                                      onClick={() => {
                                        setResource("rooms");
                                        setPage(1);
                                        setLocationFilter(r.locationId);
                                      }}
                                    >
                                      Xem phòng
                                    </button>
                                  )}
                                  {inventoryMode && spec.id === "items" && (
                                    <button
                                      onClick={() => setInspectingItem(r)}
                                    >
                                      Chi tiết
                                    </button>
                                  )}
                                  {writable && (
                                    <button
                                      className={
                                        spec.id === "roles"
                                          ? "role-edit"
                                          : undefined
                                      }
                                      onClick={() => {
                                        setEditing(r);
                                        setLookupReady({});
                                        setError("");
                                      }}
                                    >
                                      Chỉnh sửa
                                    </button>
                                  )}
                                  {spec.id === "roles" && (
                                    <button
                                      className="role-grants"
                                      onClick={() => setRoleEditing(r)}
                                    >
                                      Phân quyền
                                    </button>
                                  )}
                                </div>
                              </td>
                            )}
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                )}
                {!rows.length && <Empty />}
                <Pager
                  page={page}
                  total={loadAll ? matching.length : query.data?.total || 0}
                  onPage={setPage}
                />
              </State>
            )}
          </div>
        </div>
      </div>
      {roleEditing && (
        <RolePermissions
          role={roleEditing}
          close={() => setRoleEditing(null)}
        />
      )}
      {inspectingItem && (
        <Dialog
          title={inspectingItem.name}
          className="room-drawer"
          close={() => setInspectingItem(null)}
        >
          <State {...inventoryOptions}>
            <dl className="inventory-context">
              {spec.fields
                .filter((f) => f.key !== "isActive")
                .map((f) => (
                  <div key={f.key}>
                    <dt>{f.label}</dt>
                    <dd>{inventoryValue(f, inspectingItem)}</dd>
                  </div>
                ))}
            </dl>
            <p>
              {inspectingItem.isActive ? "Đang hoạt động" : "Ngừng sử dụng"}
            </p>
            <a
              className="button-link"
              href={`#stock?search=${encodeURIComponent(inspectingItem.code)}`}
            >
              Xem tồn và lịch sử biến động
            </a>
          </State>
        </Dialog>
      )}
      {editing && (
        <Dialog
          title={`${editing[spec.key] ? "Chỉnh sửa" : "Thêm"} ${spec.name.toLocaleLowerCase()}`}
          close={() => {
            if (!pending) {
              setEditing(null);
              onFinish?.();
            }
          }}
        >
          <form
            onSubmit={(e) => {
              e.preventDefault();
              save(e.currentTarget);
            }}
          >
            {spec.fields
              .filter(
                (f) =>
                  !(
                    spec.id === "assets" &&
                    editing[spec.key] &&
                    ["roomId", "conditionCode"].includes(f.key)
                  ),
              )
              .map((f) => (
                <label key={f.key}>
                  {f.label}
                  {f.lookup ? (
                    <Lookup
                      field={f}
                      onReady={onReady}
                      value={
                        editing[f.key] ??
                        (f.key === "locationId" ? locationFilter : undefined)
                      }
                      exclude={
                        f.key === "parentLocationId"
                          ? editing.locationId
                          : undefined
                      }
                    />
                  ) : f.choices ? (
                    <select
                      name={f.key}
                      required={f.required}
                      disabled={
                        spec.id === "references" &&
                        !!editing[spec.key] &&
                        f.key === "kind"
                      }
                      defaultValue={editing[f.key] || f.choices[0][0]}
                    >
                      {f.choices.map(([v, n]) => (
                        <option key={v} value={v}>
                          {n}
                        </option>
                      ))}
                    </select>
                  ) : (
                    <input
                      name={f.key}
                      type={f.type || "text"}
                      required={f.required}
                      maxLength={f.max}
                      defaultValue={
                        f.type === "checkbox"
                          ? undefined
                          : (editing[f.key] ?? f.default ?? "")
                      }
                      defaultChecked={
                        f.type === "checkbox"
                          ? (editing[f.key] ?? true)
                          : undefined
                      }
                      readOnly={
                        spec.id === "roles" &&
                        !!editing[spec.key] &&
                        f.key === "code"
                      }
                    />
                  )}
                </label>
              ))}
            {spec.fields.some((f) => f.key === "isActive") && (
              <p className="hint">
                Bỏ chọn “Đang hoạt động” để ngừng sử dụng. Hệ thống sẽ kiểm tra
                các dữ liệu còn liên quan trước khi lưu.
              </p>
            )}
            {error && (
              <p role="alert" className="alert">
                {error}
              </p>
            )}
            <button
              className="primary"
              disabled={pending || lookups.some((f) => !lookupReady[f.key])}
            >
              Lưu thay đổi
            </button>
          </form>
        </Dialog>
      )}
    </>
  );
}
