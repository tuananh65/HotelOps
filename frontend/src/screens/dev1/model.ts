import * as C from "../../controllers/dev1";
import type * as D from "../../controllers/dev1/contracts";
import { id as safeId } from "../../controllers/dev1/transportController";
export type Kind =
  | "structure"
  | "floors"
  | "areas"
  | "roomTypes"
  | "rooms"
  | "departments"
  | "checklists"
  | "planning"
  | "inspection"
  | "cleaningProgress"
  | "taskProgress";
export const titles: Record<Kind, string> = {
  structure: "Cấu trúc chi nhánh",
  floors: "Tầng",
  areas: "Khu vực",
  roomTypes: "Loại phòng",
  rooms: "Phòng",
  departments: "Phòng ban",
  checklists: "Checklist kiểm tra",
  planning: "Phân công housekeeping",
  inspection: "Thực hiện kiểm tra phòng",
  cleaningProgress: "Tiến độ cleaning — UC032",
  taskProgress: "Tiến độ công việc — UC109",
};
export const routeKinds: Record<number, Kind> = {
  13: "structure",
  14: "structure",
  15: "floors",
  16: "floors",
  17: "roomTypes",
  18: "roomTypes",
  19: "rooms",
  20: "rooms",
  21: "departments",
  22: "departments",
  24: "cleaningProgress",
  25: "cleaningProgress",
  26: "cleaningProgress",
  27: "planning",
  28: "inspection",
  29: "inspection",
  31: "checklists",
  32: "checklists",
  33: "inspection",
  34: "inspection",
  35: "inspection",
  36: "inspection",
  37: "inspection",
};
export type RecordValue = Record<string, unknown>;
export function obj(v: unknown): RecordValue {
  return v && typeof v === "object" && !Array.isArray(v)
    ? (v as RecordValue)
    : {};
}
export interface Loaded {
  data: unknown;
  version?: C.Versioned<unknown>;
}
export async function load(
  kind: Kind,
  b: number,
  n: number | undefined,
  page: number,
  signal: AbortSignal,
): Promise<Loaded> {
  const o = { signal };
  if (kind === "structure")
    return { data: await C.hotelStructureController.structure(b, {}, o) };
  if (n) {
    let v: C.Versioned<unknown>;
    switch (kind) {
      case "floors":
        v = await C.hotelStructureController.floor(b, n, o);
        break;
      case "areas":
        v = await C.hotelStructureController.area(b, n, o);
        break;
      case "roomTypes":
        v = await C.roomTypeController.detail(b, n, o);
        break;
      case "rooms":
        v = await C.roomController.edit(b, n, o);
        break;
      case "departments":
        v = await C.departmentController.detail(b, n, o);
        break;
      case "checklists":
        v = await C.checklistController.detail(b, n, o);
        break;
      case "planning":
        v = await C.housekeepingController.assignmentOptions(b, n, {}, o);
        break;
      case "inspection":
        v = await C.housekeepingController.execution(b, n, o);
        break;
      case "cleaningProgress":
      case "taskProgress":
        v = await C.progressController.detail(
          b,
          n,
          kind === "cleaningProgress" ? "cleaning" : "task",
          o,
        );
        break;
      default:
        throw new Error("Không có nguồn dữ liệu cho màn hình này");
    }
    return { data: v.data, version: v };
  }
  switch (kind) {
    case "floors":
    case "areas": {
      const v = await C.hotelStructureController.structure(
        b,
        { collection: kind, page, pageSize: 20 },
        o,
      );
      return { data: v[kind] };
    }
    case "roomTypes":
      return {
        data: await C.roomTypeController.list(
          { branchId: b, page, pageSize: 20 },
          o,
        ),
      };
    case "rooms":
      return {
        data: await C.roomController.list(
          { branchId: b, page, pageSize: 20 },
          o,
        ),
      };
    case "departments": {
      const v = await C.departmentController.list(b, { page, pageSize: 20 }, o);
      return { data: v.departments };
    }
    default:
      return { data: null };
  }
}
export interface Field {
  key: string;
  label: string;
  type?: "number" | "checkbox" | "datetime-local" | "textarea";
  optional?: boolean;
  min?: number;
  max?: number;
}
const f = (
  key: string,
  label: string,
  type?: Field["type"],
  optional = false,
): Field => ({ key, label, type, optional });
const code = f("code", "Mã");
const name = f("name", "Tên");
const description = f("description", "Mô tả", "textarea", true);
const reason = f("reason", "Lý do", "textarea");
export const fields: Partial<Record<Kind, { create: Field[]; edit: Field[] }>> =
  {
    floors: {
      create: [
        code,
        name,
        f("floorNumber", "Số tầng", "number", true),
        description,
      ],
      edit: [code, name, description, reason],
    },
    areas: {
      create: [
        code,
        name,
        f("floorId", "ID tầng", "number", true),
        description,
      ],
      edit: [
        code,
        name,
        f("floorId", "ID tầng", "number", true),
        description,
        reason,
      ],
    },
    roomTypes: {
      create: [
        code,
        name,
        f("standardCleaningMinutes", "Thời gian dọn (phút)", "number"),
        f("defaultChecklistId", "ID checklist mặc định", "number"),
        f("standardCapacity", "Sức chứa", "number", true),
        description,
      ],
      edit: [
        code,
        name,
        f("standardCleaningMinutes", "Thời gian dọn (phút)", "number"),
        f("defaultChecklistId", "ID checklist mặc định", "number"),
        description,
      ],
    },
    rooms: {
      create: [
        f("roomNumber", "Số phòng"),
        f("floorId", "ID tầng", "number"),
        f("areaId", "ID khu vực", "number", true),
        f("roomTypeId", "ID loại phòng", "number"),
        f("notes", "Ghi chú", "textarea", true),
      ],
      edit: [
        f("roomNumber", "Số phòng"),
        f("floorId", "ID tầng", "number"),
        f("areaId", "ID khu vực", "number", true),
        f("roomTypeId", "ID loại phòng", "number"),
        f("notes", "Ghi chú", "textarea", true),
        reason,
      ],
    },
    departments: {
      create: [
        code,
        name,
        description,
        f("managerUserId", "ID người quản lý", "number", true),
      ],
      edit: [
        name,
        description,
        f("managerUserId", "ID người quản lý", "number", true),
      ],
    },
    checklists: {
      create: [
        code,
        name,
        description,
        f("roomTypeIds", "ID loại phòng (phân cách bằng dấu phẩy)"),
      ],
      edit: [reason],
    },
  };
