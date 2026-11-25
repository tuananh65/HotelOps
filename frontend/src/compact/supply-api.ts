import { ApiError, api, message } from "./api";

export type SupplyGroup = "A" | "B" | "C";
export type SupplyConfiguration = {
  configurationId: number; branchId: number; itemId: number | null; assetId: number | null;
  code: string; name: string; supplyGroup: SupplyGroup; warehouseId: number | null;
  unitId: number | null; unit: string | null; initialQuantity: number; minimumStock: number;
  perGuestQuantity: number; isActive: boolean; revision: number; updatedBy: number;
  updatedAt: string; rowVersion: string;
};
export type SaveSupplyConfiguration = Pick<SupplyConfiguration, "branchId" | "itemId" | "assetId" | "supplyGroup" | "warehouseId" | "unitId" | "initialQuantity" | "minimumStock" | "perGuestQuantity" | "isActive"> & { rowVersion: string | null };
export type SupplyBalance = {
  balanceId: number; itemId: number; code: string; name: string; unitId: number; warehouseId: number;
  initialTarget: number; initialIssued: number; additionalIssued: number; reserved: number;
  used: number; returned: number; remaining: number; available: number; rowVersion: string;
};
export type SupplyAllocation = {
  allocationId: number; branchId: number; userId: number; assignmentId: number;
  status: string; shortageReason: string | null; createdAt: string; closedAt: string | null;
  rowVersion: string; items: SupplyBalance[];
};
export type SupplyLedger = {
  ledgerId: number; itemId: number; kind: string; quantity: number; workId: number | null;
  reservationId: number | null; documentLineId: number | null; actorUserId: number; occurredAt: string;
};
export const supplyGroups: Record<SupplyGroup, string> = {
  A: "A · Vật tư cấp theo công việc", B: "B · Đồ dùng bền", C: "C · Thiết bị sửa chữa bên ngoài",
};
export const supplyQuantity = (value: number) => value.toLocaleString("vi-VN", { maximumFractionDigits: 4 });
export const saveSupplyConfiguration = (body: SaveSupplyConfiguration) => api<SupplyConfiguration>("supplies/configuration", body, "PUT");
export function supplyMessage(error: unknown): string {
  const code = error instanceof ApiError ? error.code : typeof error === "string" ? error : "";
  const labels: Record<string, string> = {
    STALE_ROW_VERSION: "Cấu hình đã thay đổi. Đóng biểu mẫu và tải lại trước khi sửa.",
    SUPPLY_CONFIGURATION_REQUIRED: "Chi nhánh chưa cấu hình định mức vật tư cho phòng.",
    STOCK_INSUFFICIENT: "Kho chưa đủ vật tư để cấp. Gửi yêu cầu bổ sung để được xử lý.",
    INVALID_SUPPLY_QUANTITY: "Số lượng phải không âm và có tối đa bốn chữ số thập phân.",
    FILL_SUPPLY_CONFIGURATION_INCOMPLETE: "Chọn đơn vị và nhập định mức cho mỗi khách lớn hơn không.",
    BRANCH_REQUIRES_ONE_ACTIVE_WAREHOUSE: "Chi nhánh phải có đúng một kho hoạt động. Vui lòng kiểm tra cấu hình kho của chi nhánh.",
    SUPPLY_BASE_UNIT_REQUIRED: "Sử dụng đúng đơn vị cơ sở của vật tư.",
    WAREHOUSE_UNAVAILABLE: "Kho đã ngừng hoạt động hoặc không thuộc chi nhánh này.",
    ITEM_UNAVAILABLE: "Vật tư không còn hoạt động.",
    ASSET_UNAVAILABLE: "Tài sản không còn hoạt động tại chi nhánh này.",
    SUPPLY_RESERVATIONS_OPEN: "Còn vật tư giữ cho công việc. Giải quyết công việc trước khi kết thúc ca.",
    SUPPLY_OWNER_REQUIRED: "Bạn chỉ được xem vật tư của mình.",
  };
  return labels[code.split(":")[0]] || (typeof error === "string" ? "Không thể xử lý vật tư. Vui lòng tải lại hoặc liên hệ giám sát." : message(error));
}
