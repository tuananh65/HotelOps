import type { ReactNode } from "react";
import { AppButton, AppDrawer } from "../../components/design-system";
import { displayCode } from "../../i18n/vi";
import "./operational-read.css";

export function ReadDrawer({
  title,
  onClose,
  children,
}: {
  title: string;
  onClose: () => void;
  children: ReactNode;
}) {
  return (
    <AppDrawer
      open
      title={title}
      onClose={onClose}
      size="large"
      footer={<AppButton onClick={onClose}>Đóng</AppButton>}
    >
      <div className="ops-detail">{children}</div>
    </AppDrawer>
  );
}
export function Fact({
  label,
  children,
}: {
  label: string;
  children: ReactNode;
}) {
  return (
    <div>
      <dt>{label}</dt>
      <dd>{children ?? "Chưa ghi nhận"}</dd>
    </div>
  );
}
export function operationalLabel(value: unknown): string {
  const labels: Record<string, string> = {
    TODO: "Chưa bắt đầu",
    PASS: "Đạt",
    FAIL: "Không đạt",
    CONDITIONAL: "Đạt có điều kiện",
    RECORDED: "Đã ghi nhận",
    NOT_RECORDED: "Chưa ghi nhận",
    UNRESOLVED: "Chưa xác minh",
    OUT_OF_SYNC: "Chưa khớp với trạng thái phòng",
    VERIFIED_SAME_ROOM: "Đã xác minh đúng phòng",
    RELEASE: "Xác nhận sẵn sàng đón khách",
    READY: "Chờ xác nhận đón khách",
    AVAILABLE: "Khả dụng",
    INSPECTION: "Kiểm tra phòng",
    CLEANING_TASK: "Công việc vệ sinh",
    ROOM_BLOCK: "Khóa phòng",
    MAINTENANCE: "Bảo trì",
    UNKNOWN: "Chưa xác định",
    CLEANING: "Đang vệ sinh",
    OCCUPIED: "Có khách",
  };
  return value == null || value === ""
    ? "Chưa ghi nhận"
    : (labels[String(value)] ?? displayCode(value));
}
/** Unspecified SQL timestamps retain their recorded wall time; never invent a timezone. */
export function recordedTime(value: string | null | undefined) {
  if (!value) return "Chưa ghi nhận";
  if (!/[zZ]$|[+-]\d{2}:\d{2}$/.test(value)) {
    const match = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}:\d{2})/.exec(value);
    return match
      ? `${match[4]} ${match[3]}/${match[2]}/${match[1]}`
      : "Chưa xác định";
  }
  const time = new Date(value);
  return Number.isFinite(time.getTime())
    ? new Intl.DateTimeFormat("vi-VN", {
        timeZone: "Asia/Ho_Chi_Minh",
        dateStyle: "short",
        timeStyle: "short",
      }).format(time)
    : "Chưa xác định";
}

export function hotelInputToUtc(value: string): string | null {
  if (!value) return null;
  if (!/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}$/.test(value))
    throw new Error("Ngày giờ không hợp lệ.");
  const parsed = new Date(`${value}:00+07:00`);
  if (!Number.isFinite(parsed.getTime()))
    throw new Error("Ngày giờ không hợp lệ.");
  const result = parsed.toISOString();
  if (utcToHotelInput(result) !== value)
    throw new Error("Ngày giờ không hợp lệ.");
  return result;
}
export function utcToHotelInput(value: string | null | undefined): string {
  if (!value) return "";
  const at = Date.parse(value);
  return Number.isFinite(at)
    ? new Date(at + 7 * 60 * 60 * 1000).toISOString().slice(0, 16)
    : "";
}
