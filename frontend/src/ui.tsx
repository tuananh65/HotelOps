import type { ReactNode } from "react";
import screens from "./fe04-screens.json";
// Release composition: FE-04 metadata only; no prototype data or client authorization.
export function PageHeader({ id, children }: { id: number; children?: ReactNode }) {
  const screen = screens.find(s => s.id === id);
  return <div className="page-header"><div><span className="eyebrow">FE-04</span><h1>{screen?.name ?? "Housekeeping"}</h1><p>{screen?.description}</p></div><div className="header-actions">{children}</div></div>;
}
