import { useState, type ReactNode } from "react";
import { Link, NavLink, useLocation } from "react-router-dom";
import {
  LayoutDashboard,
  Users,
  Building2,
  BedDouble,
  ClipboardCheck,
  Package,
  Clock3,
  Menu,
  X,
  UserRound,
} from "lucide-react";
import { useAuth } from "../auth/AuthProvider";
import { NotificationUnreadBadge } from "../notifications/NotificationUnreadBadge";
import {
  HomeNavigation,
  navigationLabels,
  ShellNavigationContext,
} from "./HomeNavigation";

const groups = [
  {
    id: "overview",
    label: "Tổng quan",
    icon: LayoutDashboard,
    prefixes: ["/dashboard", "/notifications", "/audit"],
  },
  {
    id: "people",
    label: "Người dùng",
    icon: Users,
    prefixes: ["/users", "/roles"],
  },
  {
    id: "structure",
    label: "Khách sạn",
    icon: Building2,
    prefixes: ["/hotel", "/structure", "/room-directory"],
  },
  {
    id: "rooms",
    label: "Buồng phòng",
    icon: BedDouble,
    prefixes: ["/rooms", "/cleaning", "/rework"],
  },
  {
    id: "tasks",
    label: "Công việc",
    icon: ClipboardCheck,
    prefixes: [
      "/inspections",
      "/inspection-history",
      "/operational-tasks",
      "/operations/incidents",
    ],
  },
  {
    id: "resources",
    label: "Nguồn lực",
    icon: Package,
    prefixes: [
      "/assets",
      "/inventory",
      "/operations/material-requests",
      "/operations/replenishments",
    ],
  },
  {
    id: "workday",
    label: "Ca làm việc",
    icon: Clock3,
    prefixes: ["/workday", "/my-time-log", "/time-corrections"],
  },
  { id: "account", label: "Cá nhân", icon: UserRound, prefixes: ["/profile"] },
];
const matches = (path: string, prefix: string) =>
  path === prefix || path.startsWith(prefix + "/");
const groupFor = (path: string) =>
  groups.find((group) => group.prefixes.some((prefix) => matches(path, prefix)))
    ?.id ?? "overview";

export function CacaoShell({ children }: { children: ReactNode }) {
  const { state } = useAuth();
  const location = useLocation();
  const [selection, setSelection] = useState<{
    route: string;
    group: string;
  } | null>(null);
  const [mobileRoute, setMobileRoute] = useState<string | null>(null);
  const authenticated =
    state.status === "AUTHENTICATED" && state.session !== null;
  const active =
    selection?.route === location.pathname
      ? selection.group
      : groupFor(location.pathname);
  const open = mobileRoute === location.pathname;
  return (
    <div className={authenticated ? "cacao-shell" : "cacao-public"}>
      <a className="cacao-skip" href="#workspace-content">
        Đến nội dung
      </a>
      {authenticated && (
        <HomeNavigation>
          {(paths, loading) => {
            const available = groups.filter((group) =>
              paths.some((path) => groupFor(path) === group.id),
            );
            const selected =
              available.find((group) => group.id === active) ?? available[0];
            const links = paths.filter(
              (path) => groupFor(path) === selected?.id,
            );
            return (
              <aside
                className={`cacao-navigation${open ? " is-open" : ""}`}
                aria-label="Điều hướng HotelOps"
              >
                <nav className="cacao-rail" aria-label="Nhóm nghiệp vụ">
                  <Link
                    className="cacao-mark"
                    to="/home"
                    aria-label="HotelOps — Trang chủ"
                  >
                    H
                  </Link>
                  {available.map((group) => (
                    <button
                      key={group.id}
                      type="button"
                      aria-pressed={selected?.id === group.id}
                      onClick={() =>
                        setSelection({
                          route: location.pathname,
                          group: group.id,
                        })
                      }
                    >
                      <group.icon size={21} aria-hidden="true" />
                      <span>{group.label}</span>
                    </button>
                  ))}
                </nav>
                <div className="cacao-subnav">
                  <Link className="cacao-brand" to="/home">
                    Hotel<span>Ops</span>
                  </Link>
                  <button
                    className="cacao-close"
                    type="button"
                    onClick={() => setMobileRoute(null)}
                    aria-label="Đóng điều hướng"
                  >
                    <X size={20} />
                  </button>
                  <h2>{selected?.label ?? "Không gian làm việc"}</h2>
                  <nav aria-label="Chức năng">
                    {links.map((path) => (
                      <NavLink
                        key={path}
                        to={path}
                        onClick={() => setMobileRoute(null)}
                      >
                        {navigationLabels[path]}
                        {path === "/notifications" && (
                          <NotificationUnreadBadge />
                        )}
                      </NavLink>
                    ))}
                  </nav>
                  {!paths.length && (
                    <p role="status">
                      {loading
                        ? "Đang tải điều hướng…"
                        : "Chưa có chức năng khả dụng."}
                    </p>
                  )}
                  <div className="cacao-subnav-foot">
                    <span>HotelOps</span>
                    <small>Vận hành khách sạn</small>
                    <Link to="/home" onClick={() => setMobileRoute(null)}>
                      Trang chủ
                    </Link>
                  </div>
                </div>
              </aside>
            );
          }}
        </HomeNavigation>
      )}
      <div className="cacao-workspace">
        {authenticated && (
          <header className="cacao-topbar">
            <button
              className="cacao-menu"
              type="button"
              aria-label={open ? "Đóng điều hướng" : "Mở điều hướng"}
              aria-expanded={open}
              onClick={() => setMobileRoute(open ? null : location.pathname)}
            >
              <Menu size={21} />
            </button>
            <span>Không gian làm việc</span>
            <Link to="/profile">
              <UserRound size={18} aria-hidden="true" />
              {state.session?.user.name || state.session?.user.username}
            </Link>
          </header>
        )}
        {authenticated && open && (
          <button
            type="button"
            className="cacao-backdrop"
            aria-label="Đóng điều hướng"
            onClick={() => setMobileRoute(null)}
          />
        )}
        <div
          id="workspace-content"
          className={authenticated ? "cacao-content" : "cacao-public-content"}
          tabIndex={-1}
        >
          <ShellNavigationContext.Provider
            value={authenticated && location.pathname !== "/home"}
          >
            {children}
          </ShellNavigationContext.Provider>
        </div>
      </div>
    </div>
  );
}
