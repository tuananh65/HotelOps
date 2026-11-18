import { decimalId } from "./contracts";
import type {
  Context,
  MutationKind,
  MutationRequests,
  ReadKind,
} from "./contracts";
export type Screen =
  "structure" | "floors" | "areas" | "roomTypes" | "rooms" | "departments";
export type Action =
  "browse" | "create" | "edit" | "detail" | "deactivate" | "history";
export interface Field {
  key: string;
  label: string;
  type?: "id" | "integer" | "text" | "longtext";
  required?: boolean;
  max?: number;
}
const code: Field = { key: "code", label: "Mã", required: true, max: 30 };
const name: Field = { key: "name", label: "Tên", required: true, max: 150 };
const description: Field = {
  key: "description",
  label: "Mô tả",
  type: "longtext",
  max: 4000,
};
const reason: Field = {
  key: "reason",
  label: "Lý do",
  required: true,
  max: 1000,
};
const floorId: Field = {
  key: "floorId",
  label: "Mã tầng",
  type: "id",
  required: true,
};
const areaId: Field = {
  key: "areaId",
  label: "Mã khu vực (không bắt buộc)",
  type: "id",
};
const typeId: Field = {
  key: "roomTypeId",
  label: "Mã loại phòng",
  type: "id",
  required: true,
};
const number: Field = {
  key: "roomNumber",
  label: "Số phòng",
  required: true,
  max: 30,
};
const notes: Field = {
  key: "notes",
  label: "Ghi chú",
  type: "longtext",
  max: 4000,
};
const manager: Field = {
  key: "managerUserId",
  label: "Mã người quản lý (không bắt buộc)",
  type: "id",
};
const cleaning: Field = {
  key: "standardCleaningMinutes",
  label: "Thời gian vệ sinh tiêu chuẩn (phút)",
  type: "id",
  required: true,
};
const checklist: Field = {
  key: "defaultChecklistId",
  label: "Mã danh sách kiểm tra mặc định đã công bố",
  type: "id",
  required: true,
};
export const fields: Record<MutationKind, Field[]> = {
  createFloor: [
    code,
    { ...name, max: 100 },
    { key: "floorNumber", label: "Số tầng (không bắt buộc)", type: "integer" },
    description,
  ],
  createArea: [
    code,
    name,
    { ...floorId, label: "Mã tầng cha (không bắt buộc)", required: false },
    description,
  ],
  updateFloor: [code, { ...name, max: 100 }, description, reason],
  updateArea: [
    code,
    name,
    { ...floorId, label: "Mã tầng cha (không bắt buộc)", required: false },
    description,
    reason,
  ],
  createType: [
    code,
    name,
    cleaning,
    checklist,
    {
      key: "standardCapacity",
      label: "Sức chứa tiêu chuẩn (không bắt buộc)",
      type: "id",
    },
    description,
  ],
  updateType: [
    { ...code, label: "Mã (để trống nếu giữ nguyên)", required: false },
    name,
    cleaning,
    checklist,
    description,
  ],
  deactivateType: [reason],
  createRoom: [number, floorId, areaId, typeId, notes],
  updateRoom: [number, floorId, areaId, typeId, notes, reason],
  createDepartment: [code, name, description, manager],
  updateDepartment: [name, description, manager],
};
export const titles: Record<Screen, string> = {
  structure: "Cấu trúc chi nhánh",
  floors: "Tầng",
  areas: "Khu vực",
  roomTypes: "Loại phòng",
  rooms: "Danh mục phòng",
  departments: "Bộ phận",
};
export function mutationFor(
  screen: Screen,
  action: Action,
): MutationKind | null {
  if (action === "deactivate")
    return screen === "roomTypes" ? "deactivateType" : null;
  if (action !== "create" && action !== "edit") return null;
  const noun = {
    structure: null,
    floors: "Tầng",
    areas: "Khu vực",
    roomTypes: "Loại",
    rooms: "Phòng",
    departments: "Bộ phận",
  }[screen];
  return noun
    ? (`${action === "edit" ? "update" : "create"}${noun}` as MutationKind)
    : null;
}
export function readFor(screen: Screen, action: Action): ReadKind {
  if (action === "browse")
    return screen === "roomTypes"
      ? "roomTypes"
      : screen === "rooms"
        ? "rooms"
        : "structure";
  return (
    {
      structure: "structure",
      floors: "floor",
      areas: "area",
      roomTypes: "roomType",
      rooms:
        action === "edit"
          ? "roomEdit"
          : action === "history"
            ? "roomHistory"
            : "roomDetail",
      departments: "department",
    } as const
  )[screen];
}
export function draftRequest<K extends MutationKind>(
  kind: K,
  draft: Record<string, string>,
  context: Context,
): MutationRequests[K] | null {
  const output: Record<string, unknown> = {};
  for (const field of fields[kind]) {
    const raw = draft[field.key] ?? "";
    if (kind === "updateType" && field.key === "code" && raw === "") continue;
    if (field.key === "reason" && raw.trim().length < 10) return null;
    if (field.required && !raw.trim()) return null;
    if (field.type === "id") {
      const n = raw === "" && !field.required ? null : decimalId(raw);
      if (n === null && raw !== "") return null;
      if (field.required && n === null) return null;
      if (
        ["standardCleaningMinutes", "standardCapacity"].includes(field.key) &&
        n !== null &&
        n > 2147483647
      )
        return null;
      output[field.key] = n;
    } else if (field.type === "integer") {
      if (raw !== "" && !/^-?[0-9]+$/.test(raw)) return null;
      const n = raw === "" ? null : Number(raw);
      if (
        n !== null &&
        (!Number.isInteger(n) || n < -2147483648 || n > 2147483647)
      )
        return null;
      output[field.key] = n;
    } else {
      if (field.max && raw.length > field.max) return null;
      output[field.key] = raw === "" && !field.required ? null : raw;
    }
  }
  if (kind === "createType" || kind === "createRoom")
    output.branchId = context.branchId;
  if (kind === "deactivateType") output.confirmation = true;
  return output as unknown as MutationRequests[K];
}
