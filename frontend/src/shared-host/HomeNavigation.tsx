import { NotificationUnreadBadge } from "../notifications/NotificationUnreadBadge";
import {
  createContext,
  useContext,
  useEffect,
  useState,
  type ReactNode,
} from "react";
import { Link } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import type { AuthorityGeneration } from "../auth/authStore";

export const navigationLabels: Record<string, string> = {
  "/inventory/return-requests": "Material returns",
  "/assets/maintenance-history": "Asset maintenance history",
  "/assets/locations": "Assets by location",
  "/assets": "Assets",
  "/structure/priority-sla": "Priority SLA",
  "/operations/material-requests": "Material requests",
  "/operations/replenishments": "Replenishments",
  "/inspection-history": "Inspection history",
  "/inspections/checklists": "Inspection checklists",
  "/cleaning/tasks": "Cleaning tasks",
  "/cleaning/assignment": "Cleaning assignment",
  "/rework": "Rework",
  "/rooms/turnover": "Turnover history",
  "/workday/shift-plans": "Shift planning",
  "/profile": "Profile",
  "/notifications": "Notifications",
  "/users": "Users",
  "/hotel": "Structure",
  "/room-directory": "Room directory",
  "/dashboard": "Dashboard",
  "/audit": "Audit history",
  "/rooms/status": "Room status board",
  "/roles": "Roles and permissions",
  "/structure/work-categories": "Work categories",
  "/structure/reference-data": "Reference data",
  "/operations/incidents": "Incidents",
  "/operational-tasks": "Operational tasks",
  "/workday": "Workday",
  "/my-time-log": "My Time Log",
};
const labels = navigationLabels;
export const ShellNavigationContext = createContext(false);
type NavigationProps = {
  enabled?: readonly string[];
  children?: (paths: string[], loading: boolean) => ReactNode;
};
export function HomeNavigation(props: NavigationProps) {
  const hosted = useContext(ShellNavigationContext);
  return hosted ? null : <AuthorizedNavigation {...props} />;
}
function AuthorizedNavigation({ enabled, children }: NavigationProps) {
  const { state, actions } = useAuth();
  const owner =
    state.status === "AUTHENTICATED" && state.pending === null
      ? state.authorityGeneration
      : null;
  const [result, setResult] = useState<{
    owner: AuthorityGeneration;
    paths: string[];
  } | null>(null);
  const [finished, setFinished] = useState<AuthorityGeneration | null>(null);
  useEffect(() => {
    if (!owner) return;
    let controller: AbortController | null = null;
    let generation = 0;
    const refresh = async () => {
      controller?.abort();
      controller = new AbortController();
      const signal = controller.signal;
      const revision = ++generation;
      setResult(null);
      setFinished(null);
      try {
        const response = await fetch("/api/auth/navigation", {
          credentials: "same-origin",
          cache: "no-store",
          redirect: "error",
          signal,
        });
        if (response.status === 401) {
          actions.invalidateSession(owner);
          return;
        }
        if (!response.ok) return;
        const body: unknown = await response.json();
        if (
          !body ||
          typeof body !== "object" ||
          !("paths" in body) ||
          !Array.isArray(body.paths) ||
          !body.paths.every(
            (path: unknown) =>
              typeof path === "string" && Object.hasOwn(labels, path),
          )
        )
          return;
        if (!signal.aborted && revision === generation)
          setResult({ owner, paths: [...new Set(body.paths as string[])] });
      } catch {
        /* Authorization unavailable: keep links hidden. */
      } finally {
        if (!signal.aborted && revision === generation) setFinished(owner);
      }
    };
    void refresh();
    const onFocus = () => {
      void refresh();
    };
    window.addEventListener("focus", onFocus);
    const timer = window.setInterval(onFocus, 30000);
    return () => {
      ++generation;
      controller?.abort();
      window.removeEventListener("focus", onFocus);
      window.clearInterval(timer);
    };
  }, [owner, actions]);
  const visible = owner && result?.owner === owner ? result.paths : [];
  if (children)
    return children(
      visible.filter((path) => !enabled || enabled.includes(path)),
      !!owner && finished !== owner,
    );
  return (
    <span aria-label="Authorized home navigation">
      {visible
        .filter((path) => !enabled || enabled.includes(path))
        .map((path) => (
          <span key={path}>
            <Link to={path}>
              {labels[path]}
              {path === "/notifications" && <NotificationUnreadBadge />}
            </Link>{" "}
          </span>
        ))}
    </span>
  );
}
