export interface InventoryItem {
  itemId: number;
  itemCode: string;
  itemName: string;
  categoryId: number;
  baseUomId: number;
  isActive: boolean;
  isConsumable: boolean;
  description: string | null;
  specification: string | null;
  rowVersion: string;
}

export interface InventoryPage {
  items: InventoryItem[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export type InventoryResult<T> =
  { ok: true; data: T } | { ok: false; kind: string; status?: number };

export function positiveId(value: unknown): value is number {
  return typeof value === "number" && Number.isSafeInteger(value) && value > 0;
}

export function routeItemId(value: string): number | undefined {
  if (!/^[1-9][0-9]*$/.test(value)) return;
  const id = Number(value);
  return positiveId(id) ? id : undefined;
}
