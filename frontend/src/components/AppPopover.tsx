import {
  useEffect,
  useId,
  useRef,
  type ReactNode,
  type RefObject,
} from "react";
import { X } from "lucide-react";
import { IconButton } from "./design-system";

/** Native top-layer popover avoids clipping by toolbars or scrolling tables. */
export function AppPopover({
  open,
  title,
  anchor,
  onClose,
  children,
  footer,
  busy = false,
}: {
  open: boolean;
  title: string;
  anchor: RefObject<HTMLElement | null>;
  onClose: () => void;
  children: ReactNode;
  footer?: ReactNode;
  busy?: boolean;
}) {
  const panel = useRef<HTMLDivElement>(null);
  const close = useRef(onClose);
  close.current = onClose;
  const id = useId();
  useEffect(() => {
    const element = panel.current!;
    if (!open) {
      if (element.matches(":popover-open")) element.hidePopover();
      return;
    }
    element.showPopover();
    const reposition = () => {
      const target = anchor.current?.getBoundingClientRect();
      if (!target) return;
      const width = Math.min(400, window.innerWidth - 24);
      element.style.width = `${width}px`;
      element.style.left = `${Math.max(12, Math.min(target.right - width, window.innerWidth - width - 12))}px`;
      element.style.top = `${Math.min(target.bottom + 12, window.innerHeight - 120)}px`;
      element.style.maxHeight = `${Math.max(100, window.innerHeight - target.bottom - 24)}px`;
    };
    reposition();
    element.querySelector<HTMLElement>("button, a, input")?.focus();
    const toggle = (event: Event) => {
      if ((event as ToggleEvent).newState === "closed") close.current();
    };
    element.addEventListener("toggle", toggle);
    window.addEventListener("resize", reposition);
    return () => {
      element.removeEventListener("toggle", toggle);
      window.removeEventListener("resize", reposition);
      if (element.matches(":popover-open")) element.hidePopover();
      anchor.current?.focus();
    };
  }, [open, anchor]);
  return (
    <div
      ref={panel}
      popover="auto"
      className="ds-popover"
      aria-labelledby={id}
      aria-busy={busy || undefined}
      onKeyDown={(event) => {
        if (event.key === "Escape") {
          event.preventDefault();
          onClose();
        }
      }}
    >
      <header>
        <h2 id={id}>{title}</h2>
        <IconButton label="Đóng thông báo" icon={X} onClick={onClose} />
      </header>
      <div className="ds-popover-body">{open && children}</div>
      {footer && <footer>{footer}</footer>}
    </div>
  );
}
