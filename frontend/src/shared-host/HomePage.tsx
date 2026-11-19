import { useState, type ReactNode } from "react";
import { Link } from "react-router-dom";
import { ArrowUpRight, Search, Users, Building2, ClipboardCheck, Package, Clock3, Bell, UserRound } from "lucide-react";
import { useAuth } from "../auth/AuthProvider";
import { HomeNavigation, navigationLabels } from "./HomeNavigation";
import { NotificationUnreadBadge } from "../notifications/NotificationUnreadBadge";
import "./home.css";
import { useHomeDisplay } from "./useHomeDisplay";
import { SearchInput } from "../components/SearchInput";

const sections = [
  { title: "Nhân sự & phân quyền", icon: Users, description: "Tài khoản và quyền truy cập hệ thống.", paths: ["/users", "/roles"] },
  { title: "Khách sạn & cấu hình", icon: Building2, description: "Cấu trúc chi nhánh và danh mục vận hành.", paths: ["/hotel", "/room-directory", "/structure/work-categories", "/structure/priority-sla", "/structure/reference-data"] },
  { title: "Vận hành & buồng phòng", icon: ClipboardCheck, description: "Theo dõi phòng, công việc và xử lý sự cố.", paths: ["/dashboard", "/rooms/status", "/rooms/turnover", "/cleaning/tasks", "/cleaning/assignment", "/rework", "/inspections/checklists", "/inspection-history", "/operational-tasks", "/incidents/new", "/operations/incidents", "/operations/maintenance", "/internal-requests/new", "/internal-requests"] },
  { title: "Tài sản & vật tư", icon: Package, description: "Tra cứu tài sản và các yêu cầu vật tư.", paths: ["/assets", "/assets/locations", "/assets/maintenance-history", "/inventory/stock", "/inventory/items", "/inventory/stocktakes", "/inventory/adjustments", "/inventory/return-requests", "/inventory/transfer-requests", "/material-requests/new", "/operations/material-requests", "/operations/replenishments"] },
  { title: "Ca làm việc", icon: Clock3, description: "Kế hoạch ca và thời gian làm việc.", paths: ["/workday", "/workday/scheduled", "/workday/shift-plans", "/my-time-log", "/time-corrections", "/workday/handovers"] },
];
const names: Record<string, string> = {
  "/users": "Quản lý người dùng", "/roles": "Vai trò & phân quyền", "/hotel": "Cấu trúc khách sạn",
  "/room-directory": "Danh mục phòng", "/structure/work-categories": "Danh mục công việc", "/structure/priority-sla": "Mức ưu tiên & SLA", "/structure/reference-data": "Dữ liệu danh mục",
  "/dashboard": "Bảng tổng quan", "/rooms/status": "Trạng thái phòng", "/rooms/turnover": "Lịch sử luân chuyển phòng", "/cleaning/tasks": "Công việc buồng phòng", "/cleaning/assignment": "Phân công dọn phòng", "/rework": "Công việc làm lại", "/inspections/checklists": "Danh sách kiểm tra", "/inspection-history": "Lịch sử kiểm tra", "/operational-tasks": "Công việc vận hành", "/operations/incidents": "Sự cố & bảo trì",
  "/assets": "Danh mục tài sản", "/assets/locations": "Tài sản theo vị trí", "/assets/maintenance-history": "Lịch sử bảo trì", "/inventory/return-requests": "Yêu cầu hoàn trả", "/operations/material-requests": "Yêu cầu vật tư", "/operations/replenishments": "Bổ sung vật tư", "/workday": "Ngày làm việc", "/workday/shift-plans": "Kế hoạch ca", "/my-time-log": "Thời gian làm việc của tôi",
  "/profile": "Hồ sơ cá nhân", "/notifications": "Thông báo", "/audit": "Lịch sử kiểm toán",
};
const normalize = (value: string) => value.normalize("NFD").replace(/[\u0300-\u036f]/g, "").replace(/đ/g, "d").toLowerCase();

