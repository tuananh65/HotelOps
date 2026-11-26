import { useEffect, useId, useLayoutEffect, useRef, useState, type ReactNode } from "react";
import { X, CircleAlert, CircleCheck } from "lucide-react";
import "./alert-popup.css";

// Keep alerts in their original form/dialog so modal focus and form controls work.
// Each dialog has its own stack; the native modal layer covers background alerts.
function arrangeAlerts() {
  const groups = new Map<Element | null, HTMLElement[]>();
  document.querySelectorAll<HTMLElement>(".alert-popup:not([hidden])").forEach((element) => {
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
    let top = (viewport?.offsetTop || 0) + 12;
    for (const element of items) {
      element.style.maxHeight = `${limit}px`;
      element.style.top = `${top}px`;
      top += element.getBoundingClientRect().height + gap;
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
  const [dismissed, setDismissed] = useState(false);
  const ref = useRef<HTMLElement>(null);
  const body = useRef<HTMLDivElement>(null);
  const previousText = useRef("");
  const timer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined);
  const id = useId();
  // Keep the message mounted so new content reopens a dismissed notification.
  useLayoutEffect(() => {
    const text = body.current?.textContent || "";
    if (text !== previousText.current) {
      previousText.current = text;
      clearTimeout(timer.current);
      const owner=ref.current?.closest("dialog");
      const first=Array.from(document.querySelectorAll<HTMLElement>(".alert-popup:not([hidden])"))
        .find(element=>element.closest("dialog")===owner&&element.classList.contains(tone)
          &&element.querySelector(".alert-popup-body")?.textContent===text);
      // Deduplicate within the same modal layer; never hide a distinct actionable panel.
      if(first&&first!==ref.current&&!body.current?.querySelector("button,input,select,textarea,a")){
        setDismissed(true);return;
      }
      setDismissed(false);
      // Interactive validation/retry panels remain actionable; only passive toasts expire.
      if (!body.current?.querySelector("button,input,select,textarea,a"))
        timer.current = setTimeout(() => setDismissed(true), 5000);
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
      clearTimeout(timer.current);
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
    <section ref={ref} hidden={dismissed} className={`alert-popup ${tone}`} aria-label={heading}>
      <div className="alert-popup-heading">
        {tone === "success" ? <CircleCheck size={18} aria-hidden="true" /> : <CircleAlert size={18} aria-hidden="true" />}
        <strong>{heading}</strong>
        <button type="button" aria-label="Đóng thông báo" aria-controls={id} onClick={() => setDismissed(true)}>
          <X size={18} aria-hidden="true" />
        </button>
      </div>
      <div ref={body} id={id} className="alert-popup-body" role={tone === "success" ? "status" : "alert"}>
        {children}
      </div>
    </section>
  );
}
