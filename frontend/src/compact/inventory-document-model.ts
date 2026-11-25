export const documentNames: Record<string, string> = {
  RECEIPT: "Nhập kho",
  ISSUE: "Xuất kho",
  RETURN: "Trả vật tư",
  TRANSFER: "Chuyển kho",
  TRANSFER_RECEIPT: "Nhận chuyển kho",
  TRANSFER_REQUEST: "Đề nghị chuyển kho",
  MATERIAL_REQUEST: "Yêu cầu vật tư",
  STOCKTAKE: "Kiểm kê",
  ADJUSTMENT: "Điều chỉnh",
  REVERSAL: "Đảo giao dịch",
  REPLENISHMENT: "Bổ sung vật tư",
  REUSABLE_ISSUE: "Bàn giao vật tư tái sử dụng",
  REUSABLE_RETURN: "Thu hồi vật tư tái sử dụng",
};
export const documentPermission: Record<string, string> = {
  RECEIPT: "STOCK_RECEIPT_POST",
  ISSUE: "STOCK_ISSUE_POST",
  RETURN: "STOCK_RETURN_POST",
  TRANSFER: "STOCK_TRANSFER_POST",
  TRANSFER_RECEIPT: "STOCK_TRANSFER_POST",
  TRANSFER_REQUEST: "STOCK_TRANSFER_POST",
  MATERIAL_REQUEST: "MR_CREATE",
  STOCKTAKE: "STOCKTAKE_PERFORM",
  ADJUSTMENT: "STOCK_ADJUST_POST",
  REVERSAL: "STOCK_REVERSE",
  REPLENISHMENT: "REPLENISHMENT_CREATE",
};
export function documentRoute(kind: string) {
  return kind === "MATERIAL_REQUEST"
    ? "fulfillment"
    : kind === "REPLENISHMENT"
      ? "replenishment"
      : ["STOCKTAKE", "ADJUSTMENT", "REVERSAL"].includes(kind)
        ? "stocktaking"
        : "transactions";
}
export const documentHref = (doc: Record<string, any>) =>
  `#${documentRoute(doc.kind)}?kind=${doc.kind}&id=${doc.documentId}`;
export const inventoryGroups = {
  transactions: ["RECEIPT", "ISSUE", "RETURN", "TRANSFER", "TRANSFER_RECEIPT"],
  fulfillment: ["MATERIAL_REQUEST"],
  stocktaking: ["STOCKTAKE", "ADJUSTMENT", "REVERSAL"],
  replenishment: ["REPLENISHMENT"],
  history: Object.keys(documentNames),
};
export type InventoryGroup = keyof typeof inventoryGroups;
