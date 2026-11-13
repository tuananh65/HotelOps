import { positiveId } from "./model";
export type VersionStatus = "DRAFT" | "PUBLISHED" | "RETIRED";
export interface CollectionQuery {
  page: number;
  pageSize: number;
  search: string | null;
  isActive: boolean | null;
  status: VersionStatus | null;
  roomTypeId: number | null;
}
export interface VersionSummary {
  id: number;
  number: number;
  status: VersionStatus;
  effectiveAt: string | null;
  publishedBy: number | null;
  usedByInspection: boolean;
}
export interface CollectionItem {
  id: number;
  branchId: number;
  code: string;
  name: string;
  active: boolean;
  roomTypeIds: number[];
  versions: VersionSummary[];
}
export interface CollectionPage extends CollectionQuery {
  items: CollectionItem[];
  total: number;
  branchId: number;
}
export const defaultCollectionQuery = (): CollectionQuery => ({
  page: 1,
  pageSize: 20,
  search: null,
  isActive: null,
  status: null,
  roomTypeId: null,
});
export function collectionQuery(input: CollectionQuery): CollectionQuery {
  const search = input.search?.trim() || null;
  if (
    !Number.isInteger(input.page) ||
    !Number.isInteger(input.pageSize) ||
    input.page < 1 ||
    input.pageSize < 1 ||
    input.pageSize > 100 ||
    input.page > Math.floor(2147483647 / input.pageSize) ||
    (search && (search.length > 200 || /[\x00-\x1f\x7f-\x9f]/.test(search))) ||
    (input.status !== null &&
      !["DRAFT", "PUBLISHED", "RETIRED"].includes(input.status)) ||
    (input.isActive !== null && typeof input.isActive !== "boolean")
  )
    throw new Error("Bộ lọc hoặc phân trang không hợp lệ.");
  if (input.roomTypeId !== null) positiveId(input.roomTypeId);
  return { ...input, search };
}
export function collectionParams(input: CollectionQuery): URLSearchParams {
  const query = collectionQuery(input);
  const params = new URLSearchParams({
    page: String(query.page),
    pageSize: String(query.pageSize),
  });
  for (const name of ["search", "isActive", "status", "roomTypeId"] as const)
    if (query[name] !== null) params.set(name, String(query[name]));
  return params;
}
export function validateCollection(
  value: CollectionPage,
  branch: number,
  query: CollectionQuery,
): CollectionPage {
  const id = (v: number) => Number.isSafeInteger(v) && v > 0;
  if (
    !value ||
    value.branchId !== branch ||
    !Number.isInteger(value.total) ||
    value.total < 0 ||
    value.total > 2147483647 ||
    !Array.isArray(value.items) ||
    value.items.length > query.pageSize ||
    value.items.length > value.total ||
    (Object.keys(query) as (keyof CollectionQuery)[]).some(
      (key) => value[key] !== query[key],
    ) ||
    value.items.some(
      (item) =>
        !item ||
        !id(item.id) ||
        item.branchId !== branch ||
        typeof item.code !== "string" ||
        typeof item.name !== "string" ||
        typeof item.active !== "boolean" ||
        !Array.isArray(item.roomTypeIds) ||
        item.roomTypeIds.some((v) => !id(v)) ||
        !Array.isArray(item.versions) ||
        item.versions.some(
          (v) =>
            !v ||
            !id(v.id) ||
            !Number.isInteger(v.number) ||
            v.number <= 0 ||
            !["DRAFT", "PUBLISHED", "RETIRED"].includes(v.status) ||
            typeof v.usedByInspection !== "boolean" ||
            (v.effectiveAt !== null &&
              (typeof v.effectiveAt !== "string" ||
                !Number.isFinite(Date.parse(v.effectiveAt)))) ||
            (v.publishedBy !== null && !id(v.publishedBy)),
        ),
    )
  )
    throw new Error("Invalid collection response");
  return value;
}
