import type { ReactNode } from "react";

// Keep related filters and actions together; the layout wraps on narrow screens.
export default function PageToolbar({
  children,
  actions,
  className = "",
}: {
  children?: ReactNode;
  actions?: ReactNode;
  className?: string;
}) {
  return (
    <div className={`page-toolbar ${className}`}>
      {children}
      {actions && <div className="page-toolbar-actions">{actions}</div>}
    </div>
  );
}
