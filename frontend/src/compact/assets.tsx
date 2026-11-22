import PageTitle from "./page-title";
import PageToolbar from "./page-toolbar";
import Catalog from "./catalog";
import { useCatalogRows } from "./use-catalog-rows";
import { useState } from "react";
import { action, api, fields, message, type Row } from "./api";
import { useAuth } from "./auth";
import { Dialog, History, State, useQuery, Pager, Empty, Badge } from "./ui";
import { SlidersHorizontal } from "lucide-react";
export default function Assets({
  maintenance = false,
  readScope,
}: {
  maintenance?: boolean;
  readScope?: number;
}) {
  const { branch, can } = useAuth();
  const [editing, setEditing] = useState<Row | null>(null),
    [search, setSearch] = useState("");
  const [page, setPage] = useState(1),
    [revision, bump] = useState(0),
    [id, setId] = useState<number | null>(
      readScope !== undefined
        ? Number(new URLSearchParams(location.hash.split("?")[1]).get("id")) ||
            null
        : null,
    ),
    [command, setCommand] = useState(""),
    [error, setError] = useState(""),
    [pending, setPending] = useState(false);
  const catalog = useCatalogRows(
    readScope === undefined ? "assets" : null,
    branch,
    revision,
  );
  const monitoring = useQuery<Row[]>(
    readScope !== undefined
      ? `insights/owner/assets?${readScope ? `branchId=${readScope}` : ""}`
      : null,
    revision,
  );
  const query = readScope !== undefined ? monitoring : catalog,
    detail = useQuery(id ? `assets/${id}` : null, revision),
    options = useQuery(
      can("ROOM_VIEW") ? `workspace/options?branchId=${branch}` : null,
    );
  const asset = detail.data?.asset?.fields;
  const [filters, setFilters] = useState(false),
    [condition, setCondition] = useState(""),
    [sort, setSort] = useState("name");
  const rows = (query.data || [])
    .filter((a) => !condition || a.conditionCode === condition)
    .filter((a) =>
      `${a.name} ${a.assetCode} ${a.serialNumber || ""}`
        .toLocaleLowerCase()
        .includes(search.trim().toLocaleLowerCase()),
    )
    .sort((a, b) =>
      String(a[sort] || "").localeCompare(String(b[sort] || ""), "vi"),
    );
  const currentPage = Math.min(page, Math.max(1, Math.ceil(rows.length / 25)));
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
            placeholder="Tên, mã tài sản hoặc số sê-ri…"
            onChange={(e) => {
              setSearch(e.target.value);
              setPage(1);
            }}
          />
        </label>
        {maintenance && (
          <button aria-expanded={filters} onClick={() => setFilters(!filters)}>
            <SlidersHorizontal size={16} /> Bộ lọc
          </button>
        )}
      </PageToolbar>
      {maintenance && filters && (
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
              {["GOOD", "FAIR", "POOR", "FAULTY", "UNDER_REPAIR", "LOST"].map(
                (s) => (
                  <option key={s}>{s}</option>
                ),
              )}
            </select>
          </label>
          <label>
            Sắp xếp
            <select value={sort} onChange={(e) => setSort(e.target.value)}>
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
        <div className="table-scroll">
          <table>
            <thead>
              <tr>
                <th>Tài sản</th>
                {readScope !== undefined && <th>Chi nhánh</th>}
                <th>Mã</th>
                <th>Tình trạng</th>
                <th>Thao tác</th>
              </tr>
            </thead>
            <tbody>
              {rows.slice((currentPage - 1) * 25, currentPage * 25).map((a) => (
                <tr key={a.assetId}>
                  <td>{a.name}</td>
                  {readScope !== undefined && <td>{a.branchName}</td>}
                  <td>{a.assetCode}</td>
                  <td>{a.conditionCode}</td>
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
              ))}
            </tbody>
          </table>
        </div>
        {!rows.length && <Empty />}
        <Pager
          page={currentPage}
          total={rows.length}
          pageSize={25}
          onPage={setPage}
        />
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
                  {asset.assetCode} · {asset.conditionCode} ·{" "}
                  {asset.isActive ? "Đang sử dụng" : "Đã ngừng sử dụng"}
                </p>
                <div className="toolbar">
                  {maintenance && (
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
                          <span>{w.title}</span>
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
                {command && (
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
                <h3>Lịch sử tài sản</h3>
                <History rows={fields(detail.data?.events)} />
              </>
            )}
          </State>
        </Dialog>
      )}
    </>
  );
}
