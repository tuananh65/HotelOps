import { useEffect } from "react";

// A horizontal scroll container becomes the sticky containing block. Compensate
// for its viewport position so headers also remain visible when the page scrolls.
export function useStickyHeaders() {
  useEffect(() => {
    let frame = 0;
    let observedTopbar: HTMLElement | null = null;
    const update = () => {
      frame = 0;
      const topbar = document.querySelector<HTMLElement>(".topbar");
      if (topbar !== observedTopbar) {
        if (observedTopbar) resizeObserver.unobserve(observedTopbar);
        if (topbar) resizeObserver.observe(topbar);
        observedTopbar = topbar;
      }
      const navbarBottom = Math.max(
        0,
        topbar?.getBoundingClientRect().bottom || 0,
      );
      topbar
        ?.closest<HTMLElement>(".workspace")
        ?.style.setProperty(
          "--topbar-height",
          `${topbar.getBoundingClientRect().height}px`,
        );
      document
        .querySelectorAll<HTMLElement>(".table-scroll, .calendar-scroll")
        .forEach((container) => {
          const dialog = container.closest("dialog");
          const filters = container
            .closest(".query-state")
            ?.querySelector<HTMLElement>(".account-filter-panel");
          const anchor = dialog
            ? Math.max(0, dialog.getBoundingClientRect().top + 1)
            : Math.max(
                navbarBottom,
                filters?.getBoundingClientRect().bottom || 0,
              );
          const offset = Math.max(
            0,
            anchor - container.getBoundingClientRect().top,
          );
          container.style.setProperty("--table-header-offset", `${offset}px`);
        });
    };
    const schedule = () => {
      if (!frame) frame = requestAnimationFrame(update);
    };
    const observer = new MutationObserver(schedule);
    const resizeObserver = new ResizeObserver(schedule);
    observer.observe(document.getElementById("root")!, {
      childList: true,
      subtree: true,
    });
    window.addEventListener("scroll", schedule, {
      capture: true,
      passive: true,
    });
    window.addEventListener("resize", schedule);
    schedule();
    return () => {
      observer.disconnect();
      resizeObserver.disconnect();
      cancelAnimationFrame(frame);
      window.removeEventListener("scroll", schedule, true);
      window.removeEventListener("resize", schedule);
    };
  }, []);
}
