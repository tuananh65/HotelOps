import { WorkflowFeedbackHost } from "../components/workspace-interactions";
import { useEffect, useRef, useState, type ReactNode } from "react";
import { Link, NavLink, useLocation, useNavigate } from "react-router-dom";
import {
  ChevronDown,
  ChevronRight,
  Home,
  LogOut,
  Menu,
  PanelLeftClose,
  PanelLeftOpen,
  Search,
  UserRound,
  X,
  KeyRound,
} from "lucide-react";
import { useAuth } from "../auth/AuthProvider";
import {
  AppDrawer,
  ActionMenu,
  ErrorState,
  IconButton,
} from "../components/design-system";
import { LoadingIndicator } from "../components/LoadingIndicator";
import {
  HomeNavigation,
  navigationLabels,
  ShellNavigationContext,
} from "./HomeNavigation";
import {
  navigationGroups,
  navigationGroupFor,
  navigationIconFor,
  pathMatches,
} from "./navigationIcons";
import { HeaderNotifications } from "./HeaderNotifications";

function WorkspaceChrome({
  paths,
  loading,
  children,
}: {
  paths: string[];
  loading: boolean;
  children: ReactNode;
}) {
  const { state, actions } = useAuth();
  const location = useLocation();
  const navigate = useNavigate();
  const owner = state.authorityGeneration;
  const authenticated =
    state.status === "AUTHENTICATED" && state.session !== null;
  const [collapsed, setCollapsed] = useState(false);
  const [mobileRoute, setMobileRoute] = useState<string | null>(null);
  const [expanded, setExpanded] = useState<Record<string, boolean>>({});
  const [search, setSearch] = useState("");
  const [searchOpen, setSearchOpen] = useState(false);
  const [error, setError] = useState<{
    owner: typeof owner;
    message: string;
  } | null>(null);
  const pendingLogout = useRef(false);
  const searchRef = useRef<HTMLInputElement>(null);
  const searchContainer = useRef<HTMLDivElement>(null);
  const activePath = [...paths]
    .filter((path) => pathMatches(location.pathname, path))
    .sort((a, b) => b.length - a.length)[0];
  const currentGroup = navigationGroupFor(location.pathname);
  const groups = navigationGroups.filter((group) =>
    paths.some((path) => navigationGroupFor(path) === group.id),
  );
  const open = authenticated && mobileRoute === location.pathname;
  const searchResults = paths
    .filter((path) =>
      navigationLabels[path]
        .toLocaleLowerCase("vi")
        .includes(search.trim().toLocaleLowerCase("vi")),
    )
    .slice(0, 8);
  useEffect(() => {
    setSearchOpen(false);
    setSearch("");
  }, [location.pathname, owner]);
  useEffect(() => {
    const outside = (event: PointerEvent) => {
      if (!searchContainer.current?.contains(event.target as Node))
        setSearchOpen(false);
    };
    document.addEventListener("pointerdown", outside);
    return () => document.removeEventListener("pointerdown", outside);
  }, []);
  async function logout() {
    if (pendingLogout.current || state.pending) return;
    pendingLogout.current = true;
    try {
      const result = await actions.logout();
      if (result.ok) navigate("/login");
      else
        setError({
          owner,
          message:
            "Chưa xác nhận đăng xuất. Vui lòng kiểm tra phiên trong hồ sơ cá nhân.",
        });
    } catch {
      setError({
        owner,
        message:
          "Chưa xác nhận đăng xuất. Vui lòng kiểm tra phiên trong hồ sơ cá nhân.",
      });
    } finally {
      pendingLogout.current = false;
    }
  }
  function navigation(mobile = false) {
    return (
      <div
        className={`ds-sidebar-inner${mobile ? " ds-sidebar-inner--mobile" : ""}`}
      >
        <Link className="ds-brand" to="/home" aria-label="HotelOps — Trang chủ">
          <img className="ds-brand-mark" src="/duc-vuong-logo.jpg" alt="Đức Vượng Group" />
          <span className="ds-brand-name">
            Hotel<span>Ops</span>
            <small>VẬN HÀNH KHÁCH SẠN</small>
          </span>
        </Link>
        <nav className="ds-sidebar-nav cacao-subnav" aria-label="Chức năng">
          <NavLink
            to="/home"
            className="ds-home-link"
            title={collapsed && !mobile ? "Trang chủ" : undefined}
            onClick={() => setMobileRoute(null)}
          >
            <Home size={18} aria-hidden="true" />
            <span>Trang chủ</span>
          </NavLink>
          {groups.map((group) => {
            const active = currentGroup === group.id;
            const isExpanded =
              expanded[`${location.pathname}:${group.id}`] ?? active;
            const links = paths.filter(
              (path) =>
                navigationGroupFor(path) === group.id &&
                !(
                  path === "/incidents/new" &&
                  paths.includes("/operations/incidents")
                ),
            );
            const GroupIcon = group.icon;
            return (
              <div
                key={group.id}
                className={`ds-nav-group${active ? " is-active" : ""}`}
              >
                <button
                  type="button"
                  className="ds-nav-group-toggle"
                  title={collapsed && !mobile ? group.label : undefined}
                  aria-label={group.label}
                  aria-expanded={!collapsed || mobile ? isExpanded : false}
                  onClick={() => {
                    if (
                      !mobile &&
                      window.matchMedia("(max-width: 1023px)").matches
                    ) {
                      setMobileRoute(location.pathname);
                      setExpanded((value) => ({
                        ...value,
                        [`${location.pathname}:${group.id}`]: true,
                      }));
                    } else if (collapsed && !mobile) {
                      setCollapsed(false);
                      setExpanded((value) => ({
                        ...value,
                        [`${location.pathname}:${group.id}`]: true,
                      }));
                    } else
                      setExpanded((value) => ({
                        ...value,
                        [`${location.pathname}:${group.id}`]: !isExpanded,
                      }));
                  }}
                >
                  <GroupIcon size={18} aria-hidden="true" />
                  <span>{group.label}</span>
                  <ChevronDown
                    size={14}
                    className={isExpanded ? "is-expanded" : ""}
                    aria-hidden="true"
                  />
                </button>
                {isExpanded && (!collapsed || mobile) && (
                  <div className="ds-nav-children">
                    {links.map((path) => {
                      const Icon = navigationIconFor(path);
                      return (
                        <NavLink
                          key={path}
                          to={path}
                          className={activePath === path ? "is-current" : ""}
                          aria-current={activePath === path ? "page" : false}
                          end
                          onClick={() => setMobileRoute(null)}
                        >
                          <Icon size={16} aria-hidden="true" />
                          <span>{navigationLabels[path]}</span>
                        </NavLink>
                      );
                    })}
                  </div>
                )}
              </div>
            );
          })}
          {!paths.length &&
            (loading ? (
              <LoadingIndicator compact label="Đang tải điều hướng" />
            ) : (
              <p className="ds-nav-unavailable" role="status">
                Chưa có chức năng khả dụng.
              </p>
            ))}
        </nav>
        <div className="ds-sidebar-footer">
          <img className="ds-brand-mark ds-brand-mark--small" src="/duc-vuong-logo.jpg" alt="Đức Vượng Group" />
          <div>
            <strong>HotelOps</strong>
            <small>Không gian làm việc</small>
          </div>
        </div>
      </div>
    );
  }
  return (
    <div
      className={
        authenticated
          ? `cacao-shell ds-shell${collapsed ? " is-collapsed" : ""}`
          : "cacao-public"
      }
    >
      <a className="cacao-skip" href="#workspace-content">
        Đến nội dung
      </a>
      {authenticated && (
        <aside className="ds-sidebar" aria-label="Điều hướng HotelOps">
          {navigation()}
        </aside>
      )}
      <AppDrawer
        navigation
        open={open}
        title="Điều hướng HotelOps"
        onClose={() => setMobileRoute(null)}
      >
        <aside aria-label="Điều hướng HotelOps trên điện thoại">
          {navigation(true)}
        </aside>
      </AppDrawer>
      <div className="cacao-workspace ds-workspace">
        {authenticated && (
          <header className="ds-topbar">
            <IconButton
              className="ds-sidebar-toggle"
              label={
                collapsed
                  ? "Mở rộng thanh điều hướng"
                  : "Thu gọn thanh điều hướng"
              }
              icon={collapsed ? PanelLeftOpen : PanelLeftClose}
              aria-expanded={!collapsed}
              onClick={() => setCollapsed((value) => !value)}
            />
            <IconButton
              className="ds-mobile-toggle"
              label={open ? "Đóng điều hướng" : "Mở điều hướng"}
              icon={Menu}
              aria-expanded={open}
              onClick={() => setMobileRoute(open ? null : location.pathname)}
            />
            <div className="ds-global-search" ref={searchContainer}>
              <Search size={18} aria-hidden="true" />
              <input
                ref={searchRef}
                aria-label="Tìm chức năng"
                placeholder="Tìm chức năng…"
                value={search}
                role="combobox"
                aria-expanded={searchOpen}
                aria-controls="workspace-search-results"
                autoComplete="off"
                onFocus={() => setSearchOpen(true)}
                onChange={(event) => {
                  setSearch(event.target.value);
                  setSearchOpen(true);
                }}
                onKeyDown={(event) => {
                  if (event.key === "Escape") setSearchOpen(false);
                  if (event.key === "ArrowDown") {
                    event.preventDefault();
                    searchContainer.current
                      ?.querySelector<HTMLAnchorElement>('[role="option"]')
                      ?.focus();
                  }
                  if (
                    event.key === "Enter" &&
                    searchResults[0] &&
                    search.trim()
                  ) {
                    event.preventDefault();
                    navigate(searchResults[0]);
                    setSearchOpen(false);
                  }
                }}
              />
              {search && (
                <IconButton
                  label="Xóa tìm kiếm"
                  icon={X}
                  onClick={() => {
                    setSearch("");
                    searchRef.current?.focus();
                  }}
                />
              )}
              {searchOpen && (
                <div
                  id="workspace-search-results"
                  className="ds-search-results"
                  role="listbox"
                  aria-label="Kết quả tìm chức năng"
                  onKeyDown={(event) => {
                    if (event.key === "Escape") {
                      setSearchOpen(false);
                      searchRef.current?.focus();
                    }
                    if (["ArrowDown", "ArrowUp"].includes(event.key)) {
                      event.preventDefault();
                      const items = [
                        ...searchContainer.current!.querySelectorAll<HTMLAnchorElement>(
                          '[role="option"]',
                        ),
                      ];
                      const index = items.indexOf(
                        document.activeElement as HTMLAnchorElement,
                      );
                      items[
                        (index +
                          (event.key === "ArrowDown" ? 1 : -1) +
                          items.length) %
                          items.length
                      ]?.focus();
                    }
                  }}
                >
                  {searchResults.length ? (
                    searchResults.map((path) => {
                      const Icon = navigationIconFor(path);
                      return (
                        <Link
                          role="option"
                          aria-selected={activePath === path}
                          key={path}
                          to={path}
                          onClick={() => setSearchOpen(false)}
                        >
                          <Icon size={16} aria-hidden="true" />
                          {navigationLabels[path]}
                        </Link>
                      );
                    })
                  ) : (
                    <p>Không tìm thấy chức năng được phép truy cập.</p>
                  )}
                </div>
              )}
            </div>
            <div className="ds-header-actions">
              {paths.includes("/notifications") && <HeaderNotifications />}
              <ActionMenu
                label="Menu tài khoản"
                icon={UserRound}
                trigger={
                  <>
                    <span className="ds-user-name">
                      {state.session?.user.name || state.session?.user.username}
                    </span>
                    <ChevronDown size={14} aria-hidden="true" />
                  </>
                }
                items={[
                  ...(paths.includes("/profile")
                    ? [
                        {
                          label: "Hồ sơ cá nhân",
                          to: "/profile",
                          icon: UserRound,
                        },
                      ]
                    : []),
                  ...(paths.includes("/change-password")
                    ? [
                        {
                          label: "Đổi mật khẩu",
                          to: "/change-password",
                          icon: KeyRound,
                        },
                      ]
                    : []),
                  {
                    label: "Đăng xuất",
                    icon: LogOut,
                    onSelect: () => void logout(),
                    disabled: !!state.pending,
                    danger: true,
                  },
                ]}
              />
            </div>
          </header>
        )}
        <div
          id="workspace-content"
          className={
            authenticated ? "cacao-content ds-content" : "cacao-public-content"
          }
          tabIndex={-1}
        >
          {authenticated && (
            <nav className="ds-breadcrumb" aria-label="Đường dẫn trang">
              <Link to="/home">
                <Home size={14} aria-hidden="true" />
                Trang chủ
              </Link>
              {location.pathname !== "/home" && (
                <>
                  <ChevronRight size={14} aria-hidden="true" />
                  <span aria-current="page">
                    {location.pathname === "/users/new"
                      ? "Tạo tài khoản"
                      : (navigationLabels[location.pathname] ??
                        (activePath
                          ? `${navigationLabels[activePath]} · Chi tiết`
                          : "Chi tiết"))}
                  </span>
                </>
              )}
            </nav>
          )}
          {error?.owner === owner && <ErrorState message={error.message} />}
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
export function CacaoShell({ children }: { children: ReactNode }) {
  return (
    <HomeNavigation>
      {(paths, loading) => (
        <WorkspaceChrome paths={paths} loading={loading}>
          {children}
          <WorkflowFeedbackHost />
        </WorkspaceChrome>
      )}
    </HomeNavigation>
  );
}
