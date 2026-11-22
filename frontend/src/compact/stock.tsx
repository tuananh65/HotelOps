import PageTitle from "./page-title";
import PageToolbar from "./page-toolbar";
import { useState } from "react";
import { useAuth } from "./auth";
import { Dialog, Empty, Pager, State, useQuery } from "./ui";
import { SlidersHorizontal } from "lucide-react";
import { timestamp, type Row } from "./api";
import { documentHref, documentNames } from "./inventory-document-model";
export default function Stock({ readScope }: { readScope?: number }) {
  const { branch, profile } = useAuth();
  const storekeeper = profile.roles
    .filter((r) => r.branchId == null || r.branchId === branch)
    .every((r) => r.code === "STOREKEEPER");
  const [page, setPage] = useState(1);
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
          (!low || (r.minimumQty != null && r.baseQty < r.minimumQty)),
      )
      .sort((a, b) =>
        sort === "quantity"
          ? a.baseQty - b.baseQty
          : String(a.item).localeCompare(String(b.item), "vi"),
      ) || [];
  return (
    <>
      <div className="page-heading">
        <PageTitle>
          <h1>Tồn kho vật tư</h1>
          <p>Số lượng theo đơn vị cơ sở, tính từ chứng từ đã ghi sổ</p>
        </PageTitle>
      </div>
      <PageToolbar>
        <label className="search">
          Tìm vật tư hoặc kho
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
          <label>
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
          <table>
            <thead>
              <tr>
                <th>Kho</th>
                {readScope !== undefined && <th>Chi nhánh</th>}
                <th>Mã vật tư</th>
                <th>Vật tư</th>
                <th className="numeric">Tồn cơ sở</th>
                <th>Đơn vị</th>
                <th>Tối thiểu</th>
                <th>Biến động cuối</th>
                <th>Thao tác</th>
              </tr>
            </thead>
            <tbody>
              {rows.slice((page - 1) * 25, page * 25).map((r) => (
                <tr key={`${r.warehouseId}-${r.itemId}`}>
                  <td>{r.warehouse}</td>
                  {readScope !== undefined && (
                    <td>
                      {profile.branches.find((b) => b.branchId === r.branchId)
                        ?.name || "—"}
                    </td>
                  )}
                  <td>{r.code}</td>
                  <td>{r.item}</td>
                  <td className="numeric">
                    {Number(r.baseQty || 0).toLocaleString("vi-VN")}
                  </td>
                  <td>{r.unit || "—"}</td>
                  <td>{r.minimumQty ?? "—"}</td>
                  <td>{r.lastMovement ? timestamp(r.lastMovement) : "—"}</td>
                  <td>
                    <button
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
              <table>
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
                      <td>
                        <a
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
                      <td>
                        {r.signedBaseQty > 0 ? "+" : ""}
                        {r.signedBaseQty}
                        {r.reversalOfMovementId && (
                          <small>Đảo giao dịch #{r.reversalOfMovementId}</small>
                        )}
                      </td>
                      <td>
                        {timestamp(r.occurredAt)}
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
