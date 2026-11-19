/** Display-only formatting. Never use this value for API decisions or validation. */
export function userMessage<T>(value: T): T {
  if (typeof value !== "string") return value;
  const messages: Record<string, string> = {
    MSG08: "Đã lưu thay đổi.",
    MSG09: "Đã gửi thành công.",
    MSG03: "Vui lòng kiểm tra thông tin đã nhập.",
    MSG02: "Bạn chưa có quyền thực hiện thao tác này.",
    MSG10: "Không có dữ liệu phù hợp.",
  };
  if (messages[value]) return messages[value] as T;
  if (/^(?:MSG\d+|[A-Z]+(?:_[A-Z0-9]+)+)$/.test(value))
    return "Không thể hoàn tất thao tác. Vui lòng tải lại và thử lại." as T;
  return value
    .replace(/\s*\(MSG\d+\)/g, "")
    .replace(/\bMSG\d+\s*[:—–-]?\s*/g, "")
    .replace(/^[A-Z]+(?:_[A-Z0-9]+)+\s*:\s*/, "")
    .trim() as T;
}
