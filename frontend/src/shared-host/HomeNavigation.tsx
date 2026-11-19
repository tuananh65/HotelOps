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
  "/inventory/transfer-requests": "Yêu cầu điều chuyển",
  "/inventory/stock": "Tồn kho và sổ kho",
  "/inventory/items": "Danh mục vật tư",
  "/inventory/stocktakes": "Theo dõi kiểm kê",
  "/inventory/adjustments": "Điều chỉnh tồn kho",
  "/settings": "Cấu hình hệ thống",
  "/operations/maintenance": "Công việc bảo trì",
  "/incidents/new": "Báo sự cố",
  "/material-requests/new": "Tạo yêu cầu vật tư",
  "/internal-requests": "Theo dõi & xử lý yêu cầu nội bộ",
  "/internal-requests/new": "Tạo yêu cầu nội bộ",
  "/change-password": "Đổi mật khẩu",
  "/workday/scheduled": "Bắt đầu / kết thúc ca",
  "/workday/handovers": "Bàn giao ca",
  "/time-corrections": "Đề nghị điều chỉnh thời gian",
  "/inventory/return-requests": "Hoàn trả vật tư",
  "/assets/maintenance-history": "Lịch sử bảo trì tài sản",
  "/assets/locations": "Tài sản theo vị trí",
  "/assets": "Tài sản",
  "/structure/priority-sla": "Thời hạn SLA theo độ ưu tiên",
  "/operations/material-requests": "Theo dõi yêu cầu vật tư",
  "/operations/replenishments": "Bổ sung vật tư",
  "/inspection-history": "Lịch sử kiểm tra",
  "/inspections/assignment": "Phòng chờ kiểm tra",
  "/inspections/checklists": "Danh sách kiểm tra phòng",
  "/cleaning/tasks": "Công việc buồng phòng",
  "/cleaning/assignment": "Phân công vệ sinh",
  "/rework": "Xử lý công việc làm lại",
  "/rooms/turnover": "Lịch sử luân chuyển phòng",
  "/workday/shift-plans": "Lập kế hoạch ca",
  "/profile": "Hồ sơ cá nhân",
  "/notifications": "Thông báo",
  "/users": "Người dùng",
  "/hotel": "Cấu trúc khách sạn",
  "/room-directory": "Danh mục phòng",
  "/dashboard": "Tổng quan",
  "/audit": "Lịch sử thay đổi",
  "/rooms/status": "Trạng thái phòng",
  "/roles": "Vai trò và quyền hạn",
  "/structure/work-categories": "Nhóm công việc",
  "/structure/reference-data": "Dữ liệu danh mục",
  "/operations/incidents": "Sự cố",
  "/operational-tasks": "Công việc được giao",
  "/workday": "Lịch ca & ngày làm việc",
  "/my-time-log": "Nhật ký thời gian của tôi",
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
    let running = false;
    const refresh = async () => {
      if (running) return;
      running = true;
      controller = new AbortController();
      const signal = controller.signal;
      const revision = ++generation;
      let succeeded = false;
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
        if (!signal.aborted && revision === generation) {
          setResult({ owner, paths: [...new Set(body.paths as string[])] });
          succeeded = true;
        }
      } catch {
        /* Authorization unavailable: keep links hidden. */
      } finally {
        running = false;
        if (!signal.aborted && revision === generation) {
          if (!succeeded) setResult(null);
          setFinished(owner);
        }
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
    <span aria-label="Điều hướng trang chủ theo quyền">
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
