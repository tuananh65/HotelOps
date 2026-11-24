import { AlertPopup } from "./alert-popup";
import { useState } from "react";
import { SlidersHorizontal, Plus } from "lucide-react";
import { useAuth } from "./auth";
import { api, fields, message, timestamp, type Row, type Page } from "./api";
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
import PageTitle from "./page-title";
import PageToolbar from "./page-toolbar";
import {
  documentNames,
  documentPermission,
  documentHref,
  inventoryGroups,
  type InventoryGroup,
} from "./inventory-document-model";
import InventoryDocumentForm from "./inventory-document-form";

const titles = {
  transactions: "Nhập / Xuất / Chuyển kho",
  fulfillment: "Yêu cầu vật tư",
  stocktaking: "Kiểm kê & Điều chỉnh",
  replenishment: "Bổ sung vật tư",
  history: "Lịch sử & Chứng từ",
};
export default function InventoryDocuments({
  group,
}: {
  group: InventoryGroup;
}) {
  const { branch, can } = useAuth();
  const allowed = inventoryGroups[group].filter((k) =>
    can(
      k === "MATERIAL_REQUEST"
        ? "MR_VIEW"
        : k === "REPLENISHMENT"
          ? "REPLENISHMENT_VIEW"
          : "INVENTORY_VIEW",
    ),
  );
  const params = new URLSearchParams(location.hash.split("?")[1]);
  const [kind, setKind] = useState(
    allowed.includes(params.get("kind") || "")
      ? params.get("kind")!
      : allowed[0],
  );
  const [id, setId] = useState<number | null>(Number(params.get("id")) || null);
  const [search, setSearch] = useState("");
  const [filters, setFilters] = useState(false);
  const [status, setStatus] = useState("");
  const [sort, setSort] = useState("latest");
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
  const [page, setPage] = useState(1);
  const [revision, bump] = useState(0);
  const [form, setForm] = useState<{
    type: string;
    line?: Row;
    target?: string;
  } | null>(null);
  const [pending, setPending] = useState(false);
  const [error, setError] = useState("");
  const [notice, setNotice] = useState("");
  const query = useQuery<Page>(
    kind
      ? `documents?branchId=${branch}&kind=${kind}&page=${page}&pageSize=25&search=${encodeURIComponent(search)}&status=${status}&sort=${sort}${from ? `&from=${encodeURIComponent(new Date(`${from}T00:00:00`).toISOString())}` : ""}${to ? `&to=${encodeURIComponent(new Date(`${to}T23:59:59.999`).toISOString())}` : ""}`
      : null,
    revision,
  );
  const detail = useQuery(id ? `documents/${id}` : null, revision);
  const options = useQuery(`workspace/options?branchId=${branch}`, revision);
  const doc = detail.data?.document.fields;
  const context = detail.data?.inventory;
  const canCreate =
    kind &&
    ["RECEIPT", "TRANSFER", "STOCKTAKE", "REPLENISHMENT"].includes(kind) &&
    can(documentPermission[kind]);
  async function mutate(path: string, body: Row, method = "POST") {
    if (pending) return;
    setPending(true);
    setError("");
    setNotice("");
    try {
      const result = await api(path, body, method);
      setForm(null);
      bump((x) => x + 1);
      if (
        result.fields?.documentId &&
        (form?.type === "create" || form?.type === "derive")
      ) {
        location.hash = documentHref(result.fields);
      }
      setNotice(
        form?.type === "derive" || form?.type === "create"
          ? "Đã tạo bản nháp. Kiểm tra và xác nhận ghi sổ để cập nhật tồn kho."
          : "Đã lưu thay đổi.",
      );
    } catch (e) {
      setError(message(e));
      bump((x) => x + 1);
    } finally {
      setPending(false);
    }
  }
  const commands: [string, string][] = [];
  if (doc && context) {
    const hasLines = detail.data.lines.length > 0;
    if (context.canApprove && hasLines)
      commands.push(["approve", "Duyệt chứng từ"]);
    if (context.canPost && hasLines) commands.push(["post", "Xác nhận ghi sổ"]);
    if (
      doc.kind === "STOCKTAKE" &&
      doc.status === "IN_PROGRESS" &&
      hasLines &&
      fields(detail.data.lines).every((l) => l.countedQty != null) &&
      can("STOCKTAKE_PERFORM")
    )
      commands.push(["count", "Hoàn tất kiểm đếm"]);
    if (
      context.canEdit &&
      !doc.postedAt &&
      hasLines &&
      ["DRAFT", "IN_PROGRESS"].includes(doc.status)
    )
      commands.push(["cancel", "Hủy bản nháp"]);
    if (
      doc.kind === "REPLENISHMENT" &&
      can("REPLENISHMENT_CLOSE") &&
      hasLines &&
      ["REQUESTED", "PARTIALLY_RECEIVED", "FULLY_RECEIVED"].includes(doc.status)
    )
      commands.push(["close", "Đóng yêu cầu"]);
  }
  function openForm(value: typeof form) {
    setError("");
    setForm(value);
  }
  if (!allowed.length) return <Empty>Không có quyền truy cập chứng từ.</Empty>;
  return (
    <>
      <PageTitle>
        <h1>{titles[group]}</h1>
        <p>Chứng từ và số lượng trong chi nhánh</p>
      </PageTitle>
      {group === "transactions" && <p><a className="button-link" href="#documents?transfer=1">?i?u chuy?n v?t t? gi?a c? s?</a></p>}
      <PageToolbar>
        {allowed.length > 1 && (
          <label>
            Loại chứng từ
            <select
              aria-label="Loại chứng từ"
              value={kind}
              onChange={(e) => {
                setKind(e.target.value);
                setPage(1);
                setId(null);
                setStatus("");
              }}
            >
              {allowed.map((k) => (
                <option key={k} value={k}>
                  {documentNames[k]}
                </option>
              ))}
            </select>
          </label>
        )}
        <label>
          Tìm chứng từ
          <input
            type="search"
            value={search}
            placeholder="Mã chứng từ, lý do…"
            onChange={(e) => {
              setSearch(e.target.value);
              setPage(1);
            }}
          />
        </label>
        <button aria-expanded={filters} onClick={() => setFilters(!filters)}>
          <SlidersHorizontal size={16} /> Bộ lọc
        </button>
        {canCreate && group !== "history" && (
          <button
            className="primary"
            onClick={() => openForm({ type: "create" })}
          >
            <Plus size={16} /> Tạo {documentNames[kind].toLocaleLowerCase()}
          </button>
        )}
      </PageToolbar>
      {filters && (
        <PageToolbar>
          <label>
            Trạng thái
            <select
              value={status}
              onChange={(e) => {
                setStatus(e.target.value);
                setPage(1);
              }}
            >
              <option value="">Tất cả trạng thái</option>
              {[
                "DRAFT",
                "REQUESTED",
                "PENDING_APPROVAL",
                "APPROVED",
                "PARTIALLY_ISSUED",
                "FULLY_ISSUED",
                "IN_PROGRESS",
                "COUNTED",
                "POSTED",
                "IN_TRANSIT",
                "PARTIALLY_RECEIVED",
                "RECEIVED",
                "FULLY_RECEIVED",
                "CLOSED",
                "CANCELLED",
              ].map((s) => (
                <option key={s} value={s}>
                  {label(s)}
                </option>
              ))}
            </select>
          </label>
          <label>
            Từ ngày
            <input
              type="date"
              value={from}
              max={to || undefined}
              onChange={(e) => {
                setFrom(e.target.value);
                setPage(1);
              }}
            />
          </label>
          <label>
            Đến ngày
            <input
              type="date"
              value={to}
              min={from || undefined}
              onChange={(e) => {
                setTo(e.target.value);
                setPage(1);
              }}
            />
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
              <option value="number">Mã chứng từ</option>
            </select>
          </label>
          <button
            onClick={() => {
              setSearch("");
              setStatus("");
              setFrom("");
              setTo("");
              setSort("latest");
              setPage(1);
            }}
          >
            Đặt lại
          </button>
        </PageToolbar>
      )}
      {notice && <AlertPopup tone="success">{notice}</AlertPopup>}
      <State {...query} retry={() => bump((x) => x + 1)}>
        <div className="table-scroll">
          <table>
            <thead>
              <tr>
                <th>Chứng từ</th>
                <th>Trạng thái</th>
                <th>Ngày tạo</th>
                <th>Thao tác</th>
              </tr>
            </thead>
            <tbody>
              {fields(query.data?.items).map((d) => (
                <tr key={d.documentId}>
                  <td>
                    <strong>{d.documentNo}</strong>
                    <small>{d.reason || documentNames[d.kind]}</small>
                  </td>
                  <td>
                    <Badge value={d.status} />
                  </td>
                  <td>{timestamp(d.createdAt)}</td>
                  <td>
                    <button
                      onClick={() => {
                        setId(d.documentId);
                        setForm(null);
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
        <Pager
          page={page}
          total={query.data?.total || 0}
          pageSize={25}
          onPage={setPage}
        />
      </State>
      {id && (
        <Dialog
          title={doc?.documentNo || `Chứng từ #${id}`}
          className="room-drawer inventory-document"
          close={() => {
            if (!pending) {
              setId(null);
              setForm(null);
            }
          }}
        >
          <State {...detail} retry={() => bump((x) => x + 1)}>
            {doc && (
              <>
                <Badge value={doc.status} />
                <p>
                  {documentNames[doc.kind]} · {timestamp(doc.createdAt)}
                </p>
                <p>{doc.reason}</p>
                {context && (
                  <>
                    <dl className="inventory-context">
                      <dt>Người tạo</dt>
                      <dd>{context.creator}</dd>
                      <dt>Người duyệt</dt>
                      <dd>{context.approver || "Chưa duyệt"}</dd>
                      <dt>Kho xuất</dt>
                      <dd>{context.sourceWarehouse || "—"}</dd>
                      <dt>Kho nhận</dt>
                      <dd>{context.destinationWarehouse || "—"}</dd>
                      {doc.sourceDocumentId && (
                        <>
                          <dt>Chứng từ nguồn</dt>
                          <dd>{context.sourceNumber}</dd>
                        </>
                      )}
                    </dl>
                    <div className="toolbar">
                      {commands.map(([command, name]) => (
                        <button
                          key={command}
                          disabled={pending}
                          onClick={() => openForm({ type: command })}
                        >
                          {name}
                        </button>
                      ))}
                      {context.derivations
                        .filter((target: string) =>
                          context.lines.some(
                            (line: Row) => line.remaining[target] > 0,
                          ),
                        )
                        .map((target: string) => (
                          <button
                            className="primary"
                            key={target}
                            disabled={pending}
                            onClick={() => openForm({ type: "derive", target })}
                          >
                            {target === "ISSUE"
                              ? "Cấp phát vật tư"
                              : target === "RECEIPT"
                                ? "Tiếp nhận hàng"
                                : documentNames[target]}
                          </button>
                        ))}
                    </div>
                    {["ADJUSTMENT", "RETURN", "REVERSAL"].includes(doc.kind) &&
                      !doc.postedAt &&
                      !context.canPost && (
                        <p>
                          Chứng từ cần người khác duyệt; người ghi sổ phải khác
                          người duyệt.
                        </p>
                      )}
                  </>
                )}
                <div className="table-scroll">
                  <table>
                    <thead>
                      <tr>
                        <th>Vật tư / đơn vị</th>
                        <th>Số lượng</th>
                        {doc.kind === "MATERIAL_REQUEST" ? (
                          <>
                            <th>Được duyệt</th>
                            <th>Đã cấp / còn lại</th>
                          </>
                        ) : doc.kind === "STOCKTAKE" ? (
                          <>
                            <th>Tồn sổ / thực đếm</th>
                            <th>Chênh lệch</th>
                          </>
                        ) : (
                          <th>Tồn hiện tại (cơ sở)</th>
                        )}
                        {context?.canEdit && <th>Thao tác</th>}
                      </tr>
                    </thead>
                    <tbody>
                      {fields(detail.data?.lines).map((line) => {
                        const info = context?.lines.find(
                          (l: Row) => l.lineId === line.lineId,
                        );
                        const fulfill = fields(detail.data?.fulfillment).find(
                          (l) => l.lineId === line.lineId,
                        );
                        return (
                          <tr key={line.lineId}>
                            <td>
                              {info?.name ||
                                options.data?.items.find(
                                  (i: Row) => i.itemId === line.itemId,
                                )?.name}
                              <small>
                                {info?.code} · {info?.unit || "—"}
                              </small>
                            </td>
                            <td>{line.requestedQty ?? line.actualQty}</td>
                            {doc.kind === "MATERIAL_REQUEST" ? (
                              <>
                                <td>{line.approvedQty ?? "—"}</td>
                                <td>
                                  {(fulfill?.issuedBaseQty || 0) /
                                    line.conversionFactor}{" "}
                                  /{" "}
                                  {(fulfill?.missingBaseQty || 0) /
                                    line.conversionFactor}
                                </td>
                              </>
                            ) : doc.kind === "STOCKTAKE" ? (
                              <>
                                <td>
                                  {line.expectedQty ?? "—"} /{" "}
                                  {line.countedQty ?? "—"}
                                </td>
                                <td>
                                  {line.countedQty == null ||
                                  line.expectedQty == null
                                    ? "—"
                                    : line.countedQty - line.expectedQty}
                                </td>
                              </>
                            ) : (
                              <td>{info?.balance ?? "—"}</td>
                            )}
                            {context?.canEdit && (
                              <td>
                                <button
                                  disabled={pending}
                                  onClick={() =>
                                    openForm({ type: "line", line })
                                  }
                                >
                                  Sửa
                                </button>
                              </td>
                            )}
                          </tr>
                        );
                      })}
                    </tbody>
                  </table>
                </div>
                {context?.canEdit && !doc.sourceDocumentId && (
                  <button onClick={() => openForm({ type: "line" })}>
                    + Thêm vật tư
                  </button>
                )}
                {context?.children.length > 0 && (
                  <section>
                    <h3>Chứng từ liên quan</h3>
                    {context.children.map((child: Row) => (
                      <a
                        className="overview-task"
                        key={child.documentId}
                        href={documentHref(
                          child as { kind: string; documentId: number },
                        )}
                      >
                        <span>
                          {documentNames[child.kind]}
                          <small>{child.documentNo}</small>
                        </span>
                        <Badge value={child.status} />
                      </a>
                    ))}
                  </section>
                )}
                <h3>Lịch sử chứng từ</h3>
                <History rows={fields(detail.data?.events)} />
              </>
            )}
          </State>
        </Dialog>
      )}
      {form && (
        <Dialog
          title={
            form.type === "create"
              ? `Tạo ${documentNames[kind].toLowerCase()}`
              : form.type === "derive"
                ? documentNames[form.target!]
                : form.type === "line"
                  ? "Ghi nhận vật tư"
                  : commands.find((c) => c[0] === form.type)?.[1] || "Xác nhận"
          }
          close={() => {
            if (!pending) setForm(null);
          }}
        >
          <State {...options}>
            <InventoryDocumentForm
              key={`${form.type}-${form.line?.lineId || "new"}-${form.target || ""}`}
              form={form}
              kind={kind}
              doc={doc}
              context={context}
              lines={fields(detail.data?.lines)}
              options={options.data}
              pending={pending}
              submit={mutate}
            />
          </State>
          {error && (
            <AlertPopup>
              {error}
            </AlertPopup>
          )}
        </Dialog>
      )}
    </>
  );
}
