// Fixed presentation keys keep the same state visually stable across modules.
const hues: Record<string, number> = {
  "Bản nháp": 215,
  "Chưa bắt đầu": 218,
  "Chờ tiếp nhận": 38,
  "Đang mở": 28,
  "Đang thực hiện": 210,
  "Đang làm việc": 200,
  "Đã tiếp nhận": 185,
  "Chờ duyệt": 48,
  "Chờ phê duyệt": 48,
  "Đã duyệt": 143,
  "Đã hoàn tất": 164,
  "Đã hoàn thành": 164,
  "Đã đóng": 265,
  "Đã kết thúc": 260,
  "Đã hủy": 345,
  "Từ chối": 0,
  "Đã từ chối": 0,
  "Tạm dừng": 285,
  "Đang tạm dừng": 285,
  "Cần bổ sung thông tin": 315,
  "Đã cấp một phần": 60,
  "Đã cấp": 123,
  "Đã cấp đủ": 130,
  "Đã nhận": 175,
  "Đang vận chuyển": 235,
  "Quá hạn": 4,
  "Đã lên lịch": 195,
  "Chưa xác định": 220,
};
export function statusColor(label: string) {
  const hash = Array.from(label).reduce(
    (value, c) => (value * 31 + c.charCodeAt(0)) >>> 0,
    0,
  );
  const hue = hues[label] ?? hash % 360;
  return {
    color: `hsl(${hue} 68% 28%)`,
    backgroundColor: `hsl(${hue} 75% 94%)`,
    borderColor: `hsl(${hue} 55% 78%)`,
  };
}
