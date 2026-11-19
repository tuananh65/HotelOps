import { useEffect, useState } from "react";
import { useAuth } from "../auth/AuthProvider";

/** Menu admission includes business-role rules that raw permission grants omit. */
export function useNavigationAccess() {
  const { state, actions } = useAuth();
  const owner =
    state.status === "AUTHENTICATED" &&
    !state.pending &&
    !state.session?.mustChangePassword
      ? state.authorityGeneration
      : null;
  const [snapshot, setSnapshot] = useState<{
    owner: typeof owner;
    paths: string[];
  } | null>(null);
  useEffect(() => {
    setSnapshot(null);
    if (!owner) return;
    const controller = new AbortController();
    let running = false;
    const refresh = async () => {
      if (running) return;
      running = true;
      try {
        const response = await fetch("/api/auth/navigation", {
          credentials: "same-origin",
          cache: "no-store",
          redirect: "error",
          signal: controller.signal,
        });
        if (controller.signal.aborted) return;
        if (response.status === 401) actions.invalidateSession(owner);
        if (!response.ok) throw new Error("Navigation unavailable");
        const data: unknown = await response.json();
        if (
          !data ||
          typeof data !== "object" ||
          !("paths" in data) ||
          !Array.isArray(data.paths) ||
          !data.paths.every((p: unknown) => typeof p === "string")
        )
          throw new Error("Invalid navigation");
        if (!controller.signal.aborted)
          setSnapshot({ owner, paths: data.paths });
      } catch {
        if (!controller.signal.aborted) setSnapshot(null);
      } finally {
        running = false;
      }
    };
    void refresh();
    const focus = () => {
      void refresh();
    };
    window.addEventListener("focus", focus);
    const timer = window.setInterval(focus, 30000);
    return () => {
      controller.abort();
      window.clearInterval(timer);
      window.removeEventListener("focus", focus);
    };
  }, [owner, actions]);
  return (path: string) =>
    !!owner && snapshot?.owner === owner && snapshot.paths.includes(path);
}
