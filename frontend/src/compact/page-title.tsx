import { createContext, useContext, type ReactNode } from "react";
import { createPortal } from "react-dom";

export const PageTitleTarget = createContext<HTMLDivElement | null>(null);

// Keep the title owned by its page (including dynamic hotel names), but render
// it in the persistent navbar. Unmounting a page also removes its title.
export default function PageTitle({ children }: { children: ReactNode }) {
  const target = useContext(PageTitleTarget);
  return target ? createPortal(children, target) : null;
}
