import { asId, toLabel } from "../api/adapters";
import { ApiError, request } from "../api/http";
import type { BalanceDto } from "../api/types";
import type { RecordData } from "../domain";

// Contract: feature/DEV2-inventory-authz-clean, InventoryController/InventoryDtos.
// Only GET requests. Backend InventoryAccess owns permission and branch decisions.
type Id = number | string;
interface Page<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}
interface Unit {
  uomId: Id;
  uomCode: string;
  uomName: string;
  isActive: boolean;
  description?: string;
  rowVersion: string;
}
interface Category {
  id: Id;
  code: string;
  name: string;
  isActive: boolean;
}
interface Item {
  itemId: Id;
  itemCode: string;
  itemName: string;
  categoryId: Id;
  baseUomId: Id;
  isActive: boolean;
  isConsumable: boolean;
  description?: string;
  specification?: string;
  rowVersion: string;
}
interface Warehouse {
  warehouseId: Id;
  branchId: Id;
  departmentId?: Id;
  warehouseCode: string;
  warehouseName: string;
  isActive: boolean;
  locationDescription?: string;
  rowVersion: string;
}
interface Stock {
  warehouseId: Id;
  itemId: Id;
  onHand: number;
  available: number;
  minimumQty: number | null;
  belowMinimum: boolean;
}
interface DocumentLine {
  lineId: Id;
  itemId: Id;
  uomId: Id;
  requestedQty: number | null;
  actualQty: number;
  baseQty: number;
  receivedQty?: number;
  damagedQty?: number;
  missingQty?: number;
  inTransitQty?: number;
}
interface Document {
  documentId: Id;
  documentNo: string;
  documentType: string;
  status: string;
  sourceWarehouseId?: Id;
  destinationWarehouseId?: Id;
  documentDate: string;
  createdAt: string;
  reason?: string;
  note?: string;
  rowVersion: string;
  lines: DocumentLine[];
}
interface CountLine {
  stocktakeLineId: Id;
  itemId: Id;
  systemQty: number;
  countedQty: number | null;
  varianceQty: number | null;
}
interface Stocktake {
  stocktakeId: Id;
  stocktakeNo: string;
  warehouseId: Id;
  snapshotAt: string;
  status: string;
  countedBy?: Id;
  note?: string;
  rowVersion: string;
  lines: CountLine[];
}
interface Movement {
  movementId: Id;
  documentId: Id;
  documentNo: string;
  documentType: string;
  status: string;
  warehouseId: Id;
  itemId: Id;
  signedBaseQty: number;
  occurredAt: string;
}

export const isInventoryScreen = (id: number) =>
  Number.isInteger(id) && id >= 51 && id <= 61;
const root = "/api/inventory/";
const optionalId = (id: Id | null | undefined) => (id == null ? "" : asId(id));
const invalid = () =>
  new ApiError(
    "Inventory API returned invalid or incomplete data. Reload and try again.",
    0,
    "server",
  );
const changed = () =>
  new ApiError(
    "Inventory changed during pagination. Reload the list.",
    409,
    "conflict",
  );

/** The backend has no snapshot token. Detect count changes, duplicate IDs and
 * incomplete pages; do not fabricate a snapshot or claim an atomic read. */
export async function readInventoryPages<T>(
  path: string,
  key: (row: T) => string,
  signal?: AbortSignal,
): Promise<T[]> {
  const rows: T[] = [];
  const seen = new Set<string>();
  let total: number | undefined;
  let size: number | undefined;
  for (let page = 1; page <= 10000; page++) {
    const result = await request<Page<T>>(root + path, {
      query: { page, pageSize: 100 },
      signal,
    });
    if (
      !result ||
      !Array.isArray(result.items) ||
      result.page !== page ||
      !Number.isSafeInteger(result.totalCount) ||
      result.totalCount < 0 ||
      !Number.isSafeInteger(result.pageSize) ||
      result.pageSize < 1 ||
      result.pageSize > 100 ||
      result.items.length > result.pageSize
    )
      throw invalid();
    if (
      (total !== undefined && total !== result.totalCount) ||
      (size !== undefined && size !== result.pageSize)
    )
      throw changed();
    total = result.totalCount;
    size = result.pageSize;
    for (const row of result.items) {
      const id = key(row);
      if (seen.has(id)) throw changed();
      seen.add(id);
      rows.push(row);
    }
    if (rows.length > total) throw changed();
    if (rows.length === total) return rows;
    if (result.items.length !== size) throw invalid();
  }
  throw invalid();
}
const base = (
  id: Id,
  name: string,
  code: string,
  status: string,
  rowVersion = "",
): RecordData => {
  if (
    typeof name !== "string" ||
    typeof code !== "string" ||
    typeof status !== "string"
  )
    throw invalid();
  return {
    id: asId(id),
    name,
    code,
    status: toLabel(status),
    branch: "",
    department: "",
    version: 0,
    createdAt: "",
    rowVersion,
    canEdit: false,
    allowedActions: [],
  };
};