export type Action =
  | "create"
  | "edit"
  | "deactivate"
  | "publish"
  | "retire"
  | "assign"
  | "assignInspection"
  | "start"
  | "finish"
  | "defect"
  | "rework"
  | "saveProgress"
  | "saveInspection";
export const actionLabels: Record<Action, string> = {
  create: "Thêm mới",
  edit: "Sửa",
  deactivate: "Vô hiệu hóa",
  publish: "Xuất bản",
  retire: "Ngừng sử dụng",
  assign: "Phân công dọn phòng",
  assignInspection: "Phân công kiểm tra",
  start: "Bắt đầu kiểm tra",
  finish: "Gửi kết quả kiểm tra",
  defect: "Ghi nhận lỗi",
  rework: "Yêu cầu làm lại",
  saveProgress: "Lưu tiến độ",
  saveInspection: "Lưu tiêu chí kiểm tra",
};
export const specialFields: Partial<Record<Action, Field[]>> = {
  deactivate: [reason, f("confirmation", "Xác nhận vô hiệu hóa", "checkbox")],
  publish: [reason, f("confirmation", "Xác nhận xuất bản", "checkbox")],
  retire: [reason, f("confirmation", "Xác nhận ngừng sử dụng", "checkbox")],
  assign: [
    f("assigneeId", "ID nhân viên được phân công", "number"),
    f("dueAt", "Hạn hoàn thành", "datetime-local"),
    reason,
  ],
  assignInspection: [
    f("inspectorId", "ID người kiểm tra", "number"),
    f("dueAt", "Hạn hoàn thành", "datetime-local"),
    reason,
  ],
  start: [reason],
  finish: [reason],
  defect: [
    f("itemId", "ID tiêu chí bị lỗi", "number"),
    f("severityId", "ID mức độ lỗi", "number"),
    f("description", "Mô tả lỗi", "textarea"),
    f("location", "Vị trí"),
  ],
  rework: [
    f("assigneeId", "ID nhân viên", "number"),
    f("dueAt", "Hạn hoàn thành", "datetime-local"),
    f("defectIds", "ID lỗi (phân cách bằng dấu phẩy)"),
    reason,
  ],
  saveProgress: [
    { key: "percent", label: "Tiến độ (%)", type: "number", min: 0, max: 100 },
    f("note", "Ghi chú", "textarea", true),
  ],
  saveInspection: [reason],
};
export function ids(value: unknown): number[] {
  const result = String(value ?? "")
    .split(",")
    .map((x) => Number(x.trim()));
  result.forEach(safeId);
  return result;
}
export async function save(
  kind: Kind,
  action: Action,
  b: number,
  n: number | undefined,
  v: C.Versioned<unknown> | undefined,
  input: RecordValue,
) {
  if (action === "create")
    switch (kind) {
      case "floors":
        return C.hotelStructureController.createFloor(
          b,
          input as unknown as D.CreateFloorRequest,
        );
      case "areas":
        return C.hotelStructureController.createArea(
          b,
          input as unknown as D.CreateAreaRequest,
        );
      case "roomTypes":
        return C.roomTypeController.create({
          ...input,
          branchId: b,
        } as unknown as D.CreateRoomTypeRequest);
      case "rooms":
        return C.roomController.create({
          ...input,
          branchId: b,
        } as unknown as D.CreateRoomRequest);
      case "departments":
        return C.departmentController.create(
          b,
          input as unknown as D.CreateDepartmentRequest,
        );
      case "checklists":
        return C.checklistController.create(b, {
          ...input,
          roomTypeIds: ids(input.roomTypeIds),
        } as unknown as D.CreateChecklistRequest);
    }
  if (!n || !v) throw new Error("Hãy mở chi tiết mới nhất trước khi thao tác.");
  if (input.dueAt)
    input = { ...input, dueAt: new Date(String(input.dueAt)).toISOString() };
  // Each branch narrows the snapshot to the DTO returned by that exact detail loader.
  if (action === "edit")
    switch (kind) {
      case "floors":
        return C.hotelStructureController.updateFloor(
          b,
          n,
          v as C.Versioned<D.EditableStructureDto>,
          input as unknown as D.UpdateFloorRequest,
        );
      case "areas":
        return C.hotelStructureController.updateArea(
          b,
          n,
          v as C.Versioned<D.EditableStructureDto>,
          input as unknown as D.UpdateAreaRequest,
        );
      case "roomTypes":
        return C.roomTypeController.update(
          b,
          n,
          v as C.Versioned<D.RoomTypeDetailsDto>,
          input as unknown as D.UpdateRoomTypeRequest,
        );
      case "rooms":
        return C.roomController.update(
          b,
          n,
          v as C.Versioned<D.CreatedRoomDto>,
          input as unknown as D.UpdateRoomInformationRequest,
        );
      case "departments":
        return C.departmentController.update(
          b,
          n,
          v as C.Versioned<D.DepartmentInformationDto>,
          input as unknown as D.UpdateDepartmentRequest,
        );
      case "checklists":
        return C.checklistController.update(
          b,
          n,
          v as C.Versioned<D.ChecklistDetailsDto>,
          input as unknown as D.UpdateChecklistRequest,
        );
    }
  if (kind === "roomTypes" && action === "deactivate")
    return C.roomTypeController.deactivate(
      b,
      n,
      v as C.Versioned<D.RoomTypeDetailsDto>,
      input as unknown as D.DeactivateRoomTypeRequest,
    );
  if (kind === "checklists" && (action === "publish" || action === "retire"))
    return C.checklistController[action](
      b,
      n,
      v as C.Versioned<D.ChecklistDetailsDto>,
      input as unknown as D.ChecklistTransitionRequest,
    );
  if (kind === "planning" && action === "assign")
    return C.housekeepingController.assignCleaning(
      b,
      n,
      v as C.Versioned<D.HousekeepingPlanningDto>,
      input as unknown as D.AssignCleaningRequest,
    );
  if (kind === "planning" && action === "assignInspection")
    return C.housekeepingController.assignInspection(
      b,
      n,
      input as unknown as D.AssignInspectionRequest,
    );
  if (action === "saveProgress")
    return C.progressController.save(
      b,
      n,
      kind === "cleaningProgress" ? "cleaning" : "task",
      v as C.Versioned<D.WorkProgressDto>,
      input as unknown as D.SaveProgressRequest,
    );
  const inspection = v as C.Versioned<D.InspectionExecutionDto>;
  if (kind === "inspection")
    switch (action) {
      case "start":
        return C.housekeepingController.startInspection(
          b,
          n,
          inspection,
          String(input.reason),
        );
      case "finish":
        return C.housekeepingController.finishInspection(
          b,
          n,
          inspection,
          String(input.reason),
        );
      case "saveInspection":
        return C.housekeepingController.saveInspection(
          b,
          n,
          inspection,
          input as unknown as D.SaveInspectionRequest,
        );
      case "defect":
        return C.housekeepingController.recordDefect(
          b,
          n,
          inspection,
          input as unknown as D.RecordInspectionDefectRequest,
        );
      case "rework":
        return C.housekeepingController.requestRework(b, n, inspection, {
          ...input,
          defectIds: ids(input.defectIds),
        } as unknown as D.RequestReworkRequest);
    }
  throw new Error("Thao tác chưa được backend hỗ trợ ở màn hình này.");
}
