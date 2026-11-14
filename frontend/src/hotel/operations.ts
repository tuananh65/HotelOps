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
const code: Field = { key: "code", label: "Code", required: true, max: 30 };
const name: Field = { key: "name", label: "Name", required: true, max: 150 };
const description: Field = {
  key: "description",
  label: "Description",
  type: "longtext",
  max: 4000,
};
const reason: Field = {
  key: "reason",
  label: "Reason",
  required: true,
  max: 1000,
};
const floorId: Field = {
  key: "floorId",
  label: "Floor ID",
  type: "id",
  required: true,
};
const areaId: Field = {
  key: "areaId",
  label: "Area ID (optional)",
  type: "id",
};
const typeId: Field = {
  key: "roomTypeId",
  label: "Room type ID",
  type: "id",
  required: true,
};
const number: Field = {
  key: "roomNumber",
  label: "Room number",
  required: true,
  max: 30,
};
const notes: Field = {
  key: "notes",
  label: "Notes",
  type: "longtext",
  max: 4000,
};
const manager: Field = {
  key: "managerUserId",
  label: "Manager user ID (optional)",
  type: "id",
};
const cleaning: Field = {
  key: "standardCleaningMinutes",
  label: "Standard cleaning minutes",
  type: "id",
  required: true,
};
const checklist: Field = {
  key: "defaultChecklistId",
  label: "Published default checklist ID",
  type: "id",
  required: true,
};
export const fields: Record<MutationKind, Field[]> = {
  createFloor: [
    code,
    { ...name, max: 100 },
    { key: "floorNumber", label: "Floor number (optional)", type: "integer" },
    description,
  ],
  createArea: [
    code,
    name,
    { ...floorId, label: "Parent floor ID (optional)", required: false },
    description,
  ],
  updateFloor: [code, { ...name, max: 100 }, description, reason],
  updateArea: [
    code,
    name,
    { ...floorId, label: "Parent floor ID (optional)", required: false },
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
      label: "Standard capacity (optional)",
      type: "id",
    },
    description,
  ],
  updateType: [
    { ...code, label: "Code (leave blank to keep)", required: false },
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
  structure: "Branch structure",
  floors: "Floors",
  areas: "Areas",
  roomTypes: "Room types",
  rooms: "Room directory",
  departments: "Departments",
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
    floors: "Floor",
    areas: "Area",
    roomTypes: "Type",
    rooms: "Room",
    departments: "Department",
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
