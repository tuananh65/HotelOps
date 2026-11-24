import Transfers from "./transfers";
import { AlertPopup } from "./alert-popup";
import PageTitle from "./page-title";
import PageToolbar from "./page-toolbar";
import { useState } from "react";
import { SlidersHorizontal } from "lucide-react";
import {
  api,
  action,
  fields,
  message,
  type Row,
  type Page,
  timestamp,
} from "./api";
import { useAuth } from "./auth";
import { Badge, ConfirmationActions, Dialog, Empty, History, Pager, State, useQuery } from "./ui";
const types = [
  ["MATERIAL_REQUEST", "Yêu cầu vật tư", "MR_VIEW", "MR_CREATE"],
  [
    "REPLENISHMENT",
    "Bổ sung hàng",
    "REPLENISHMENT_VIEW",
    "REPLENISHMENT_CREATE",
  ],
  ["RECEIPT", "Nhập kho", "INVENTORY_VIEW", "STOCK_RECEIPT_POST"],
  ["ISSUE", "Xuất kho", "INVENTORY_VIEW", "STOCK_ISSUE_POST"],
  ["RETURN", "Hoàn kho", "INVENTORY_VIEW", "STOCK_RETURN_POST"],
  ["TRANSFER", "Chuyển kho", "INVENTORY_VIEW", "STOCK_TRANSFER_POST"],
  [
    "TRANSFER_REQUEST",
    "Đề nghị chuyển",
    "INVENTORY_VIEW",
    "STOCK_TRANSFER_POST",
  ],
  [
    "TRANSFER_RECEIPT",
    "Nhận chuyển kho",
    "INVENTORY_VIEW",
    "STOCK_TRANSFER_POST",
  ],
  ["STOCKTAKE", "Kiểm kê", "INVENTORY_VIEW", "STOCKTAKE_PERFORM"],
  ["ADJUSTMENT", "Điều chỉnh", "INVENTORY_VIEW", "STOCK_ADJUST_POST"],
  ["REVERSAL", "Đảo chứng từ", "INVENTORY_VIEW", "STOCK_REVERSE"],
];
export default function Documents(props: {materialOnly?:boolean;readScope?:number;historyTitle?:boolean}) {
  const transfer=new URLSearchParams(location.hash.split("?")[1]).get("transfer")==="1";
  return transfer && props.readScope===undefined && !props.materialOnly ? <Transfers/> : <DocumentList {...props}/>;
}
function DocumentList({
  materialOnly = false,
  readScope,
  historyTitle = false,
}: {
  materialOnly?: boolean;
  readScope?: number;
  historyTitle?: boolean;
}) {
  const { branch, can, profile } = useAuth(),
    visible = types.filter(
      (t) => can(t[2]) && (!materialOnly || t[0] === "MATERIAL_REQUEST"),
    );
  const [kind, setKind] = useState(
      visible.find(
        (t) =>
          t[0] === new URLSearchParams(location.hash.split("?")[1]).get("kind"),
      )?.[0] ||
        (readScope !== undefined && !materialOnly
          ? "RECEIPT"
          : visible[0]?.[0]) ||
        "RECEIPT",
    ),
    [revision, bump] = useState(0),
    [page, setPage] = useState(1),
    [id, setId] = useState<number | null>(
      Number(new URLSearchParams(location.hash.split("?")[1]).get("id")) ||
        null,
    ),
    [creating, setCreating] = useState(
      new URLSearchParams(location.hash.split("?")[1]).get("create") === "1",
    ),
    [adding, setAdding] = useState(false),
    [command, setCommand] = useState(""),
    [error, setError] = useState(""),
    [pending, setPending] = useState(false);
  const [search, setSearch] = useState(""),
    [pendingOnly, setPendingOnly] = useState(false),
    [filters, setFilters] = useState(false);
  const [sort, setSort] = useState("latest");
  const [createKey, setCreateKey] = useState(() => crypto.randomUUID());
  const query = useQuery<Page>(
      `documents?${readScope === 0 ? "" : `branchId=${readScope ?? branch}&`}kind=${kind}&page=${page}${historyTitle || materialOnly || readScope !== undefined ? `&search=${encodeURIComponent(search)}&sort=${sort}${pendingOnly ? "&status=PENDING_APPROVAL" : ""}` : ""}`,
      revision,
    ),
    options = useQuery(`workspace/options?branchId=${branch}`),
    detail = useQuery(id ? `documents/${id}` : null, revision);
  const doc = detail.data?.document.fields,
    write = can(visible.find((t) => t[0] === kind)?.[3] || "");
  const maintenanceSources = useQuery<Page>(
    creating && kind === "MATERIAL_REQUEST" && can("MAINTENANCE_VIEW")
      ? `work?branchId=${branch}&kind=MAINTENANCE&assignedToMe=true&pageSize=100`
      : null,
  );
  async function mutate(path: string, body: Row) {
    if (pending) return;
    setPending(true);
    setError("");
    try {
      const r = await api(path, body);
      if (path === "documents") setId(r.fields.documentId);
      setCreating(false);
      setAdding(false);
      setCommand("");
      bump((x) => x + 1);
    } catch (e) {
      setError(message(e));
      bump((x) => x + 1);
    } finally {
      setPending(false);
    }
  }
  const actions: [string, string][] = [];
  const ownsDocument =
    !doc ||
    kind !== "MATERIAL_REQUEST" ||
    doc.createdBy === profile.user.fields.userId;
  if (doc) {
    if (
      write &&
      ownsDocument &&
      ["DRAFT", "NEEDS_INFORMATION"].includes(doc.status) &&
      ["MATERIAL_REQUEST", "TRANSFER_REQUEST"].includes(kind)
    )
      actions.push(["submit", "Gửi duyệt"]);
    if (
      doc.createdBy !== profile.user.fields.userId &&
      ((kind === "MATERIAL_REQUEST" && detail.data?.canApprove === true) ||
        (kind !== "MATERIAL_REQUEST" && write)) &&
      ["PENDING_APPROVAL", "DRAFT"].includes(doc.status) &&
      [
        "MATERIAL_REQUEST",
        "TRANSFER_REQUEST",
        "RETURN",
        "ADJUSTMENT",
        "REVERSAL",
      ].includes(kind)
    )
      actions.push(["approve", "Duyệt"]);
    if (
      doc.status === "PENDING_APPROVAL" &&
      (kind === "MATERIAL_REQUEST"
        ? detail.data?.canApprove === true
        : can(visible.find((t) => t[0] === kind)?.[3] || ""))
    )
      actions.push(["reject", "Từ chối"]);
    if (
      write &&
      ownsDocument &&
      !doc.postedAt &&
      ["DRAFT", "APPROVED", "COUNTED"].includes(doc.status) &&
      ![
        "MATERIAL_REQUEST",
        "REPLENISHMENT",
        "TRANSFER_REQUEST",
        "STOCKTAKE",
      ].includes(kind)
    )
      actions.push(["post", "Ghi sổ kho"]);
    if (write && kind === "STOCKTAKE" && doc.status === "IN_PROGRESS")
      actions.push(["count", "Chốt kiểm kê"]);
    if (
      write &&
      ownsDocument &&
      !doc.postedAt &&
      [
        "DRAFT",
        "IN_PROGRESS",
        "PENDING_APPROVAL",
        "NEEDS_INFORMATION",
      ].includes(doc.status)
    )
      actions.push(["cancel", "Hủy"]);
    if (
      ["MATERIAL_REQUEST", "REPLENISHMENT"].includes(kind) &&
      (kind === "MATERIAL_REQUEST"
        ? detail.data?.canApprove === true
        : can("REPLENISHMENT_CLOSE")) &&
      !["CLOSED", "REMAINDER_CLOSED", "CANCELLED"].includes(doc.status)
    )
      actions.push(["close", "Đóng phần còn lại"]);
  }
  return (
    <>
      <div className="page-heading">
        <PageTitle>
          <h1>{historyTitle ? "Lịch sử yêu cầu vật tư" : materialOnly ? "Yêu cầu vật tư" : "Vật tư & chứng từ"}</h1>
          <p>Duyệt yêu cầu và ghi sổ kho là hai bước riêng biệt</p>
        </PageTitle>
      </div>
      {!materialOnly && readScope===undefined && can("INVENTORY_VIEW",false) && <p><a className="button-link" href="#documents?transfer=1">Điều chuyển vật tư giữa chi nhánh</a></p>}
      <PageToolbar
        actions={
          write && (
            <button
              className="primary"
              onClick={() => {
                setCreating(true);
                setCreateKey(crypto.randomUUID());
                setError("");
              }}
            >
              + Tạo chứng từ
            </button>
          )
        }
      >
        {readScope !== undefined && !materialOnly && (
          <label>
            Loại chứng từ
            <select
              value={kind}
              onChange={(e) => {
                setKind(e.target.value);
                setPage(1);
                setId(null);
                setPendingOnly(false);
              }}
            >
              {visible.map(([v, n]) => (
                <option key={v} value={v}>
                  {n}
                </option>
              ))}
            </select>
          </label>
        )}
        {historyTitle || materialOnly || readScope !== undefined ? (
          <>
            <label className="search">
              Tìm yêu cầu vật tư
              <input
                value={search}
                placeholder="Mã hoặc lý do yêu cầu"
                onChange={(e) => {
                  setSearch(e.target.value);
                  setPage(1);
                }}
              />
            </label>
            <button
              aria-expanded={filters}
              onClick={() => setFilters(!filters)}
            >
              <SlidersHorizontal size={18} /> Bộ lọc{pendingOnly ? " •" : ""}
            </button>
          </>
        ) : (
          <div className="tabs">
            {visible.map((t) => (
              <button
                key={t[0]}
                aria-pressed={kind === t[0]}
                onClick={() => {
                  setKind(t[0]);
                  setPage(1);
                  setId(null);
                }}
              >
                {t[1]}
              </button>
            ))}
          </div>
        )}
      </PageToolbar>
      {(historyTitle || materialOnly || readScope !== undefined) && filters && (
        <PageToolbar>
          <label>
            Trạng thái
            <select
              value={pendingOnly ? "pending" : "all"}
              onChange={(e) => {
                setPendingOnly(e.target.value === "pending");
                setPage(1);
              }}
            >
              <option value="all">Tất cả yêu cầu</option>
              <option value="pending">Chờ phê duyệt</option>
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
              <option value="latest">Mới nhất</option>
              <option value="oldest">Cũ nhất</option>
            </select>
          </label>
          <button
            onClick={() => {
              setPendingOnly(false);
              setSort("latest");
              setSearch("");
              setPage(1);
            }}
          >
            Đặt lại
          </button>
        </PageToolbar>
      )}
      <State {...query} retry={() => bump((x) => x + 1)}>
        <div className="table-scroll">
          <table>
            <thead>
              <tr>
                <th>Chứng từ</th>
                {readScope !== undefined && <th>Chi nhánh</th>}
                <th>Trạng thái</th>
                <th>Ngày tạo</th>
                <th>Thao tác</th>
              </tr>
            </thead>
            <tbody>
              {fields(query.data?.items).map((d) => (
                <tr key={d.documentId}>
                  <td>
                    <strong>#{d.documentId}</strong>
                    <small>
                      {d.supplierName ||
                        d.reason ||
                        visible.find((t) => t[0] === kind)?.[1]}
                    </small>
                  </td>
                  {readScope !== undefined && (
                    <td>
                      {profile.branches.find((b) => b.branchId === d.branchId)
                        ?.name || "—"}
                    </td>
                  )}
                  <td>
                    <Badge value={d.status} />
                  </td>
                  <td>{timestamp(d.createdAt)}</td>
                  <td>
                    <button
                      onClick={() => {
                        setId(d.documentId);
                        setCommand("");
                        setAdding(false);
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
        {!query.data?.total && <Empty />}
        <Pager page={page} total={query.data?.total || 0} onPage={setPage} />
      </State>
      {creating && write && (
        <Dialog
          title="Tạo chứng từ"
          close={() => {
            if (!pending) setCreating(false);
          }}
        >
          <State {...options}>
            <form
              onSubmit={(e) => {
                e.preventDefault();
                const f = new FormData(e.currentTarget);
                const num = (k: string) => (f.get(k) ? Number(f.get(k)) : null);
                mutate("documents", {
                  branchId: branch,
                  operationKey: createKey,
                  kind,
                  departmentId: num("departmentId"),
                  sourceWarehouseId: num("sourceWarehouseId"),
                  destinationWarehouseId: num("destinationWarehouseId"),
                  sourceDocumentId: num("sourceDocumentId"),
                  sourceWorkId: num("sourceWorkId"),
                  supplierName: f.get("supplierName") || null,
                  reason: f.get("reason") || null,
                  expectedOn: f.get("expectedOn") || null,
                });
              }}
            >
              <label>
                Bộ phận
                <select
                  name="departmentId"
                  required={kind === "MATERIAL_REQUEST"}
                  defaultValue=""
                >
                  <option value="">Chọn bộ phận</option>
                  {options.data?.departments
                    .filter(
                      (d: Row) =>
                        kind !== "MATERIAL_REQUEST" ||
                        profile.memberships.some(
                          (m) =>
                            m.branchId === branch &&
                            m.departmentId === d.departmentId &&
                            m.kind === "EMPLOYMENT",
                        ),
                    )
                    .map((d: Row) => (
                      <option key={d.departmentId} value={d.departmentId}>
                        {d.name}
                      </option>
                    ))}
                </select>
              </label>
              {kind === "MATERIAL_REQUEST" && can("MAINTENANCE_VIEW") && (
                <State {...maintenanceSources}>
                  <label>
                    Công việc bảo trì liên quan
                    <select
                      name="sourceWorkId"
                      defaultValue={
                        new URLSearchParams(location.hash.split("?")[1]).get(
                          "sourceWorkId",
                        ) || ""
                      }
                    >
                      <option value="">Không liên kết công việc</option>
                      {fields(maintenanceSources.data?.items)
                        .filter(
                          (w) =>
                            !["CLOSED", "COMPLETED", "CANCELLED"].includes(
                              w.status,
                            ),
                        )
                        .map((w) => (
                          <option key={w.workId} value={w.workId}>
                            {w.title}
                          </option>
                        ))}
                    </select>
                  </label>
                </State>
              )}
              {[
                ["sourceWarehouseId", "Kho xuất / kiểm kê"],
                ["destinationWarehouseId", "Kho nhận"],
              ].map(([k, n]) => (
                <label key={k}>
                  {n}
                  <select name={k} defaultValue="">
                    <option value="">Không chọn</option>
                    {options.data?.warehouses.map((w: Row) => (
                      <option key={w.warehouseId} value={w.warehouseId}>
                        {w.name}
                      </option>
                    ))}
                  </select>
                </label>
              ))}
              <label>
                Mã chứng từ nguồn (nếu có)
                <input name="sourceDocumentId" type="number" min={1} />
              </label>
              <label>
                Nhà cung cấp
                <input name="supplierName" maxLength={200} />
              </label>
              <label>
                Ngày dự kiến
                <input name="expectedOn" type="date" />
              </label>
              <label>
                Lý do
                <textarea name="reason" minLength={10} maxLength={2000} />
              </label>
              <button className="primary" disabled={pending}>
                Tạo bản nháp
              </button>
            </form>
          </State>
          {error && (
            <AlertPopup>
              {error}
            </AlertPopup>
          )}
        </Dialog>
      )}
      {id && (
        <Dialog
          title={doc?.documentNo || `Chứng từ #${id}`}
          className={
            materialOnly || readScope !== undefined ? "room-drawer" : undefined
          }
          close={() => {
            if (!pending) setId(null);
          }}
        >
          <State {...detail}>
            {doc && (
              <>
                <div className="toolbar">
                  <Badge value={doc.status} />
                  {actions.map(([a, n]) => (
                    <button
                      key={a}
                      disabled={pending}
                      onClick={() => {
                        setAdding(false);
                        setCommand(a);
                        setError("");
                      }}
                    >
                      {n}
                    </button>
                  ))}
                </div>
                <p>{doc.reason}</p>
                <div className="table-scroll">
                  <table>
                    <thead>
                      <tr>
                        <th>Vật tư</th>
                        <th>Số lượng</th>
                        <th>Đã duyệt</th>
                        {materialOnly && (
                          <>
                            <th>Đã cấp (đơn vị gốc)</th>
                            <th>Còn thiếu (đơn vị gốc)</th>
                          </>
                        )}
                      </tr>
                    </thead>
                    <tbody>
                      {fields(detail.data?.lines).map((l) => (
                        <tr key={l.lineId}>
                          <td>
                            {options.data?.items.find(
                              (i: Row) => i.itemId === l.itemId,
                            )?.name || `Vật tư #${l.itemId}`}
                          </td>
                          <td>{l.actualQty}</td>
                          <td>{l.approvedQty ?? "—"}</td>
                          {materialOnly && (
                            <>
                              <td>
                                {fields(detail.data?.fulfillment).find(
                                  (f) => f.lineId === l.lineId,
                                )?.issuedBaseQty ?? "—"}
                              </td>
                              <td>
                                {fields(detail.data?.fulfillment).find(
                                  (f) => f.lineId === l.lineId,
                                )?.missingBaseQty ?? "—"}
                              </td>
                            </>
                          )}
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>
                {write &&
                  ownsDocument &&
                  [
                    "DRAFT",
                    "NEEDS_INFORMATION",
                    "REQUESTED",
                    "IN_PROGRESS",
                  ].includes(doc.status) && (
                    <button
                      disabled={pending}
                      onClick={() => {
                        setCommand("");
                        setAdding(!adding);
                      }}
                    >
                      + Thêm vật tư
                    </button>
                  )}
                {adding && (
                  <Dialog
                    title="Thêm vật tư"
                    close={() => {
                      if (!pending) setAdding(false);
                    }}
                  >
                    <form
                      onSubmit={(e) => {
                        e.preventDefault();
                        const f = new FormData(e.currentTarget);
                        const item = options.data?.items.find(
                          (i: Row) => i.itemId === Number(f.get("itemId")),
                        );
                        mutate(`documents/${id}/lines`, {
                          rowVersion: doc.rowVersion,
                          itemId: item.itemId,
                          unitId: item.baseUnitId,
                          quantity: Number(f.get("quantity")),
                          unitCost: f.get("unitCost")
                            ? Number(f.get("unitCost"))
                            : null,
                          countedQty: f.get("countedQty")
                            ? Number(f.get("countedQty"))
                            : null,
                          sourceLineId: f.get("sourceLineId")
                            ? Number(f.get("sourceLineId"))
                            : null,
                        });
                      }}
                    >
                      <label>
                        Vật tư
                        <select name="itemId" defaultValue="" required>
                          <option value="">Chọn vật tư (đơn vị cơ sở)</option>
                          {options.data?.items.map((i: Row) => (
                            <option key={i.itemId} value={i.itemId}>
                              {i.name} · {i.code}
                            </option>
                          ))}
                        </select>
                      </label>
                      <div className="form-grid">
                        <label>
                          Số lượng
                          <input
                            type="number"
                            name="quantity"
                            min={0.001}
                            step="0.001"
                            required
                          />
                        </label>
                        <label>
                          Đơn giá
                          <input
                            name="unitCost"
                            type="number"
                            min={0}
                            step="0.01"
                          />
                        </label>
                      </div>
                      {kind === "STOCKTAKE" && (
                        <label>
                          Số lượng đếm
                          <input
                            name="countedQty"
                            type="number"
                            min={0}
                            step="0.001"
                            required
                          />
                        </label>
                      )}
                      <label>
                        Mã dòng nguồn (nếu có)
                        <input name="sourceLineId" type="number" min={1} />
                      </label>
                      <button disabled={pending}>Lưu vật tư</button>
                      {error && (
                        <AlertPopup>
                          {error}
                        </AlertPopup>
                      )}
                    </form>
                  </Dialog>
                )}
                {command && (
                  <Dialog className="confirmation-popup"
                    title={
                      actions.find(([key]) => key === command)?.[1] ||
                      "Xác nhận"
                    }
                    close={() => {
                      if (!pending) setCommand("");
                    }}
                  >
                    <form
                      onSubmit={(e) => {
                        e.preventDefault();
                        mutate(
                          `documents/${id}/actions/${command}`,
                          action(doc.rowVersion, {
                            reason:
                              new FormData(e.currentTarget).get("reason") ||
                              null,
                          }),
                        );
                      }}
                    >
                      <h3>{actions.find((a) => a[0] === command)?.[1]}</h3>
                      {command === "post" && (
                        <p>
                          Ghi sổ sẽ thay đổi tồn kho. Chứng từ đã ghi sổ không
                          được sửa trực tiếp.
                        </p>
                      )}
                      <label>
                        Lý do / ghi chú
                        <textarea
                          name="reason"
                          minLength={10}
                          required={["cancel", "close", "reject"].includes(
                            command,
                          )}
                        />
                      </label>
                      <ConfirmationActions pending={pending} cancel={()=>setCommand("")}/>
                      {error && (
                        <AlertPopup>
                          {error}
                        </AlertPopup>
                      )}
                    </form>
                  </Dialog>
                )}
                {error && !adding && !command && (
                  <AlertPopup>
                    {error}
                  </AlertPopup>
                )}
                <h3>Lịch sử</h3>
                <History rows={fields(detail.data?.events)} />
              </>
            )}
          </State>
        </Dialog>
      )}
    </>
  );
}
