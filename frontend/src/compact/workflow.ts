export const kinds = [
  ["CLEANING", "Dọn phòng", "CLEANING_VIEW", "CLEANING_ASSIGN"],
  ["INSPECTION", "Kiểm tra phòng", "INSPECTION_VIEW", "INSPECTION_PERFORM"],
  ["REWORK", "Làm lại", "CLEANING_VIEW", "CLEANING_ASSIGN"],
  ["INCIDENT", "Sự cố", "INCIDENT_VIEW", "INCIDENT_CREATE"],
  ["MAINTENANCE", "Bảo trì", "MAINTENANCE_VIEW", "MAINTENANCE_ASSIGN"],
  ["OPERATIONAL", "Công việc", "TASK_VIEW", "TASK_MANAGE"],
  ["INTERNAL_REQUEST", "Yêu cầu nội bộ", "IR_VIEW", "IR_CREATE"],
];
export const commands: Record<string, string> = {
  progress: "Cập nhật tiến độ",
  accept: "Nhận việc",
  start: "Bắt đầu",
  pause: "Tạm dừng",
  resume: "Tiếp tục",
  complete: "Gửi hoàn thành",
  verify: "Xác nhận hoàn tất",
  pass: "Kết luận đạt",
  fail: "Kết luận không đạt",
  assign: "Phân công",
  cancel: "Hủy công việc",
  reopen: "Mở lại",
  submit: "Gửi yêu cầu",
  route: "Chuyển bộ phận",
  reject: "Từ chối",
  triage: "Phân loại sự cố",
  escalate: "Báo cáo vượt cấp",
  "handover-offer": "Đề nghị bàn giao",
  "handover-accept": "Nhận bàn giao",
  "handover-reject": "Từ chối bàn giao",
};
export function permission(kind: string, action: string) {
  if (["CLEANING", "REWORK"].includes(kind))
    return ["create", "assign", "cancel"].includes(action)
      ? "CLEANING_ASSIGN"
      : "CLEANING_EXECUTE";
  if (kind === "INSPECTION") return "INSPECTION_PERFORM";
  if (kind === "OPERATIONAL")
    return ["create", "assign", "verify", "cancel", "reopen"].includes(action)
      ? "TASK_MANAGE"
      : "TASK_EXECUTE";
  if (kind === "MAINTENANCE")
    return ["verify", "reopen"].includes(action)
      ? "MAINTENANCE_REVIEW"
      : ["create", "assign", "cancel"].includes(action)
        ? "MAINTENANCE_ASSIGN"
        : "MAINTENANCE_EXECUTE";
  if (kind === "INTERNAL_REQUEST")
    return ["create", "submit", "verify", "reopen"].includes(action)
      ? "IR_CREATE"
      : action === "escalate"
        ? "IR_OVERDUE_HANDLE"
        : "IR_PROCESS";
  return action === "triage"
    ? "INCIDENT_TRIAGE"
    : action === "escalate"
      ? "INCIDENT_ESCALATION_HANDLE"
      : "INCIDENT_MANAGE";
}
export const terminal = [
  "CLOSED",
  "COMPLETED",
  "CANCELLED",
  "PASSED",
  "FAILED",
  "REJECTED",
];
export function nextCommands(
  kind: string,
  status: string,
  assigned: boolean,
  accepted: boolean,
  creator: boolean,
) {
  const result: string[] = [];
  if (assigned) {
    if (
      !accepted &&
      ((["CLEANING", "INSPECTION", "OPERATIONAL"].includes(kind) &&
        status === "ASSIGNED") ||
        (["REWORK", "INTERNAL_REQUEST"].includes(kind) &&
          status === "PENDING_ACCEPTANCE") ||
        (kind === "MAINTENANCE" && status === "PENDING") ||
        (kind === "INCIDENT" && status === "OPEN"))
    )
      result.push("accept");
    if (
      accepted &&
      ((["CLEANING", "INSPECTION"].includes(kind) && status === "ASSIGNED") ||
        (["REWORK", "OPERATIONAL", "INTERNAL_REQUEST"].includes(kind) &&
          status === "ACCEPTED") ||
        (kind === "MAINTENANCE" && status === "PENDING") ||
        (kind === "INCIDENT" && status === "OPEN"))
    )
      result.push("start");
    if (["IN_PROGRESS", "INSPECTING"].includes(status)) result.push("complete");
    if (
      status === "IN_PROGRESS" &&
      ["CLEANING", "REWORK", "MAINTENANCE", "OPERATIONAL"].includes(kind)
    )
      result.push("pause");
    if (["PAUSED", "ON_HOLD"].includes(status)) result.push("resume");
    if (kind === "INSPECTION" && status === "PENDING_CONCLUSION")
      result.push("pass", "fail");
  }
  if (
    (kind === "OPERATIONAL" && status === "PENDING_VERIFICATION") ||
    (kind === "MAINTENANCE" && status === "COMPLETED") ||
    (kind === "INCIDENT" && status === "RESOLVED") ||
    (kind === "INTERNAL_REQUEST" && status === "COMPLETED" && creator)
  )
    result.push("verify");
  if (
    (["CLOSED", "PENDING_VERIFICATION", "RESOLVED"].includes(status) ||
      (kind === "MAINTENANCE" && status === "COMPLETED")) &&
    ["OPERATIONAL", "MAINTENANCE", "INCIDENT"].includes(kind)
  )
    result.push("reopen");
  if (kind === "INTERNAL_REQUEST" && creator && status === "DRAFT")
    result.push("submit");
  if (kind === "INTERNAL_REQUEST" && status === "SUBMITTED")
    result.push("route");
  if (!terminal.includes(status)) result.push("assign", "cancel");
  if (kind === "INCIDENT" && !terminal.includes(status))
    result.push("triage", "escalate");
  return result;
}