export async function loadInventoryScreen(
  screen: number,
  recordId?: string,
  signal?: AbortSignal,
) {
  if (!isInventoryScreen(screen)) throw invalid();
  const detailPaths: Record<number, string> = {
    52: "warehouses",
    55: "items",
    58: "documents",
    60: "stocktakes",
  };
  let detail: Warehouse | Item | Document | Stocktake | undefined;
  if (detailPaths[screen] && recordId && recordId !== "select") {
    if (!/^[1-9]\d*$/.test(recordId))
      throw new ApiError("Record not found.", 404, "notFound");
    detail = await request(
      root + detailPaths[screen] + "/" + encodeURIComponent(recordId),
      { signal },
    );
    if (!detail) throw invalid();
    const ids: Record<number, Id> = {
      52: (detail as Warehouse).warehouseId,
      55: (detail as Item).itemId,
      58: (detail as Document).documentId,
      60: (detail as Stocktake).stocktakeId,
    };
    if (asId(ids[screen]) !== recordId) throw invalid();
  }
  // Fetch only supporting collections used by the current screen.
  const needDocuments = [51, 52, 54, 55, 57, 58].includes(screen);
  const needMovements = [53, 61].includes(screen);
  const [
    units,
    categories,
    items,
    warehouses,
    stock,
    documents,
    stocktakes,
    movements,
  ] = await Promise.all([
    readInventoryPages<Unit>("units", (x) => asId(x.uomId), signal),
    request<Category[]>(root + "item-categories", { signal }),
    readInventoryPages<Item>("items", (x) => asId(x.itemId), signal),
    readInventoryPages<Warehouse>(
      "warehouses",
      (x) => asId(x.warehouseId),
      signal,
    ),
    readInventoryPages<Stock>(
      "stock",
      (x) => `${asId(x.warehouseId)}:${asId(x.itemId)}`,
      signal,
    ),
    needDocuments
      ? readInventoryPages<Document>(
          "documents",
          (x) => asId(x.documentId),
          signal,
        )
      : Promise.resolve([] as Document[]),
    [59, 60].includes(screen)
      ? readInventoryPages<Stocktake>(
          "stocktakes",
          (x) => asId(x.stocktakeId),
          signal,
        )
      : Promise.resolve([] as Stocktake[]),
    needMovements
      ? readInventoryPages<Movement>(
          "movements",
          (x) => asId(x.movementId),
          signal,
        )
      : Promise.resolve([] as Movement[]),
  ]);
  if (!Array.isArray(categories)) throw invalid();
  const upsert = <T>(rows: T[], value: T, key: (x: T) => Id) => {
    const index = rows.findIndex((x) => asId(key(x)) === asId(key(value)));
    if (index < 0) rows.push(value);
    else rows[index] = value;
  };
  if (detail) {
    if (screen === 52)
      upsert(warehouses, detail as Warehouse, (x) => x.warehouseId);
    if (screen === 55) upsert(items, detail as Item, (x) => x.itemId);
    if (screen === 58)
      upsert(documents, detail as Document, (x) => x.documentId);
    if (screen === 60)
      upsert(stocktakes, detail as Stocktake, (x) => x.stocktakeId);
  }
  const unitNames = new Map(units.map((x) => [asId(x.uomId), x.uomName]));
  const categoryNames = new Map(categories.map((x) => [asId(x.id), x.name]));
  const warehouseNames = new Map(
    warehouses.map((x) => [asId(x.warehouseId), x.warehouseName]),
  );
  const reference = (names: Map<string, string>, id: Id | null | undefined) =>
    id == null ? "" : (names.get(asId(id)) ?? `#${asId(id)}`);
  const collections: Record<string, RecordData[]> = {
    units: units.map((x) => ({
      ...base(
        x.uomId,
        x.uomName,
        x.uomCode,
        x.isActive ? "ACTIVE" : "INACTIVE",
        x.rowVersion,
      ),
      description: x.description ?? "",
    })),
    items: items.map((x) => ({
      ...base(
        x.itemId,
        x.itemName,
        x.itemCode,
        x.isActive ? "ACTIVE" : "INACTIVE",
        x.rowVersion,
      ),
      category: reference(categoryNames, x.categoryId),
      unit: reference(unitNames, x.baseUomId),
      description: x.description ?? "",
      specification: x.specification ?? "",
      minimum: "N/A",
    })),
    warehouses: warehouses.map((x) => ({
      ...base(
        x.warehouseId,
        x.warehouseName,
        x.warehouseCode,
        x.isActive ? "ACTIVE" : "INACTIVE",
        x.rowVersion,
      ),
      branchId: asId(x.branchId),
      branch: `#${asId(x.branchId)}`,
      departmentId: optionalId(x.departmentId),
      department: x.departmentId == null ? "" : `#${asId(x.departmentId)}`,
      description: x.locationDescription ?? "",
    })),
    transactions: documents.map((x) => ({
      ...base(x.documentId, x.documentNo, x.documentNo, x.status, x.rowVersion),
      type: toLabel(x.documentType),
      warehouse: optionalId(x.sourceWarehouseId ?? x.destinationWarehouseId),
      destination: optionalId(x.destinationWarehouseId),
      source: [
        reference(warehouseNames, x.sourceWarehouseId),
        reference(warehouseNames, x.destinationWarehouseId),
      ]
        .filter(Boolean)
        .join(" → "),
      createdAt: x.createdAt,
      documentDate: x.documentDate,
      note: x.note ?? "",
      reason: x.reason ?? "",
      lines: x.lines.map((l) => ({
        id: asId(l.lineId),
        item: asId(l.itemId),
        unit: reference(unitNames, l.uomId),
        quantity:
          x.status === "DRAFT" ? (l.requestedQty ?? l.actualQty) : l.actualQty,
        baseQuantity: l.baseQty,
        received: l.receivedQty,
        damaged: l.damagedQty,
        missing: l.missingQty,
        inTransit: l.inTransitQty,
      })),
    })),
    stocktakes: stocktakes.map((x) => ({
      ...base(
        x.stocktakeId,
        x.stocktakeNo,
        x.stocktakeNo,
        x.status,
        x.rowVersion,
      ),
      warehouse: asId(x.warehouseId),
      cutoff: x.snapshotAt,
      creator: optionalId(x.countedBy),
      note: x.note ?? "",
      lines: x.lines.map((l) => ({
        id: asId(l.stocktakeLineId),
        item: asId(l.itemId),
        system: l.systemQty,
        quantity: l.countedQty,
        variance: l.varianceQty,
      })),
    })),
    ledger: movements.map((x) => ({
      ...base(x.movementId, x.documentNo, x.documentNo, x.status),
      warehouse: asId(x.warehouseId),
      item: asId(x.itemId),
      document: asId(x.documentId),
      type: toLabel(x.documentType),
      delta: x.signedBaseQty,
      createdAt: x.occurredAt,
    })),
  };
  const balances: BalanceDto[] = stock.map((x) => {
    if (
      typeof x.available !== "number" ||
      !Number.isFinite(x.available) ||
      (x.minimumQty !== null &&
        (typeof x.minimumQty !== "number" || !Number.isFinite(x.minimumQty)))
    )
      throw invalid();
    return {
      warehouseId: asId(x.warehouseId),
      itemId: asId(x.itemId),
      quantity: x.available,
      minimum: x.minimumQty,
    };
  });
  // No inferred zero balance for absent pairs and no fabricated historical balance.
  return { collections, balances };
}
