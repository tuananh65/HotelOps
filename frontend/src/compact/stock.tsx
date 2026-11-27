import PageTitle from "./page-title";
import InventoryLifecycle from "./inventory-lifecycle";
import ReusableInventory from "./reusable-inventory";
import CrossBranchStock from "./cross-branch-stock";
import PageToolbar from "./page-toolbar";
import { useState } from "react";
import { useAuth } from "./auth";
import { Dialog, Empty, Pager, State, useQuery } from "./ui";
import { SlidersHorizontal } from "lucide-react";
import { timestamp, type Row } from "./api";
import { documentHref, documentNames } from "./inventory-document-model";
import { api } from "./api";
import { AlertPopup } from "./alert-popup";
import { supplyMessage } from "./supply-api";
import "./stock.css";
export default function Stock({ readScope }: { readScope?: number }) {
  const { branch, profile, can } = useAuth();
  const [switching, setSwitching] = useState(false);
  const [editing, setEditing] = useState<Row | null>(null);
  const warehouses = useQuery(readScope === undefined ? `catalog/warehouses?branchId=${branch}&pageSize=100` : null);
  const currentWarehouses = warehouses.data?.items.map((r: Row) => r.fields).filter((r: Row) => r.isActive) || [];
  const mayEdit = readScope === undefined && can("ROOM_SUPPLY_CONFIG", false) && profile.roles.some(r => r.code === "OPERATION_MANAGER" && (r.branchId == null || r.branchId === branch));
  const canCrossBranch = readScope === undefined && can("TRANSFER_REQUEST_CREATE", false) && profile.roles.some(r => r.code === "OPERATION_MANAGER" && (r.branchId == null || r.branchId === branch));
  const storekeeper = profile.roles
    .filter((r) => r.branchId == null || r.branchId === branch)
    .every((r) => r.code === "STOREKEEPER");
  const [page, setPage] = useState(1);
  const [batchView, setBatchView] = useState(false);
  const [reusableView, setReusableView] = useState(false);
  const [revision, bump] = useState(0),
    [search, setSearch] = useState(
      new URLSearchParams(location.hash.split("?")[1]).get("search") || "",
    );
  const [filters, setFilters] = useState(false),
    [warehouse, setWarehouse] = useState(""),
    [low, setLow] = useState(false),
    [sort, setSort] = useState("name");
  const [selected, setSelected] = useState<Row | null>(null),
    [ledgerPage, setLedgerPage] = useState(1);
  const ledger = useQuery(
    selected
      ? `insights/ledger?branchId=${selected.branchId ?? branch}&warehouseId=${selected.warehouseId}&itemId=${selected.itemId}&page=${ledgerPage}`
      : null,
  );
  const query = useQuery<Row[]>(
    readScope !== undefined
      ? `insights/owner/stock?${readScope ? `branchId=${readScope}` : ""}`
      : `insights/stock?branchId=${branch}`,
    revision,
  );
  const rows =
    query.data
      ?.filter((r) =>
        `${r.item} ${r.code} ${r.warehouse}`
          .toLocaleLowerCase()
          .includes(search.toLocaleLowerCase()),
      )
      .filter(
        (r) =>
          (!warehouse || r.warehouseId === Number(warehouse)) &&
          (readScope !== undefined || currentWarehouses.some((w: Row) => w.warehouseId === r.warehouseId)) &&
          (!low || (r.minimumQty != null && (r.availableQty ?? r.baseQty) < r.minimumQty)),
      )
      .sort((a, b) =>
        sort === "quantity"
          ? (a.availableQty ?? a.baseQty) - (b.availableQty ?? b.baseQty)
          : String(a.item).localeCompare(String(b.item), "vi"),
      ) || [];
  if (switching && canCrossBranch) return <><button onClick={() => setSwitching(false)}>Quay lại kho chi nhánh quản lý</button><CrossBranchStock branch={branch} /></>;
  if (batchView) return <><button onClick={() => setBatchView(false)}>Quay lại tồn kho</button><InventoryLifecycle branch={branch}/></>;
  if (reusableView) return <><button onClick={() => setReusableView(false)}>Quay lại tồn kho</button><ReusableInventory branch={branch}/></>;
  return (
    <>
      <div className="page-heading">
        {readScope === undefined && <button onClick={() => setBatchView(true)}>Lô và hạn sử dụng</button>}
        {readScope === undefined && <button onClick={() => setReusableView(true)}>Vật tư tái sử dụng</button>}
        <PageTitle>
          <h1>Tồn kho vật tư</h1>
          <p>Tồn cơ sở tính từ chứng từ đã ghi sổ. Cảnh báo dưới tối thiểu dựa trên số có thể cấp.</p>
        </PageTitle>
      </div>
      <div className="stock-context">
        <div><small>Kho đang xem</small><strong>{readScope !== undefined ? (warehouse ? query.data?.find(r => r.warehouseId === Number(warehouse))?.warehouse : "Các kho trong phạm vi đã chọn") : currentWarehouses.length ? currentWarehouses.map((w: Row) => w.name).join(" · ") : warehouses.loading ? "Đang tải kho…" : "Chưa có kho hoạt động"}</strong></div>
        {canCrossBranch && <button onClick={() => setSwitching(true)}>Xem kho chi nhánh khác</button>}
      </div>
      {!!warehouses.error && <State {...warehouses}>{null}</State>}
      <PageToolbar>
        <label className="search">
          Tìm vật tư
          <input
            value={search}
            onChange={(e) => {
              setSearch(e.target.value);
              setPage(1);
            }}
          />
        </label>
        <button aria-expanded={filters} onClick={() => setFilters(!filters)}>
          <SlidersHorizontal size={16} /> Bộ lọc
        </button>
      </PageToolbar>
      {filters && (
        <PageToolbar>
          {readScope !== undefined && <label>
            Kho
            <select
              value={warehouse}
              onChange={(e) => {
                setWarehouse(e.target.value);
                setPage(1);
              }}
            >
              <option value="">Tất cả kho</option>
              {Array.from(
                new Map(
                  query.data?.map((r) => [r.warehouseId, r.warehouse]),
                ).entries(),
              ).map(([id, name]) => (
                <option key={id} value={id}>
                  {name}
                </option>
              ))}
            </select>
          </label>}
          <label>
            Sắp xếp
            <select
              value={sort}
              onChange={(e) => {
                setSort(e.target.value);
                setPage(1);
              }}
            >
              <option value="name">Tên vật tư</option>
              <option value="quantity">Tồn tăng dần</option>
            </select>
          </label>
          <label>
            <input
              type="checkbox"
              checked={low}
              onChange={(e) => {
                setLow(e.target.checked);
                setPage(1);
              }}
            />{" "}
            Dưới định mức
          </label>
          <button
            onClick={() => {
              setWarehouse("");
              setLow(false);
              setSort("name");
              setSearch("");
              setPage(1);
            }}
          >
            Đặt lại
          </button>
        </PageToolbar>
      )}
      <State {...query} retry={() => bump((n) => n + 1)}>
        <div className="table-scroll">
          <table className="mobile-records">
            <thead>
              <tr>
                <th>STT</th>
                {readScope !== undefined && <th>Chi nhánh</th>}
                <th>Vật tư</th>
                <th className="numeric">Tồn cơ sở</th>
                <th>Đơn vị</th>
                <th>Tối thiểu</th>
                <th>Thao tác</th>
              </tr>
            </thead>
            <tbody>
              {rows.slice((page - 1) * 25, page * 25).map((r, index) => (
                <tr key={`${r.warehouseId}-${r.itemId}`} className={r.minimumQty != null && (r.availableQty ?? r.baseQty) < r.minimumQty ? "stock-below-minimum" : undefined}>
                  <td data-label="STT">{(page - 1) * 25 + index + 1}</td>
                  {readScope !== undefined && (
                    <td data-label="Chi nhánh">{profile.branches.find((b) => b.branchId === r.branchId)
                        ?.name || "—"}
                    </td>
                  )}
                  <td data-label="Vật tư">{r.item}</td>
                  <td className="numeric" data-label="Tồn cơ sở">
                    {Number(r.baseQty || 0).toLocaleString("vi-VN")}
                    {r.availableQty != null && <small>Có thể cấp: {Number(r.availableQty).toLocaleString("vi-VN")}</small>}
                    {Number(r.baseQty) > Number(r.availableQty ?? r.baseQty) && <small>{r.classification === "UNCLASSIFIED"
                      ? "Chưa phân loại vật tư; chưa đủ điều kiện cấp."
                      : r.classification === "CONSUMABLE" && r.expiryPolicy === "UNKNOWN"
                        ? "Chưa xác định chính sách hạn dùng."
                        : r.classification === "CONSUMABLE" && Number(r.batchQuantity) < Number(r.baseQty)
                          ? "Tồn sổ sách chưa được đối soát đủ theo lô."
                          : "Một phần tồn chưa đủ điều kiện cấp hoặc đã được giữ chỗ."}</small>}
                    {r.minimumQty != null && (r.availableQty ?? r.baseQty) < r.minimumQty && <small>Tồn khả dụng dưới tối thiểu</small>}
                  </td>
                  <td data-label="Đơn vị">{r.unit || "—"}</td>
                  <td data-label="Tối thiểu">{mayEdit && r.isActive !== false ? <button aria-label={`Sửa tối thiểu ${r.item}`} onClick={() => setEditing(r)}>{r.minimumQty ?? 0} · Sửa</button> : r.minimumQty ?? "—"}</td>
                  <td data-label="Thao tác"><button
                      onClick={() => {
                        setSelected(r);
                        setLedgerPage(1);
                      }}
                    >
                      Biến động
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        {!rows.length && <Empty>Chưa có tồn kho phù hợp với bộ lọc.</Empty>}
        <Pager page={page} total={rows.length} pageSize={25} onPage={setPage} />
      </State>
      {editing && <MinimumEditor row={editing} branch={branch} close={() => setEditing(null)} saved={() => { setEditing(null); bump(n => n + 1); }} />}
      {selected && (
        <Dialog
          className="room-drawer"
          title={`${selected.code} · ${selected.item}`}
          close={() => setSelected(null)}
        >
          <p>
            {selected.warehouse} · Tồn: {selected.baseQty} {selected.unit}
          </p>
          <State {...ledger}>
            <div className="table-scroll">
              <table className="mobile-records">
                <thead>
                  <tr>
                    <th>Chứng từ</th>
                    <th>Biến động ({selected.unit})</th>
                    <th>Thời gian / người ghi</th>
                  </tr>
                </thead>
                <tbody>
                  {ledger.data?.items.map((r: Row) => (
                    <tr key={r.movementId}>
                      <td data-label="Chứng từ"><a
                          href={
                            storekeeper
                              ? documentHref(
                                  r as { kind: string; documentId: number },
                                )
                              : `#documents?kind=${r.kind}&id=${r.documentId}`
                          }
                        >
                          {documentNames[r.kind]}
                          <small>{r.documentNo}</small>
                        </a>
                      </td>
                      <td data-label="Biến động">{r.signedBaseQty > 0 ? "+" : ""}
                        {r.signedBaseQty}
                        {r.reversalOfMovementId && (
                          <small>Đảo giao dịch #{r.reversalOfMovementId}</small>
                        )}
                      </td>
                      <td data-label="Thời gian / người ghi">{timestamp(r.occurredAt)}
                        <small>{r.actor}</small>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            {!ledger.data?.total && <Empty />}
            <Pager
              page={ledgerPage}
              total={ledger.data?.total || 0}
              pageSize={25}
              onPage={setLedgerPage}
            />
          </State>
        </Dialog>
      )}
    </>
  );
}

function MinimumEditor({row, branch, close, saved}: {row: Row; branch: number; close: () => void; saved: () => void}) {
  const [revision, bump] = useState(0);
  const query = useQuery(`supplies/minimum?branchId=${branch}&warehouseId=${row.warehouseId}&itemId=${row.itemId}`, revision);
  const [error, setError] = useState("");
  const [pending, setPending] = useState(false);
  return <Dialog title={`Tồn tối thiểu · ${row.item}`} close={pending ? () => {} : close}>
    <p>{row.warehouse} · Đơn vị: {row.unit}</p>
    <State {...query} retry={() => bump(n => n + 1)}>{query.data && <form key={revision} onSubmit={async e => {
      e.preventDefault(); if(pending) return;
      const minimumStock = Number(new FormData(e.currentTarget).get("minimumStock"));
      setPending(true); setError("");
      try { await api("supplies/minimum", {...query.data, branchId: branch, warehouseId: row.warehouseId, itemId: row.itemId, minimumStock}, "PUT"); saved(); }
      catch(e) { setError(supplyMessage(e)); } finally { setPending(false); }
    }}>
      <label>Tồn tối thiểu<input name="minimumStock" type="number" min="0" max="99999999999999.9999" step="0.0001" required defaultValue={query.data.minimumStock} /></label>
      {error && <AlertPopup>{error}</AlertPopup>}
      <div className="actions"><button type="submit" className="primary" disabled={pending}>{pending ? "Đang lưu…" : "Lưu tối thiểu"}</button><button type="button" disabled={pending} onClick={close}>Hủy</button></div>
    </form>}</State>
  </Dialog>;
}
