import type { ReactNode } from "react";
import type { LucideIcon } from "lucide-react";

export default function MetricHeading({
  icon: Icon,
  children,
}: {
  icon: LucideIcon;
  children: ReactNode;
}) {
  return (
    <h2 className="metric-heading">
      <span className="metric-icon">
        <Icon size={23} aria-hidden="true" />
      </span>
      {children}
    </h2>
  );
}
