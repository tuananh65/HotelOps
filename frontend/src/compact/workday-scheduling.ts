import { ApiError, dateKey, message } from "./api";

export type CalendarMarker = {
  date: string;
  name: string;
  kind: string;
  source: string;
};
export type WorkdayCalendar = { markers: CalendarMarker[]; notice: string };
export const shiftTime = (minutes: number) =>
  `${String(Math.floor(minutes / 60)).padStart(2, "0")}:${String(minutes % 60).padStart(2, "0")}`;

export function monthDates(date: string, today: string): string[] {
  const first = new Date(`${date.slice(0, 7)}-01T12:00:00`);
  const last = new Date(first.getFullYear(), first.getMonth() + 1, 0).getDate();
  return Array.from({ length: last }, (_, i) =>
    dateKey(new Date(first.getFullYear(), first.getMonth(), i + 1, 12)),
  ).filter((day) => day >= today);
}
export function weekDates(date: string, today: string): string[] {
  const first = new Date(`${date}T12:00:00`);
  first.setDate(first.getDate() - (first.getDay() + 6) % 7);
  return Array.from({ length: 7 }, (_, i) =>
    dateKey(new Date(first.getFullYear(), first.getMonth(), first.getDate() + i, 12)),
  ).filter((day) => day >= today);
}
export type HourWarning = { period: "DAY" | "WEEK"; from: string; to: string; existingMinutes: number; addedMinutes: number; totalMinutes: number; limitMinutes: number };
export const hoursText = (minutes: number) => `${Math.floor(minutes / 60)} giờ${minutes % 60 ? ` ${minutes % 60} phút` : ""}`;

const errors: Record<string, string> = {
  SHIFT_DATES_REQUIRED: "Chọn từ 1 đến 31 ngày để phân ca.",
  SHIFT_DUPLICATE_DATES: "Danh sách ngày có ngày bị lặp.",
  SHIFT_ONE_MONTH_REQUIRED: "Chọn các ngày trong cùng một tháng.",
  SHIFT_ONE_MONTH_OR_WEEK_REQUIRED: "Chọn các ngày trong cùng một tuần hoặc một tháng.",
  SHIFT_HOURS_CONFIRMATION_REQUIRED: "Lịch vượt 8 giờ/ngày hoặc 40 giờ/tuần, hoặc số giờ đã thay đổi. Hãy xem lại cảnh báo và xác nhận trước khi lưu.",
  SHIFT_ROLE_DEPARTMENT_MISMATCH: "Đối tượng không thuộc bộ phận đã chọn hoặc không còn nhân viên đủ điều kiện. Hãy chọn lại.",
  SHIFT_EMPLOYEES_REQUIRED: "Chọn từ 1 đến 100 nhân viên, không trùng người.",
  SHIFT_HOLIDAY_ACKNOWLEDGEMENT_REQUIRED:
    "Xác nhận đã xem các mốc lễ trong những ngày phân ca.",
  SHIFT_NAME_REQUIRED: "Nhập tên ca, tối đa 150 ký tự.",
  INVALID_SHIFT_DEFINITION:
    "Giờ kết thúc phải sau giờ bắt đầu trong cùng ngày; sức chứa từ 1 đến 100.",
  SHIFT_HISTORY_DEFINITION_LOCKED:
    "Ca đã có lịch sử: giữ bộ phận, đối tượng và giờ ca. Hãy tạo ca mới nếu cần thay đổi.",
  SHIFT_DEPARTMENT_INVALID: "Bộ phận phải đang hoạt động trong chi nhánh này.",
  SHIFT_ROLE_INVALID: "Đối tượng phải đang hoạt động và có quyền ghi nhận ca.",
  SHIFT_HAS_OPEN_ASSIGNMENTS:
    "Ca còn phân công chưa kết thúc. Hãy xử lý các phân công trước khi ngừng ca.",
  SHIFT_CAPACITY_BELOW_ASSIGNED:
    "Sức chứa mới thấp hơn số người đã phân trong một ngày.",
  SHIFT_DEFINITION_EXISTS: "Đã có ca cùng bộ phận, đối tượng và khung giờ.",
  SHIFT_EMPLOYEE_NOT_ELIGIBLE:
    "Có người không còn đủ điều kiện hoặc trùng lịch trong những ngày đã chọn. Hãy tải lại ứng viên.",
};
export const workdayMessage = (error: unknown) =>
  error instanceof ApiError && errors[error.code]
    ? errors[error.code]
    : message(error);
