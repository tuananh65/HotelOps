import { AlertPopup } from "./alert-popup";
import PageTitle from "./page-title";
import PageToolbar from "./page-toolbar";
import Catalog from "./catalog";
import { useCatalogRows } from "./use-catalog-rows";
import { Fragment, useState } from "react";
import { action, api, fields, message, type Row } from "./api";
import { useAuth } from "./auth";
import { ConfirmationActions, Dialog, History, State, useQuery, Pager, Empty, Badge } from "./ui";
import { SlidersHorizontal } from "lucide-react";
import { assetConditionLabel, assetConditions } from "./asset-presentation";
import AssetConditionBar from "./asset-condition-bar";
import AssetTree from "./asset-tree";
export default function Assets({
  maintenance = false,
  readScope,
}: {
  maintenance?: boolean;
  readScope?: number;
}) {
  const { branch, can, profile } = useAuth();
  const branchRoles = profile.roles.filter(
    (r) => r.branchId === null || r.branchId === branch,
  );
  const groupedByRoom =
    readScope === undefined &&
    branchRoles.length > 0 &&
    branchRoles.every((r) => r.code === "OPERATION_MANAGER");
  const [editing, setEditing] = useState<Row | null>(null),
    [search, setSearch] = useState("");
  const [page, setPage] = useState(1),
    [revision, bump] = useState(0),
    [id, setId] = useState<number | null>(
      Number(new URLSearchParams(location.hash.split("?")[1]).get("id")) || null,
    ),
    [command, setCommand] = useState(""),
    [error, setError] = useState(""),
    [pending, setPending] = useState(false);
  const catalog = useCatalogRows(
    readScope === undefined && !groupedByRoom ? "assets" : null,
    branch,
    revision,
  );
  const monitoring = useQuery<Row[]>(
    readScope !== undefined
      ? `insights/owner/assets?${readScope ? `branchId=${readScope}` : ""}`
      : null,
    revision,
  );
  const roomAssets = useQuery<Row[]>(
    groupedByRoom ? `assets?branchId=${branch}` : null,
    revision,
  );
  const query =
      readScope !== undefined
        ? monitoring
        : groupedByRoom
          ? roomAssets
          : catalog,
    detail = useQuery(id ? `assets/${id}` : null, revision),
    options = useQuery(
      can("ROOM_VIEW") ? `workspace/options?branchId=${branch}` : null,
    );
  const asset = detail.data?.asset?.fields;
  const pendingHandover = fields(detail.data?.history).find((event) => event.handoverStatus);
  const canReceiveHandover = asset?.isActive && pendingHandover?.handoverStatus === "PENDING"
    && pendingHandover.toUserId === profile.user.fields.userId && readScope === undefined;
  const [filters, setFilters] = useState(false),
    [condition, setCondition] = useState(""),
    [sort, setSort] = useState("name");
  const rows = (query.data || [])
    .filter((a) => !condition || a.conditionCode === condition)
    .filter((a) =>
      `${a.name} ${a.assetCode} ${a.serialNumber || ""} ${a.roomCode || ""} ${a.locationName || ""}`
        .toLocaleLowerCase()
        .includes(search.trim().toLocaleLowerCase()),
    )
    .sort(
      (a, b) =>
        (groupedByRoom
          ? String(a.roomCode || "\uffff").localeCompare(
              String(b.roomCode || "\uffff"),
              "vi",
              { numeric: true },
            ) || Number(a.roomId || 0) - Number(b.roomId || 0)
          : 0) ||
        String(a[sort] || "").localeCompare(String(b[sort] || ""), "vi"),
    );
  const currentPage = Math.min(page, Math.max(1, Math.ceil(rows.length / 25)));
  const pageRows = rows.slice((currentPage - 1) * 25, currentPage * 25);
  return (
    <>
      <div className="page-heading">
        <PageTitle>
          <h1>Tài sản</h1>
          <p>Vị trí, tình trạng và lịch sử thay đổi</p>
        </PageTitle>
      </div>
      <PageToolbar
        actions={
          can("ASSET_MANAGE", false) && (
            <button className="primary" onClick={() => setEditing({})}>
              + Thêm tài sản
            </button>
          )
        }
      >
        <label className="search">
          Tìm tài sản
          <input
            value={search}
            placeholder={
              groupedByRoom
                ? "Tên, mã, số sê-ri hoặc phòng…"
                : "Tên, mã tài sản hoặc số sê-ri…"
            }
            onChange={(e) => {
              setSearch(e.target.value);
              setPage(1);
            }}
          />
        </label>
        {(maintenance || groupedByRoom) && (
          <button aria-expanded={filters} onClick={() => setFilters(!filters)}>
            <SlidersHorizontal size={16} /> Bộ lọc
          </button>
        )}
      </PageToolbar>
      {(maintenance || groupedByRoom) && filters && (
        <PageToolbar>
          <label>
            Tình trạng
            <select
              value={condition}
              onChange={(e) => {
                setCondition(e.target.value);
                setPage(1);
              }}
            >
              <option value="">Tất cả</option>
              {Object.entries(assetConditions).map(([s, name]) => (
                <option key={s} value={s}>
                  {name}
                </option>
              ))}
            </select>
          </label>
          <label>
            Sắp xếp
            <select
              value={sort}
              onChange={(e) => {
                setSort(e.target.value);
                setPage(1);
              }}
            >
              <option value="name">Tên</option>
              <option value="assetCode">Mã</option>
            </select>
          </label>
          <button
            onClick={() => {
              setSearch("");
              setCondition("");
              setSort("name");
              setPage(1);
            }}
          >
            Đặt lại
          </button>
        </PageToolbar>
      )}
      <State {...query} retry={() => bump((n) => n + 1)}>
        {groupedByRoom ? <AssetTree rows={rows} allRows={query.data || []} branchName={profile.branches.find(b => b.branchId === branch)?.name || "Chi nhánh"} select={assetId => { setId(assetId); setCommand(""); setError(""); }}/> : <>
        <div className="table-scroll">
          <table>
            <thead>
              <tr>
                <th>Tài sản</th>
                {readScope !== undefined && <th>Chi nhánh</th>}
                {groupedByRoom && <th>Vị trí</th>}
                <th>Mã</th>
                <th>Tình trạng</th>
                {groupedByRoom && <th>Sử dụng</th>}
                <th>Thao tác</th>
              </tr>
            </thead>
            <tbody>
              {pageRows.map((a, index) => (
                <Fragment key={a.assetId}>
                  {groupedByRoom &&
                    (index === 0 ||
                      a.roomId !== pageRows[index - 1].roomId) && (
                      <tr className="asset-group-heading">
                        <th colSpan={6} scope="rowgroup">
                          {a.roomId
                            ? `Phòng ${a.roomCode || a.roomId}`
                            : "Ngoài phòng / chưa bố trí"}
                        </th>
                      </tr>
                    )}
                  <tr key={a.assetId}>
                    <td>{a.name}</td>
                    {readScope !== undefined && <td>{a.branchName}</td>}
                    {groupedByRoom && (
                      <td>
                        {a.roomId
                          ? `Phòng ${a.roomCode || a.roomId}`
                          : a.locationName || "Chưa bố trí"}
                      </td>
                    )}
                    <td>{a.assetCode}</td>
                    <td>{assetConditionLabel(a.conditionCode)}</td>
                    {groupedByRoom && (
                      <td>{a.conditionCode === "PENDING_INSPECTION" ? "Chưa đưa vào sử dụng" : a.isActive ? "Đang sử dụng" : "Đã ngừng"}</td>
                    )}
                    <td>
                      <button
                        onClick={() => {
                          setId(a.assetId);
                          setCommand("");
                          setError("");
                        }}
                      >
                        Chi tiết
                      </button>
                    </td>
                  </tr>
                </Fragment>
              ))}
            </tbody>
          </table>
        </div>
        {!rows.length && (
          <Empty>
            {search || condition ? (
              <>
                Không có tài sản phù hợp.{" "}
                <button
                  onClick={() => {
                    setSearch("");
                    setCondition("");
                    setPage(1);
                  }}
                >
                  Xóa bộ lọc
                </button>
              </>
            ) : (
              "Chưa có tài sản trong phạm vi đã chọn."
            )}
          </Empty>
        )}
        <Pager
          page={currentPage}
          total={rows.length}
          pageSize={25}
          onPage={setPage}
        />
        </>}
        {groupedByRoom && <AssetConditionBar rows={rows.filter(a => a.isActive).map(a => ({ condition: a.conditionCode, count: 1 }))}/>}
      </State>
      {editing && (
        <Catalog
          mode="supplies"
          resourceOverride="assets"
          dialogOnly
          initialEditing={editing}
          onFinish={(saved) => {
            setEditing(null);
            if (saved) bump((x) => x + 1);
          }}
        />
      )}
      {id && !editing && (
        <Dialog
          title={asset?.name || "Tài sản"}
          className={maintenance ? "room-drawer" : undefined}
          close={() => {
            if (!pending) setId(null);
          }}
        >
          <State {...detail} retry={() => bump((n) => n + 1)}>
            {asset && (
              <>
                <p>
                  {asset.assetCode} · {assetConditionLabel(asset.conditionCode)}{" "}
                  · {asset.conditionCode === "PENDING_INSPECTION" ? "Chưa đưa vào sử dụng" : asset.isActive ? "Đang sử dụng" : "Đã ngừng sử dụng"}
                </p>
                {asset.conditionCode === "PENDING_INSPECTION" && <p>Hồ sơ chuyển từ sổ vật tư, đang chờ kiểm kê thực tế. Chưa xác nhận vị trí và tình trạng; không phải tài sản được nhập thêm.</p>}
                <div className="toolbar">
                  {(maintenance || groupedByRoom) && (
                    <section>
                      <p>
                        Vị trí:{" "}
                        {detail.data.room
                          ? `Phòng ${detail.data.room}`
                          : detail.data.location || "Chưa ghi nhận"}
                      </p>
                      <h3>Lịch sử sửa chữa và sự cố</h3>
                      {detail.data.work?.map((w: Row) => (
                        <a
                          className="overview-task"
                          key={w.workId}
                          href={`#work?kind=${w.kind}&id=${w.workId}`}
                        >
                          <span>{w.title}{w.assetRelationship==="REPLACED"&&" · Đánh giá thiết bị cũ trước khi thay"}</span>
                          <Badge value={w.status} />
                        </a>
                      ))}
                      {!detail.data.work?.length && <Empty />}
                      <h3>Kế hoạch bảo trì</h3>
                      {readScope !== undefined &&
                        can("REPORT_VIEW") &&
                        can("MAINTENANCE_VIEW") && (
                          <a
                            className="button-link"
                            href="#technical?tab=preventive"
                          >
                            Xem báo cáo bảo trì định kỳ
                          </a>
                        )}
                      {detail.data.plans?.map((p: Row) => (
                        <a
                          className="overview-task"
                          key={p.workTemplateId}
                          href={`#templates?id=${p.workTemplateId}`}
                        >
                          {p.title}
                        </a>
                      ))}
                    </section>
                  )}
                  {asset.isActive && can("ASSET_MANAGE", false) && (
                    <button onClick={() => setEditing(asset)}>
                      Chỉnh sửa thông tin
                    </button>
                  )}
                  {asset.isActive &&
                    [
                      ["move", "Di chuyển", "ASSET_TRANSFER"],
                      ["condition", "Cập nhật tình trạng", "ASSET_MANAGE"],
                      ["retire", "Ngừng sử dụng", "ASSET_RETIRE"],
                    ]
                      .filter((x) => can(x[2]))
                      .map(([a, n]) => (
                        <button key={a} onClick={() => setCommand(a)}>
                          {n}
                        </button>
                      ))}
                </div>
                {canReceiveHandover && <div className="toolbar">
                  <button className="primary" onClick={() => setCommand("accept")}>Xác nhận nhận bàn giao</button>
                  <button className="danger" onClick={() => setCommand("reject")}>Từ chối bàn giao</button>
                </div>}
                {command && (
                  <Dialog title="Xác nhận thao tác tài sản" className="confirmation-popup" close={()=>{if(!pending)setCommand("");}}>
                  <form
                    onSubmit={async (e) => {
                      e.preventDefault();
                      if (pending) return;
                      setPending(true);
                      setError("");
                      const f = new FormData(e.currentTarget);
                      try {
                        await api(
                          `assets/${id}/actions/${command}`,
                          action(asset.rowVersion, {
                            reason: f.get("reason"),
                            roomId: f.get("roomId")
                              ? Number(f.get("roomId"))
                              : null,
                            condition: f.get("condition") || null,
                          }),
                        );
                        setCommand("");
                        bump((x) => x + 1);
                      } catch (e) {
                        setError(message(e));
                        bump((x) => x + 1);
                      } finally {
                        setPending(false);
                      }
                    }}
                  >
                    {command === "move" && (
                      <label>
                        Phòng nhận
                        <select name="roomId" required defaultValue="">
                          <option value="">Chọn phòng</option>
                          {options.data?.rooms.map((r: Row) => (
                            <option key={r.roomId} value={r.roomId}>
                              {r.code}
                            </option>
                          ))}
                        </select>
                      </label>
                    )}
                    {command === "condition" && (
                      <label>
                        Tình trạng
                        <select name="condition">
                          {[
                            ["GOOD", "Tốt"],
                            ["FAIR", "Trung bình"],
                            ["POOR", "Kém"],
                            ["FAULTY", "Hỏng"],
                            ["UNDER_REPAIR", "Đang sửa"],
                            ["LOST", "Thất lạc"],
                          ].map(([v, n]) => (
                            <option key={v} value={v}>
                              {n}
                            </option>
                          ))}
                        </select>
                      </label>
                    )}
                    <label>
                      Lý do
                      <textarea
                        name="reason"
                        required
                        minLength={10}
                        maxLength={2000}
                      />
                    </label>
                    <ConfirmationActions pending={pending} cancel={()=>setCommand("")}/>
                    {error&&<AlertPopup>{error}</AlertPopup>}
                  </form></Dialog>
                )}
                {error && !command && (
                  <AlertPopup>
                    {error}
                  </AlertPopup>
                )}
                <h3>Lịch sử tài sản</h3>
                <History
                  rows={fields(detail.data?.history).map((event) => ({
                    ...event,
                    ...(event.oldCondition
                      ? {
                          oldCondition: assetConditionLabel(event.oldCondition),
                        }
                      : {}),
                    ...(event.newCondition
                      ? {
                          newCondition: assetConditionLabel(event.newCondition),
                        }
                      : {}),
                  }))}
                />
              </>
            )}
          </State>
        </Dialog>
      )}
    </>
  );
}
