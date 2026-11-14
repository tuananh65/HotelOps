import type { StructureDto } from "./contracts";
export type StructureNode = "branch" | "floor" | "area" | "room" | "department";
export type StructureCollection = "floors" | "areas" | "rooms" | "departments";
export interface StructureQuery {
  branchId: number;
  nodeType: StructureNode;
  nodeId?: number;
  collection?: StructureCollection;
  isActive: string;
  page: number;
  pageSize: number;
}
export const defaultCollection = (
  node: StructureNode,
): StructureCollection | undefined =>
  node === "branch"
    ? "floors"
    : node === "floor"
      ? "areas"
      : node === "area"
        ? "rooms"
        : undefined;
export const collectionsFor = (node: StructureNode): StructureCollection[] =>
  node === "branch"
    ? ["floors", "areas", "departments"]
    : node === "floor"
      ? ["areas", "rooms"]
      : node === "area"
        ? ["rooms"]
        : [];
/** Additional read binding, not a permission evaluator. Never infer a full tree from a page. */
export function boundStructure(data: StructureDto, q: StructureQuery): boolean {
  if (
    data.branch.id !== q.branchId ||
    data.nodeType !== q.nodeType ||
    data.collection !== (q.collection ?? null) ||
    data.isActive !== (q.isActive === "" ? null : q.isActive === "true")
  )
    return false;
  const selected =
    q.nodeType === "floor"
      ? data.selectedFloor
      : q.nodeType === "area"
        ? data.selectedArea
        : q.nodeType === "room"
          ? data.selectedRoom
          : q.nodeType === "department"
            ? data.selectedDepartment
            : null;
  if (q.nodeType !== "branch" && selected?.id !== q.nodeId) return false;
  if (!q.collection) return true;
  if (!data.availableCollections.includes(q.collection)) return false;
  const page = data[q.collection];
  if (
    !page ||
    page.page !== q.page ||
    page.pageSize !== q.pageSize ||
    page.items.length > q.pageSize ||
    page.totalCount < page.items.length
  )
    return false;
  if (
    q.nodeType === "floor" &&
    q.collection === "areas" &&
    data.areas?.items.some((a) => a.floorId !== q.nodeId)
  )
    return false;
  if (
    q.nodeType === "floor" &&
    q.collection === "rooms" &&
    data.rooms?.items.some((r) => r.floorId !== q.nodeId)
  )
    return false;
  if (
    q.nodeType === "area" &&
    q.collection === "rooms" &&
    data.rooms?.items.some((r) => r.areaId !== q.nodeId)
  )
    return false;
  return page.items.every(
    (item) =>
      item.branchId === q.branchId &&
      (q.isActive === "" || item.isActive === (q.isActive === "true")),
  );
}
