import { createHttpClient, isObject } from "../auth/http";
import {
  positiveId,
  type InventoryItem,
  type InventoryPage,
  type InventoryResult,
} from "./contracts";

function decodeItem(value: unknown): InventoryItem | undefined {
  if (
    !isObject(value) ||
    !positiveId(value.itemId) ||
    !positiveId(value.categoryId) ||
    !positiveId(value.baseUomId) ||
    typeof value.itemCode !== "string" ||
    !value.itemCode.trim() ||
    typeof value.itemName !== "string" ||
    !value.itemName.trim() ||
    typeof value.isActive !== "boolean" ||
    typeof value.isConsumable !== "boolean" ||
    !(value.description === null || typeof value.description === "string") ||
    !(
      value.specification === null || typeof value.specification === "string"
    ) ||
    typeof value.rowVersion !== "string" ||
    !/^[A-Za-z0-9+/]{10}[AEIMQUYcgkosw048]=$/.test(value.rowVersion)
  )
    return;
  // Copy the actual DTO only; stock quantities and arbitrary extensions are excluded.
  return {
    itemId: value.itemId,
    itemCode: value.itemCode,
    itemName: value.itemName,
    categoryId: value.categoryId,
    baseUomId: value.baseUomId,
    isActive: value.isActive,
    isConsumable: value.isConsumable,
    description: value.description,
    specification: value.specification,
    rowVersion: value.rowVersion,
  };
}

function validPage(page: number, size: number): boolean {
  return (
    positiveId(page) &&
    page <= 2_147_483_647 &&
    positiveId(size) &&
    size <= 100 &&
    (page - 1) * size <= 2_147_483_647
  );
}

function decodePage(
  value: unknown,
  page: number,
  pageSize: number,
): InventoryPage | undefined {
  if (
    !isObject(value) ||
    !Array.isArray(value.items) ||
    value.page !== page ||
    value.pageSize !== pageSize ||
    typeof value.totalCount !== "number" ||
    !Number.isSafeInteger(value.totalCount) ||
    value.totalCount < 0 ||
    value.totalCount > 2_147_483_647
  )
    return;
  const items = value.items.map(decodeItem);
  if (items.some((x) => x === undefined)) return;
  const decoded = items as InventoryItem[];
  if (
    decoded.length > pageSize ||
    new Set(decoded.map((x) => x.itemId)).size !== decoded.length ||
    decoded.length !==
      Math.min(pageSize, Math.max(0, value.totalCount - (page - 1) * pageSize))
  )
    return;
  return { items: decoded, totalCount: value.totalCount, page, pageSize };
}

/** GET only. Reuses accepted cookie transport without changing shared URL rules. */
export function createInventoryApi(
  fetcher: typeof fetch = globalThis.fetch.bind(globalThis),
) {
  async function get<T>(
    path: string,
    decode: (v: unknown) => T | undefined,
    signal?: AbortSignal,
    query = "",
  ): Promise<InventoryResult<T>> {
    if (signal?.aborted) return { ok: false, kind: "cancelled" };
    // Same bounded query adapter as RoleCatalogApi. No caller URL or free-text query.
    const request = createHttpClient((url, init) =>
      fetcher(String(url) + query, init),
    );
    const result = await request({
      method: "GET",
      url: path,
      expectedStatus: 200,
      decode,
      signal,
    });
    if (signal?.aborted) return { ok: false, kind: "cancelled" };
    if (result.ok) return { ok: true, data: result.data };
    return {
      ok: false,
      kind: result.error.kind,
      ...(result.error.kind === "http" ? { status: result.error.status } : {}),
    };
  }
  return {
    list(
      page = 1,
      pageSize = 20,
      signal?: AbortSignal,
    ): Promise<InventoryResult<InventoryPage>> {
      if (!validPage(page, pageSize))
        return Promise.resolve({ ok: false, kind: "invalid-request" });
      return get(
        "/api/inventory/items",
        (value) => decodePage(value, page, pageSize),
        signal,
        `?page=${page}&pageSize=${pageSize}`,
      );
    },
    item(
      id: number,
      signal?: AbortSignal,
    ): Promise<InventoryResult<InventoryItem>> {
      if (!positiveId(id))
        return Promise.resolve({ ok: false, kind: "invalid-request" });
      return get(
        `/api/inventory/items/${id}`,
        (value) => {
          const item = decodeItem(value);
          return item?.itemId === id ? item : undefined;
        },
        signal,
      );
    },
  };
}
