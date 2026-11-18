import "./loading-indicator.css";
import { LoaderCircle } from "lucide-react";

export function LoadingIndicator({
  label = "Đang tải",
  compact = false,
}: {
  label?: string;
  compact?: boolean;
}) {
  return (
    <div
      className={`loading-indicator${compact ? " loading-indicator--compact" : ""}`}
      role="status"
      aria-label={label}
    >
      <LoaderCircle
        className="loading-indicator__ring"
        strokeWidth={1.75}
        aria-hidden="true"
      />
    </div>
  );
}
