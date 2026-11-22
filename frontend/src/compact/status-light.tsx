export default function StatusLight({
  active,
  label,
}: {
  active: boolean | null | undefined;
  label?: string;
}) {
  const text =
    label ||
    (active == null
      ? "Chưa xác định trạng thái"
      : active
        ? "Hoạt động"
        : "Ngừng hoạt động");
  return (
    <span
      className={`status-light ${active == null ? "unknown" : active ? "on" : "off"}`}
      role="img"
      aria-label={text}
      title={text}
      tabIndex={0}
    >
      <span className="status-tooltip" aria-hidden="true">
        {text}
      </span>
    </span>
  );
}
