import "./loading-indicator.css";

export function LoadingIndicator({ label = "Đang tải", compact = false }: { label?: string; compact?: boolean }) {
  return <div className={`loading-indicator${compact ? " loading-indicator--compact" : ""}`} role="status" aria-label={label}>
    <span className="loading-indicator__ring" aria-hidden="true" />
  </div>;
}
