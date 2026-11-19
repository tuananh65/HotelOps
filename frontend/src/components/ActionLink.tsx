import { Link, type LinkProps } from "react-router-dom";
import type { ReactNode } from "react";

/** Navigation to a workflow, styled like its adjacent action buttons. */
export function ActionLink({
  variant = "secondary",
  className = "",
  ...props
}: LinkProps & { variant?: "primary" | "secondary" }) {
  return (
    <Link
      {...props}
      className={`ds-button ds-button--${variant} ${className}`}
    />
  );
}
export function ActionLinks({ children }: { children: ReactNode }) {
  return <div className="ds-action-links">{children}</div>;
}
