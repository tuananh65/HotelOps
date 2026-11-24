import { useState } from "react";
import { useAuth } from "./auth";
import { action, type Row } from "./api";
import { ConfirmationActions, State, useQuery } from "./ui";
import { documentNames } from "./inventory-document-model";

type Props = {
  form: { type: string; line?: Row; target?: string };
  kind: string;
  doc?: Row;
  context?: Row;
  lines: Row[];
  options: Row;
  pending: boolean;
  cancel: () => void;
  submit: (path: string, body: Row, method?: string) => Promise<void>;
};
export default function InventoryDocumentForm({
  form,
  kind,
  doc,
  context,
  lines,
  options,
  pending,
  cancel,
  submit,
}: Props) {
  const { branch } = useAuth();
  const [key] = useState(() => crypto.randomUUID());
  const [itemId, setItem] = useState(
    form.line?.itemId || options.items[0]?.itemId || 0,
  );
  const [quantity, setQuantity] = useState(String(form.line?.actualQty ?? 1));
  const [warehouseId, setWarehouse] = useState(
    doc?.sourceWarehouseId ||
      doc?.destinationWarehouseId ||
      options.warehouses[0]?.warehouseId ||
      0,
  );
  const stock = useQuery<Row[]>(`insights/stock?branchId=${branch}`);
  const [quantities, setQuantities] = useState<Record<string, string>>(() =>
    Object.fromEntries(
      (context?.lines || []).map((l: Row) => [
        l.lineId,
        String(l.remaining[form.target!] ?? 0),
      ]),
    ),
  );
  const [destination, setDestination] = useState("");
  const actualKind = doc?.kind || kind;
  const baseItem = options.items.find((i: Row) => i.itemId === itemId);
  const unit = options.references.find(
    (u: Row) => u.referenceId === (form.line?.unitId || baseItem?.baseUnitId),
  );
  const balance =
    stock.data?.find(
      (r) =>
        r.warehouseId === Number(warehouseId) && r.itemId === Number(itemId),
    )?.baseQty ?? (stock.data ? 0 : null);
  const outgoing = ["ISSUE", "TRANSFER"].includes(actualKind);
  const factor = Number(form.line?.conversionFactor || 1);
  const after =
    balance === null
      ? null
      : balance + Number(quantity) * factor * (outgoing ? -1 : 1);
  const selectWarehouse = (
    name: string,
    title: string,
    value?: string | number,
    onChange?: (value: string) => void,
  ) => (
    <label>
      {title}
      <select
        name={name}
        value={value}
        defaultValue={value === undefined ? "" : undefined}
        required
        onChange={(e) => onChange?.(e.target.value)}
      >
        <option value="">Chọn kho</option>
        {options.warehouses.map((w: Row) => (
          <option key={w.warehouseId} value={w.warehouseId}>
            {w.name}
          </option>
        ))}
      </select>
    </label>
  );
  if (form.type === "create")
    return (
      <form
        onSubmit={(e) => {
          e.preventDefault();
          const data = new FormData(e.currentTarget);
          submit("documents", {
            branchId: branch,
            kind,
            operationKey: key,
            sourceWarehouseId: ["TRANSFER", "STOCKTAKE"].includes(kind)
              ? Number(data.get("sourceWarehouseId"))
              : null,
            destinationWarehouseId: [
              "RECEIPT",
              "REPLENISHMENT",
              "TRANSFER",
            ].includes(kind)
              ? Number(data.get("destinationWarehouseId"))
              : null,
            supplierName: data.get("supplierName") || null,
            expectedOn: data.get("expectedOn") || null,
            reason: data.get("reason"),
          });
        }}
      >
        {["TRANSFER", "STOCKTAKE"].includes(kind) &&
          selectWarehouse(
            "sourceWarehouseId",
            "Kho xuất / kiểm kê",
            warehouseId,
            (v) => setWarehouse(Number(v)),
          )}
        {["RECEIPT", "REPLENISHMENT", "TRANSFER"].includes(kind) &&
          selectWarehouse(
            "destinationWarehouseId",
            "Kho nhận",
            destination,
            setDestination,
          )}
        {["RECEIPT", "REPLENISHMENT"].includes(kind) && (
          <label>
            Nhà cung cấp
            <input name="supplierName" maxLength={200} />
          </label>
        )}
        {kind === "REPLENISHMENT" && (
          <label>
            Ngày dự kiến
            <input name="expectedOn" type="date" />
          </label>
        )}
        <label>
          Lý do
          <textarea name="reason" minLength={10} maxLength={2000} required />
        </label>
        {kind === "TRANSFER" && (
          <p>Chuyển giữa hai kho được phép trong cùng chi nhánh.</p>
        )}
        <button
          className="primary"
          disabled={
            pending ||
            (kind === "TRANSFER" && String(warehouseId) === destination)
          }
        >
          Tạo bản nháp
        </button>
      </form>
    );
  if (form.type === "derive") {
    const target = form.target!;
    const eligible =
      context?.lines.filter((l: Row) => l.remaining[target] > 0) || [];
    const invalid =
      !eligible.some((l: Row) => Number(quantities[l.lineId]) > 0) ||
      eligible.some(
        (l: Row) =>
          !Number.isFinite(Number(quantities[l.lineId])) ||
          Number(quantities[l.lineId]) < 0 ||
          Number(quantities[l.lineId]) > l.remaining[target],
      );
    return (
      <form
        onSubmit={(e) => {
          e.preventDefault();
          const data = new FormData(e.currentTarget);
          submit(`documents/${doc!.documentId}/derive`, {
            rowVersion: doc!.rowVersion,
            operationKey: key,
            kind: target,
            warehouseId: Number(data.get("warehouseId")) || null,
            reason: data.get("reason"),
            lines: eligible
              .filter((l: Row) => Number(quantities[l.lineId]) > 0)
              .map((l: Row) => ({
                lineId: l.lineId,
                quantity: Number(quantities[l.lineId]),
              })),
          });
        }}
      >
        <p>
          Tạo {documentNames[target].toLowerCase()} từ {doc?.documentNo}. Bản
          nháp chưa thay đổi tồn kho.
        </p>
        {target === "ISSUE" && context?.receivingRoom && <section className="panel"><strong>Địa điểm cấp đồ dùng bền</strong><p>{context.receivingRoom}</p></section>}
        {((target === "ISSUE" && !doc?.sourceWarehouseId) ||
          (target === "RECEIPT" && !doc?.destinationWarehouseId)) &&
          selectWarehouse("warehouseId", "Kho thực hiện", warehouseId, (v) =>
            setWarehouse(Number(v)),
          )}
        {eligible.map((l: Row) => {
          const source = lines.find((s) => s.lineId === l.lineId)!;
          const current =
            stock.data?.find(
              (s) =>
                s.warehouseId === Number(warehouseId) &&
                s.itemId === source.itemId,
            )?.baseQty ?? (stock.data ? 0 : null);
          const predicted =
            current === null
              ? null
              : current +
                Number(quantities[l.lineId]) *
                  source.conversionFactor *
                  (target === "ISSUE"
                    ? -1
                    : target === "ADJUSTMENT"
                      ? Math.sign(source.countedQty - source.expectedQty)
                      : target === "REVERSAL"
                        ? ["RECEIPT", "RETURN", "TRANSFER_RECEIPT"].includes(
                            actualKind,
                          )
                          ? -1
                          : 1
                        : 1);
          return (
            <label key={l.lineId}>
              {l.name} · {l.unit}
              <small>
                Còn được xử lý: {l.remaining[target]} {l.unit}
                {current !== null
                  ? ` · Tồn cơ sở: ${current} → ${predicted}`
                  : ""}
              </small>
              <input
                type="number"
                min={0}
                max={l.remaining[target]}
                step="0.000001"
                required
                readOnly={["ADJUSTMENT", "REVERSAL"].includes(target)}
                value={quantities[l.lineId]}
                onChange={(e) =>
                  setQuantities({ ...quantities, [l.lineId]: e.target.value })
                }
              />
            </label>
          );
        })}
        <label>
          Lý do
          <textarea name="reason" required minLength={10} maxLength={2000} />
        </label>
        {["ADJUSTMENT", "RETURN", "REVERSAL"].includes(target) && (
          <p>Bản nháp cần được người khác duyệt trước khi ghi sổ.</p>
        )}
        <button className="primary" disabled={pending || invalid}>
          Tạo chứng từ liên kết
        </button>
      </form>
    );
  }
  if (form.type === "line")
    return (
      <form
        onSubmit={(e) => {
          e.preventDefault();
          const data = new FormData(e.currentTarget);
          submit(
            `documents/${doc!.documentId}/lines${form.line ? `/${form.line.lineId}` : ""}`,
            {
              rowVersion: doc!.rowVersion,
              itemId,
              unitId: form.line?.unitId || baseItem.baseUnitId,
              quantity: Number(quantity),
              unitCost: data.get("unitCost")
                ? Number(data.get("unitCost"))
                : null,
              countedQty:
                actualKind === "STOCKTAKE"
                  ? Number(data.get("countedQty"))
                  : null,
              sourceLineId: form.line?.sourceLineId || null,
            },
            form.line ? "PUT" : "POST",
          );
        }}
      >
        <label>
          Vật tư
          <select
            value={itemId}
            disabled={!!form.line}
            onChange={(e) => setItem(Number(e.target.value))}
            required
          >
            {options.items.map((i: Row) => (
              <option key={i.itemId} value={i.itemId}>
                {i.code} · {i.name}
              </option>
            ))}
          </select>
        </label>
        <label>
          Số lượng · {unit?.name}
          <input
            type="number"
            min={0.000001}
            step="0.000001"
            required
            value={quantity}
            onChange={(e) => setQuantity(e.target.value)}
          />
        </label>
        <label>
          Đơn giá
          <input
            type="number"
            name="unitCost"
            min={0}
            step="0.0001"
            defaultValue={form.line?.unitCost ?? ""}
          />
        </label>
        {actualKind === "STOCKTAKE" && (
          <label>
            Số lượng thực đếm · {unit?.name}
            <input
              name="countedQty"
              type="number"
              min={0}
              step="0.000001"
              required
              defaultValue={form.line?.countedQty ?? 0}
            />
          </label>
        )}
        {balance !== null && actualKind !== "STOCKTAKE" && (
          <p>
            Tồn cơ sở hiện tại: {balance} · Dự kiến sau ghi sổ: {after}
          </p>
        )}
        {!!stock.error && (
          <p>Chưa tải được tồn hiện tại. Backend sẽ kiểm tra khi ghi sổ.</p>
        )}
        <button
          className="primary"
          disabled={
            pending || !baseItem || (outgoing && after !== null && after < 0)
          }
        >
          Lưu vật tư
        </button>
      </form>
    );
  return (
    <form
      onSubmit={(e) => {
        e.preventDefault();
        submit(
          `documents/${doc!.documentId}/actions/${form.type}`,
          action(doc!.rowVersion, {
            operationKey: key,
            reason: new FormData(e.currentTarget).get("reason") || null,
          }),
        );
      }}
    >
      <p>
        {form.type === "post"
          ? "Ghi sổ sẽ cập nhật tồn kho. Chứng từ đã ghi sổ không được sửa trực tiếp."
          : form.type === "approve"
            ? "Kiểm tra số lượng và chứng từ nguồn trước khi duyệt. Người ghi sổ phải khác người duyệt."
            : form.type === "close"
              ? "Đóng yêu cầu sẽ ngừng tiếp nhận phần còn lại."
              : "Xác nhận thay đổi trạng thái chứng từ."}
      </p>
      {form.type === "post" && (
        <State {...stock}>
          <div>
            {lines.map((line) => {
              const info = context?.lines.find(
                (l: Row) => l.lineId === line.lineId,
              );
              const delta =
                line.actualQty *
                line.conversionFactor *
                (outgoing
                  ? -1
                  : actualKind === "ADJUSTMENT"
                    ? Math.sign(
                        (line.countedQty || 0) - (line.expectedQty || 0),
                      )
                    : 1);
              return (
                <p key={line.lineId}>
                  {info?.name} · {line.actualQty} {info?.unit}
                  {info?.balance != null &&
                  !["REVERSAL", "ADJUSTMENT"].includes(actualKind)
                    ? ` · Tồn cơ sở: ${info.balance} → ${info.balance + delta}`
                    : ""}
                </p>
              );
            })}
          </div>
        </State>
      )}
      <label>
        Lý do / ghi chú
        <textarea
          name="reason"
          minLength={10}
          maxLength={2000}
          required={["cancel", "close"].includes(form.type)}
        />
      </label>
      <ConfirmationActions pending={pending} cancel={cancel}/>
    </form>
  );
}