export function HomePage({ children }: { children?: ReactNode }) {
  const { state } = useAuth();
  const [query, setQuery] = useState("");
  const display = useHomeDisplay(state.session?.user.id ?? "anonymous");
  const name = state.session?.user.name || state.session?.user.username;
  return <main className="home-workspace">
    <header className="home-heading"><div><h1>Trang chủ</h1><p>Xin chào{name ? `, ${name}` : ""}. Bạn muốn bắt đầu từ đâu?</p></div><span className="home-date">{new Intl.DateTimeFormat("vi-VN", { weekday: "long", day: "numeric", month: "long", year: "numeric" }).format(new Date())}</span></header>
    <HomeNavigation>{(paths, loading) => {
      const label = (path: string) => names[path] || navigationLabels[path];
      const filtered = paths.filter(path => normalize(`${label(path)} ${navigationLabels[path]}`).includes(normalize(query.trim())));
      const groups = sections.map(section => ({ ...section, visible: section.paths.filter(path => filtered.includes(path)) })).filter(section => section.visible.length);
      const personal = filtered.filter(path => !sections.some(section => section.paths.includes(path)));
      return <><button type="button" className="home-personal-toggle" aria-expanded={display.value.personal} aria-controls="home-personal-panel" aria-label={display.value.personal ? "Ẩn khu vực cá nhân" : "Hiện khu vực cá nhân"} title={display.value.personal ? "Ẩn khu vực cá nhân" : "Hiện khu vực cá nhân"} onClick={() => display.update("personal", !display.value.personal)}><UserRound size={20} aria-hidden="true" /><span>{display.value.personal ? "Ẩn cá nhân" : "Hiện cá nhân"}</span></button>
      <div className={`home-layout${display.value.personal ? "" : " home-layout-expanded"}`}><section className="home-directory" aria-labelledby="home-functions">
        <div className="home-directory-heading"><div><h2 id="home-functions">Không gian làm việc</h2><p>Các chức năng dành cho tài khoản của bạn.</p></div></div>
        <label className="home-search"><Search size={19} aria-hidden="true" /><span className="home-sr-only">Tìm chức năng</span><SearchInput suggestions={paths.map(label)} type="search" placeholder="Tìm chức năng…" value={query} onChange={event => setQuery(event.target.value)} /></label>
        {loading && <p role="status" className="home-feedback">Đang tải chức năng…</p>}
        {!loading && !paths.length && <p role="status" className="home-feedback">Chưa tải được chức năng khả dụng. Hãy tải lại trang; nếu vẫn không có kết quả, liên hệ quản trị viên để kiểm tra quyền truy cập.</p>}
        {!loading && paths.length > 0 && !filtered.length && <p role="status" className="home-feedback">Không tìm thấy chức năng phù hợp. Thử từ khóa khác hoặc xóa nội dung tìm kiếm.</p>}
        <div className="home-groups">{groups.map(group => <section className="home-group" key={group.title}><div className="home-group-heading"><group.icon size={22} aria-hidden="true" /><div><h3>{group.title}</h3><p>{group.description}</p></div></div><ul>{group.visible.map(path => <li key={path}><Link to={path}><span>{label(path)}</span><ArrowUpRight size={17} aria-hidden="true" /></Link></li>)}</ul></section>)}</div>
      </section>{display.value.personal && <aside id="home-personal-panel" className="home-personal" aria-label="Cá nhân và hoạt động">
        <section className="home-account"><UserRound size={26} aria-hidden="true" /><h2>{name || "Tài khoản của bạn"}</h2><p>Không gian cá nhân</p>{personal.map(path => <Link key={path} to={path}><span>{path === "/notifications" && <Bell size={17} aria-hidden="true" />}{label(path)}{path === "/notifications" && <NotificationUnreadBadge />}</span><ArrowUpRight size={17} aria-hidden="true" /></Link>)}</section>
        <section className="home-help"><h2>Bắt đầu công việc</h2><p>Chọn chức năng trong danh mục hoặc dùng thanh điều hướng bên trái để chuyển giữa các nhóm nghiệp vụ.</p><p>Các mục hiển thị phụ thuộc quyền truy cập của tài khoản.</p><div className="home-extra">{children}</div></section>
      </aside>}</div>{!display.value.personal && <div className="home-extra">{children}</div>}</>;
    }}</HomeNavigation>
  </main>;
}
