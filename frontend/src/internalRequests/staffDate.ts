export function staffDate(value: string | null | undefined): string {
  if (!value) return "Chưa có thời hạn";
  const date = new Date(value);
  return Number.isFinite(date.getTime()) ? new Intl.DateTimeFormat('vi-VN', {
    timeZone:'Asia/Ho_Chi_Minh', day:'2-digit',month:'2-digit',year:'numeric',hour:'2-digit',minute:'2-digit',
  }).format(date) : "Chưa có thời hạn";
}
