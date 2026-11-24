import { useEffect, useId, useLayoutEffect, useRef, useState, type ReactNode } from "react";
import { ChevronDown, ChevronUp, CircleAlert, CircleCheck } from "lucide-react";
import "./alert-popup.css";

// Keep alerts in their original form/dialog so modal focus and form controls work.
// Each dialog has its own stack; the native modal layer covers background alerts.
function arrangeAlerts() {
  const groups = new Map<Element | null, HTMLElement[]>();
  document.querySelectorAll<HTMLElement>(".alert-popup").forEach((element) => {
    const owner = element.closest("dialog");
    const items = groups.get(owner) || [];
    items.push(element);
    groups.set(owner, items);
  });
  const viewport = window.visualViewport;
  const available = (viewport?.height || window.innerHeight) - 24;
  for (const items of groups.values()) {
    const gap = Math.min(8, Math.max(0, available / (items.length * 4)));
    const limit = Math.max(1, (available - (items.length - 1) * gap) / items.length);
    let bottom = window.innerHeight - (viewport ? viewport.offsetTop + viewport.height : window.innerHeight) + 12;
    for (const element of items.reverse()) {
      element.style.maxHeight = `${limit}px`;
      element.style.bottom = `${bottom}px`;
      bottom += element.getBoundingClientRect().height + gap;
    }
  }
}

export function AlertPopup({
  children,
  tone = "warning",
  "aria-label": title,
}: {
  children: ReactNode;
  tone?: "warning" | "success";
  "aria-label"?: string;
}) {
  const [expanded, setExpanded] = useState(true);
  const ref = useRef<HTMLElement>(null);
  const body = useRef<HTMLDivElement>(null);
  const previousText = useRef("");
  const id = useId();
  // New errors must be visible even if the previous message was collapsed.
  useLayoutEffect(() => {
    const text = body.current?.textContent || "";
    if (text !== previousText.current) {
      previousText.current = text;
      setExpanded(true);
    }
    arrangeAlerts();
  });
  useEffect(() => {
    const observer = new ResizeObserver(arrangeAlerts);
    if (ref.current) observer.observe(ref.current);
    window.addEventListener("resize", arrangeAlerts);
    window.visualViewport?.addEventListener("resize", arrangeAlerts);
    window.visualViewport?.addEventListener("scroll", arrangeAlerts);
    return () => {
      observer.disconnect();
      window.removeEventListener("resize", arrangeAlerts);
      window.visualViewport?.removeEventListener("resize", arrangeAlerts);
      window.visualViewport?.removeEventListener("scroll", arrangeAlerts);
      // Layout after React removes this alert from the DOM.
      queueMicrotask(arrangeAlerts);
    };
  }, []);
  const heading = title || (tone === "success" ? "Thông báo" : "Cảnh báo");
  return (
    <section ref={ref} className={`alert-popup ${tone}`} aria-label={heading}>
      <div className="alert-popup-heading">
        {tone === "success" ? <CircleCheck size={18} aria-hidden="true" /> : <CircleAlert size={18} aria-hidden="true" />}
        <strong>{heading}</strong>
        <button type="button" aria-expanded={expanded} aria-controls={id} onClick={() => setExpanded(!expanded)}>
          {expanded ? "Thu gọn" : "Mở ra"}
          {expanded ? <ChevronUp size={16} aria-hidden="true" /> : <ChevronDown size={16} aria-hidden="true" />}
        </button>
      </div>
      <div ref={body} id={id} className="alert-popup-body" role={tone === "success" ? "status" : "alert"} hidden={!expanded}>
        {children}
      </div>
    </section>
  );
}
