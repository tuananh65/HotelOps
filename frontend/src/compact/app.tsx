import { PageTitleTarget } from "./page-title";
import { lazy, Suspense, useEffect, useState } from "react";
import {
  BedDouble,
  ClipboardList,
  CalendarDays,
  Package,
  Boxes,
  ChartNoAxesCombined,
  Users,
  Settings,
  Bell,
  LogOut,
  Menu,
  LayoutDashboard,
  ChevronLeft,
  Building2,
  ListChecks,
  Inbox,
  PackageCheck,
  Wrench,
  ListTodo,
  ArrowLeftRight,
  ClipboardCheck,
  Truck,
  History,
  PackageSearch,
  CalendarClock,
  HardDrive,
  TriangleAlert,
} from "lucide-react";
import { Auth, useAuth } from "./auth";
import { message } from "./api";
import { kinds } from "./workflow";
import { useStickyHeaders } from "./use-sticky-headers";
import BranchContext from "./branch-context";
import "./compact.css";
import "./task-ux.css";
import "./inventory.css";
import { documentRoute } from "./inventory-document-model";
const Workday = lazy(() => import("./workday")),
  Rooms = lazy(() => import("./rooms")),
  Work = lazy(() => import("./work")),
  Documents = lazy(() => import("./documents")),
  Assets = lazy(() => import("./assets")),
  Insights = lazy(() => import("./insights")),
  Admin = lazy(() => import("./admin")),
  Management = lazy(() => import("./management")),
  HotelWorkspace = lazy(() => import("./hotel-workspace")),
  Catalog = lazy(() => import("./catalog")),
  Stock = lazy(() => import("./stock")),
  Templates = lazy(() => import("./templates"));
const PersonalOverview = lazy(() => import("./personal-overview"));
const SchedulingStaff = lazy(() => import("./scheduling-staff"));
const HousekeepingPersonal = lazy(() => import("./housekeeping-personal"));
const InventoryDashboard = lazy(() => import("./inventory-dashboard"));
const InventoryDocuments = lazy(() => import("./inventory-documents"));
const MaintenanceOverview = lazy(() => import("./maintenance-overview"));
const MaintenancePlans = lazy(() => import("./maintenance-plans"));
const MaintenancePersonal = lazy(() => import("./maintenance-personal"));
const OwnerWorkspace = lazy(() => import("./owner-workspace"));
function Shell() {
  const { profile, branch, setBranch, can, canGlobal, logout } = useAuth();
  const [ownerScope, setOwnerScope] = useState(0);
  useStickyHeaders();
  const [titleTarget, setTitleTarget] = useState<HTMLDivElement | null>(null);
  const management = ["USER_VIEW", "MASTER_DATA_MANAGE", "ROLE_MANAGE"].some(
    canGlobal,
  );
  const ownerOnly =
    profile.roles.length > 0 && profile.roles.every((r) => r.code === "OWNER");
  const branchRoles = profile.roles.filter(
    (r) => r.branchId === null || r.branchId === branch,
  );
  const receptionOnly =
    branchRoles.length > 0 && branchRoles.every((r) => r.code === "RECEPTION");
  // A presentation profile, never a substitute for permission guards.
  const departmentHead =
    branchRoles.length > 0 &&
    branchRoles.every((r) => r.code === "DEPT_MANAGER");
  const operationsManager =
    branchRoles.length > 0 &&
    branchRoles.every((r) => r.code === "OPERATION_MANAGER");
  const housekeepingStaff =
    branchRoles.length > 0 && branchRoles.every((r) => r.code === "HK_STAFF");
  const maintenanceLead =
    branchRoles.length > 0 &&
    branchRoles.every((r) => r.code === "MAINTENANCE_LEAD");
  const maintenanceStaff =
    branchRoles.length > 0 &&
    branchRoles.every((r) => r.code === "MAINTENANCE_STAFF");
  const storekeeper =
    branchRoles.length > 0 &&
    branchRoles.every((r) => r.code === "STOREKEEPER");
  const [page, setPage] = useState(location.hash.slice(1).split("?")[0]),
    [route, setRoute] = useState(location.hash),
    [open, setOpen] = useState(false),
    [collapsed, setCollapsed] = useState(false),
    [error, setError] = useState("");
  const nav = (
    ownerOnly
      ? [
          {
            id: "dashboard",
            name: "Tổng quan hệ thống",
            icon: LayoutDashboard,
            show: canGlobal("DASHBOARD_VIEW"),
          },
          {
            id: "branches",
            name: "Chi nhánh",
            icon: Building2,
            show: canGlobal("DASHBOARD_VIEW"),
          },
          {
            id: "rooms",
            name: "Tình trạng phòng",
            icon: BedDouble,
            show: canGlobal("ROOM_VIEW"),
          },
          {
            id: "work",
            name: "Công việc & Vận hành",
            icon: ClipboardList,
            show: [
              "CLEANING_VIEW",
              "INSPECTION_VIEW",
              "TASK_VIEW",
              "IR_VIEW",
            ].some(canGlobal),
          },
          {
            id: "technical",
            name: "Sự cố & Bảo trì",
            icon: TriangleAlert,
            show: canGlobal("INCIDENT_VIEW") || canGlobal("MAINTENANCE_VIEW"),
          },
          {
            id: "resources",
            name: "Tài sản & Kho",
            icon: PackageSearch,
            show: ["ASSET_VIEW", "INVENTORY_VIEW", "MR_VIEW"].some(canGlobal),
          },
          {
            id: "reports",
            name: "Báo cáo & Phân tích",
            icon: ChartNoAxesCombined,
            show: canGlobal("REPORT_VIEW"),
          },
          {
            id: "audit",
            name: "Nhật ký hoạt động",
            icon: History,
            show: canGlobal("AUDIT_VIEW"),
          },
        ]
      : maintenanceStaff
        ? [
            {
              id: "dashboard",
              name: "Tổng quan cá nhân",
              icon: LayoutDashboard,
              show: can("MAINTENANCE_VIEW"),
            },
            {
              id: "work",
              name: "Công việc bảo trì",
              icon: Wrench,
              show: can("MAINTENANCE_VIEW"),
            },
            {
              id: "incidents",
              name: "Sự cố kỹ thuật",
              icon: TriangleAlert,
              show: can("INCIDENT_VIEW"),
            },
            {
              id: "assets",
              name: "Tài sản & Thiết bị",
              icon: HardDrive,
              show: can("ASSET_VIEW"),
            },
            {
              id: "materials",
              name: "Vật tư & Yêu cầu",
              icon: Package,
              show: can("MR_VIEW") || can("IR_VIEW") || can("TASK_VIEW"),
            },
            {
              id: "workday",
              name: "Lịch làm việc",
              icon: CalendarDays,
              show: can("ATTENDANCE_RECORD"),
            },
            {
              id: "notifications",
              name: "Thông báo & Lịch sử",
              icon: Bell,
              show: true,
            },
          ]
        : maintenanceLead
          ? [
              {
                id: "dashboard",
                name: "Tổng quan bảo trì",
                icon: LayoutDashboard,
                show:
                  can("DASHBOARD_VIEW", false) &&
                  can("MAINTENANCE_VIEW", false),
              },
              {
                id: "work",
                name: "Công việc bảo trì",
                icon: Wrench,
                show: can("MAINTENANCE_VIEW", false),
              },
              {
                id: "review",
                name: "Kiểm tra & Nghiệm thu",
                icon: ClipboardCheck,
                show:
                  can("MAINTENANCE_REVIEW", false) &&
                  can("MAINTENANCE_VIEW", false),
              },
              {
                id: "incidents",
                name: "Sự cố kỹ thuật",
                icon: TriangleAlert,
                show: can("INCIDENT_VIEW", false),
              },
              {
                id: "templates",
                name: "Bảo trì định kỳ",
                icon: CalendarClock,
                show: can("PM_MANAGE", false),
              },
              {
                id: "assets",
                name: "Tài sản & Thiết bị",
                icon: HardDrive,
                show: can("ASSET_VIEW", false),
              },
              {
                id: "technicians",
                name: "Nhân viên & Ca trực",
                icon: Users,
                show: can("MAINTENANCE_ASSIGN", false),
              },
              {
                id: "requests",
                name: "Yêu cầu & Thông báo",
                icon: Inbox,
                show: can("MR_VIEW", false),
              },
            ]
          : storekeeper
            ? [
                {
                  id: "dashboard",
                  name: "Tổng quan kho",
                  icon: LayoutDashboard,
                  show: can("INVENTORY_VIEW", false),
                },
                {
                  id: "supplies",
                  name: "Danh mục vật tư",
                  icon: Boxes,
                  show:
                    can("INVENTORY_VIEW", false) ||
                    can("ITEM_MASTER_MANAGE", false),
                },
                {
                  id: "stock",
                  name: "Tồn kho",
                  icon: PackageSearch,
                  show: can("INVENTORY_VIEW", false),
                },
                {
                  id: "transactions",
                  name: "Nhập / Xuất / Chuyển kho",
                  icon: ArrowLeftRight,
                  show: can("INVENTORY_VIEW", false),
                },
                {
                  id: "fulfillment",
                  name: "Yêu cầu vật tư",
                  icon: ClipboardList,
                  show: can("MR_VIEW", false),
                },
                {
                  id: "stocktaking",
                  name: "Kiểm kê & Điều chỉnh",
                  icon: ClipboardCheck,
                  show: can("INVENTORY_VIEW", false),
                },
                {
                  id: "replenishment",
                  name: "Bổ sung vật tư",
                  icon: Truck,
                  show: can("REPLENISHMENT_VIEW", false),
                },
                {
                  id: "history",
                  name: "Lịch sử & Chứng từ",
                  icon: History,
                  show: can("INVENTORY_VIEW", false),
                },
              ]
            : housekeepingStaff
              ? [
                  {
                    id: "dashboard",
                    name: "Tổng quan cá nhân",
                    icon: LayoutDashboard,
                    show: [
                      "CLEANING_VIEW",
                      "TASK_VIEW",
                      "ATTENDANCE_RECORD",
                    ].some((p) => can(p)),
                  },
                  {
                    id: "cleaning",
                    name: "Công việc dọn phòng",
                    icon: ClipboardList,
                    show: can("CLEANING_VIEW"),
                  },
                  {
                    id: "rooms",
                    name: "Phòng & Checklist",
                    icon: BedDouble,
                    show: can("ROOM_VIEW"),
                  },
                  {
                    id: "work",
                    name: "Công việc khác",
                    icon: ListTodo,
                    show: can("TASK_VIEW"),
                  },
                  {
                    id: "requests",
                    name: "Yêu cầu & Vật tư",
                    icon: Package,
                    show: can("IR_VIEW") || can("MR_VIEW"),
                  },
                  {
                    id: "workday",
                    name: "Lịch làm việc",
                    icon: CalendarDays,
                    show: can("ATTENDANCE_RECORD"),
                  },
                  {
                    id: "incidents",
                    name: "Sự cố & Thông báo",
                    icon: Bell,
                    show: can("INCIDENT_VIEW") || can("ROOM_VIEW"),
                  },
                ]
              : operationsManager
                ? [
                    {
                      id: "dashboard",
                      name: "Tổng quan vận hành",
                      icon: LayoutDashboard,
                      show: can("DASHBOARD_VIEW", false),
                    },
                    {
                      id: "rooms",
                      name: "Tình trạng phòng",
                      icon: BedDouble,
                      show: can("ROOM_VIEW", false),
                    },
                    {
                      id: "housekeeping",
                      name: "Buồng phòng",
                      icon: ListChecks,
                      show:
                        can("CLEANING_VIEW", false) ||
                        can("INSPECTION_VIEW", false),
                    },
                    {
                      id: "technical",
                      name: "Bảo trì & sự cố",
                      icon: Wrench,
                      show:
                        can("MAINTENANCE_VIEW", false) ||
                        can("INCIDENT_VIEW", false),
                    },
                    {
                      id: "work",
                      name: "Công việc & yêu cầu",
                      icon: ClipboardList,
                      show: can("TASK_VIEW", false) || can("IR_VIEW", false),
                    },
                    {
                      id: "assets",
                      name: "Tài sản",
                      icon: Boxes,
                      show: can("ASSET_VIEW", false),
                    },
                    {
                      id: "stock",
                      name: "Kho & vật tư",
                      icon: Package,
                      show:
                        can("INVENTORY_VIEW", false) || can("MR_VIEW", false),
                    },
                    {
                      id: "reports",
                      name: "Báo cáo & nhật ký",
                      icon: ChartNoAxesCombined,
                      show:
                        can("REPORT_VIEW", false) || can("AUDIT_VIEW", false),
                    },
                  ]
                : departmentHead
                  ? [
                      {
                        id: "dashboard",
                        name: "Tổng quan",
                        icon: LayoutDashboard,
                        show: can("DASHBOARD_VIEW", false),
                      },
                      {
                        id: "workday",
                        name: "Lịch làm việc",
                        icon: CalendarDays,
                        show: can("SHIFT_MANAGE", false),
                      },
                      {
                        id: "work",
                        name: "Công việc",
                        icon: ClipboardList,
                        show: can("TASK_VIEW"),
                      },
                      {
                        id: "requests",
                        name: "Yêu cầu nội bộ",
                        icon: Inbox,
                        show: can("IR_VIEW"),
                      },
                      {
                        id: "documents",
                        name: "Yêu cầu vật tư",
                        icon: PackageCheck,
                        show: can("MR_VIEW"),
                      },
                      {
                        id: "reports",
                        name: "Báo cáo",
                        icon: ChartNoAxesCombined,
                        show: can("REPORT_VIEW", false),
                      },
                      {
                        id: "staff",
                        name: "Nhân sự",
                        icon: Users,
                        show: can("SHIFT_MANAGE", false),
                      },
                    ]
                  : [
                      {
                        id: "dashboard",
                        name: "Tổng quan",
                        icon: LayoutDashboard,
                        show: !receptionOnly,
                      },
                      {
                        id: "rooms",
                        name: "Phòng",
                        icon: BedDouble,
                        show: can("ROOM_VIEW"),
                      },
                      {
                        id: "work",
                        name: "Công việc & yêu cầu",
                        icon: ClipboardList,
                        show: kinds.some((k) => can(k[2])),
                      },
                      {
                        id: "workday",
                        name: "Lịch làm việc",
                        icon: CalendarDays,
                        show: can("SHIFT_MANAGE") || can("ATTENDANCE_RECORD"),
                      },
                      {
                        id: "documents",
                        name: "Vật tư & chứng từ",
                        icon: Package,
                        show: [
                          "INVENTORY_VIEW",
                          "MR_VIEW",
                          "REPLENISHMENT_VIEW",
                        ].some((x) => can(x)),
                      },
                      {
                        id: "assets",
                        name: "Tài sản",
                        icon: Boxes,
                        show: can("ASSET_VIEW"),
                      },
                      {
                        id: "stock",
                        name: "Tồn kho",
                        icon: Package,
                        show: can("INVENTORY_VIEW", false),
                      },
                      {
                        id: "catalog",
                        name: "Cấu trúc khách sạn",
                        icon: Building2,
                        show: can("MASTER_DATA_MANAGE", false),
                      },
                      {
                        id: "settings",
                        name: "Cấu hình hệ thống",
                        icon: Settings,
                        show: ["MASTER_DATA_MANAGE", "ROLE_MANAGE"].some((p) =>
                          can(p, false),
                        ),
                      },
                      {
                        id: "supplies",
                        name: "Danh mục vật tư & tài sản",
                        icon: Boxes,
                        show: ["ITEM_MASTER_MANAGE", "ASSET_MANAGE"].some((p) =>
                          can(p, false),
                        ),
                      },
                      {
                        id: "templates",
                        name: "Checklist & lịch định kỳ",
                        icon: ListChecks,
                        show: [
                          "CHECKLIST_MANAGE",
                          "PM_MANAGE",
                          "TASK_MANAGE",
                        ].some((p) => can(p, false)),
                      },
                      {
                        id: "reports",
                        name: "Báo cáo",
                        icon: ChartNoAxesCombined,
                        show: can("REPORT_VIEW"),
                      },
                      {
                        id: "users",
                        name: "Nhân sự & truy cập",
                        icon: Users,
                        show: can("USER_VIEW"),
                      },
                    ]
  ).filter((n) => n.show);
  const preferred = receptionOnly
    ? can("ROOM_VIEW")
      ? "rooms"
      : nav[0]?.id
    : housekeepingStaff || storekeeper || maintenanceLead || maintenanceStaff
      ? nav[0]?.id
      : management
        ? "dashboard"
        : can("SHIFT_MANAGE")
          ? "workday"
          : can("USER_MANAGE")
            ? "users"
            : can("CLEANING_EXECUTE") ||
                can("MAINTENANCE_EXECUTE") ||
                can("CLEANING_ASSIGN")
              ? "work"
              : can("DASHBOARD_VIEW", false)
                ? "dashboard"
                : can("INVENTORY_VIEW", false)
                  ? "stock"
                  : can("ROOM_VIEW")
                    ? "rooms"
                    : nav[0]?.id;
  const requestedPage =
    ownerOnly && ["assets", "stock", "documents"].includes(page)
      ? "resources"
      : ownerOnly &&
          page === "work" &&
          ["INCIDENT", "MAINTENANCE"].includes(
            new URLSearchParams(route.split("?")[1]).get("kind") || "",
          )
        ? "technical"
        : maintenanceStaff && page === "documents"
          ? "materials"
          : maintenanceStaff &&
              page === "work" &&
              new URLSearchParams(route.split("?")[1]).get("kind") ===
                "INCIDENT"
            ? "incidents"
            : maintenanceStaff &&
                page === "work" &&
                new URLSearchParams(route.split("?")[1]).get("kind") ===
                  "INTERNAL_REQUEST"
              ? "requests"
              : maintenanceStaff &&
                  page === "work" &&
                  new URLSearchParams(route.split("?")[1]).get("kind") ===
                    "OPERATIONAL"
                ? "otherwork"
                : maintenanceLead && page === "documents"
                  ? "requests"
                  : maintenanceLead &&
                      page === "work" &&
                      new URLSearchParams(route.split("?")[1]).get("kind") ===
                        "INCIDENT"
                    ? "incidents"
                    : storekeeper && page === "documents"
                      ? documentRoute(
                          new URLSearchParams(route.split("?")[1]).get(
                            "kind",
                          ) || "RECEIPT",
                        )
                      : housekeepingStaff && page === "work"
                        ? ["CLEANING", "REWORK"].includes(
                            new URLSearchParams(route.split("?")[1]).get(
                              "kind",
                            ) || "",
                          )
                          ? "cleaning"
                          : new URLSearchParams(route.split("?")[1]).get(
                                "kind",
                              ) === "INCIDENT"
                            ? "incidents"
                            : new URLSearchParams(route.split("?")[1]).get(
                                  "kind",
                                ) === "INTERNAL_REQUEST"
                              ? "requests"
                              : "work"
                        : operationsManager && page === "work"
                          ? ["CLEANING", "INSPECTION", "REWORK"].includes(
                              new URLSearchParams(route.split("?")[1]).get(
                                "kind",
                              ) || "",
                            )
                            ? "housekeeping"
                            : ["INCIDENT", "MAINTENANCE"].includes(
                                  new URLSearchParams(route.split("?")[1]).get(
                                    "kind",
                                  ) || "",
                                )
                              ? "technical"
                              : "work"
                          : departmentHead &&
                              page === "work" &&
                              new URLSearchParams(route.split("?")[1]).get(
                                "kind",
                              ) === "INTERNAL_REQUEST"
                            ? "requests"
                            : page;
  const active =
    page === "notifications"
      ? page
      : maintenanceStaff &&
          ((requestedPage === "history" && can("MAINTENANCE_VIEW")) ||
            (requestedPage === "requests" && can("IR_VIEW")) ||
            (requestedPage === "otherwork" && can("TASK_VIEW")))
        ? requestedPage
        : (storekeeper || maintenanceLead) &&
            page === "workday" &&
            can("ATTENDANCE_RECORD")
          ? page
          : housekeepingStaff && page === "documents" && can("MR_VIEW")
            ? page
            : operationsManager &&
                page === "documents" &&
                (can("MR_VIEW", false) || can("INVENTORY_VIEW", false))
              ? page
              : operationsManager &&
                  page === "audit" &&
                  can("AUDIT_VIEW", false)
                ? page
                : departmentHead &&
                    ((page === "rooms" && can("ROOM_VIEW")) ||
                      (page === "stock" && can("INVENTORY_VIEW", false)))
                  ? page
                  : nav.some((n) => n.id === requestedPage)
                    ? requestedPage
                    : preferred;
  useEffect(() => {
    const handler = () => {
      setPage(location.hash.slice(1).split("?")[0]);
      setRoute(location.hash);
    };
    window.addEventListener("hashchange", handler);
    return () => window.removeEventListener("hashchange", handler);
  }, []);
  useEffect(() => {
    // A different route/branch must not inherit the previous page's scroll.
    window.scrollTo({ top: 0, left: 0, behavior: "instant" });
    setOpen(false);
  }, [route, branch, ownerScope]);
  function navigate(id: string) {
    location.hash = id;
    setPage(id);
    setOpen(false);
    setError("");
  }
  return (
    <PageTitleTarget.Provider value={titleTarget}>
      <div className={`app-shell ${collapsed ? "collapsed" : ""}`}>
        <a
          className="skip-link"
          href="#main-content"
          onClick={(event) => {
            event.preventDefault();
            document.getElementById("main-content")?.focus();
          }}
        >
          Bỏ qua điều hướng
        </a>
        <aside className={open ? "sidebar open" : "sidebar"}>
          <a
            className="brand"
            href={`#${preferred}`}
            onClick={() => navigate(preferred)}
          >
            <img
              className="brand-logo"
              src="/duc-vuong-logo.jpg"
              alt="Đức Vượng"
            />
            <span className="brand-text">
              HotelOps<small>DUC VUONG HOTEL</small>
            </span>
          </a>
          {ownerOnly ? (
            <div className="branch-context">
              <label className="branch-current">
                <Building2 size={17} />
                <span>
                  Phạm vi giám sát
                  <select
                    aria-label="Chi nhánh giám sát"
                    value={ownerScope}
                    onChange={(e) => {
                      const id = Number(e.target.value);
                      setOwnerScope(id);
                      if (id) setBranch(id);
                    }}
                  >
                    <option value={0}>Tất cả chi nhánh</option>
                    {profile.branches.map((b) => (
                      <option key={b.branchId} value={b.branchId}>
                        {b.name} ({b.code})
                      </option>
                    ))}
                  </select>
                </span>
              </label>
            </div>
          ) : (
            <BranchContext />
          )}
          <nav aria-label="Điều hướng chính">
            {nav.map((n) => (
              <button
                key={n.id}
                title={n.name}
                aria-label={n.name}
                aria-current={
                  active === n.id ||
                  (maintenanceStaff &&
                    ((active === "history" && n.id === "notifications") ||
                      (["requests", "otherwork"].includes(active || "") &&
                        n.id === "materials"))) ||
                  (housekeepingStaff &&
                    ((active === "documents" && n.id === "requests") ||
                      (active === "notifications" && n.id === "incidents"))) ||
                  (operationsManager &&
                    ((active === "documents" && n.id === "stock") ||
                      (active === "audit" && n.id === "reports")))
                    ? "page"
                    : undefined
                }
                onClick={() => navigate(n.id)}
              >
                <n.icon size={19} />
                <span>{n.name}</span>
              </button>
            ))}
          </nav>
          <button
            className="collapse-toggle"
            onClick={() => setCollapsed(!collapsed)}
            aria-label={collapsed ? "Mở rộng điều hướng" : "Thu gọn điều hướng"}
          >
            <ChevronLeft size={18} />
            <span>Thu gọn</span>
          </button>
        </aside>
        {open && (
          <button
            className="nav-backdrop"
            aria-label="Đóng điều hướng"
            onClick={() => setOpen(false)}
          />
        )}
        <div className="workspace">
          <header className="topbar topbar-page">
            <button
              className="mobile-menu"
              aria-label="Mở điều hướng"
              onClick={() => setOpen(!open)}
            >
              <Menu size={21} />
            </button>
            <div className="topbar-heading" ref={setTitleTarget} />
            <div className="topbar-account">
              <button
                aria-label="Thông báo"
                onClick={() => navigate("notifications")}
              >
                <Bell size={20} />
              </button>
              <div className="account">
                <strong>{profile.user.fields.fullName}</strong>
                <small>
                  {profile.roles
                    .filter((r) => r.branchId === null || r.branchId === branch)
                    .map((r) => r.name)
                    .join(" · ")}
                </small>
              </div>
              <button
                aria-label="Đăng xuất"
                title="Đăng xuất"
                onClick={() => logout().catch((e) => setError(message(e)))}
              >
                <LogOut size={19} />
              </button>
            </div>
          </header>
          <main
            id="main-content"
            tabIndex={-1}
            key={`${branch}-${ownerOnly ? ownerScope : ""}-${active}-${location.hash.split("?")[1] || ""}`}
          >
            {error && (
              <p role="alert" className="alert">
                {error}
              </p>
            )}
            {!branch &&
            !ownerOnly &&
            !(
              [
                "dashboard",
                "catalog",
                "settings",
                "users",
                "notifications",
              ].includes(active) && management
            ) ? (
              <div className="empty">
                Tài khoản chưa được gán chi nhánh. Liên hệ quản trị để cấp phạm
                vi truy cập.
              </div>
            ) : (
              <Suspense
                fallback={
                  <div className="empty" role="status">
                    Đang mở không gian làm việc…
                  </div>
                }
              >
                {housekeepingStaff &&
                  [
                    "requests",
                    "documents",
                    "incidents",
                    "notifications",
                  ].includes(active) && (
                    <div className="tabs" aria-label="Nội dung">
                      {(["requests", "documents"].includes(active)
                        ? [
                            ["requests", "Yêu cầu nội bộ", "IR_VIEW"],
                            ["documents", "Yêu cầu vật tư", "MR_VIEW"],
                          ]
                        : [
                            ["incidents", "Sự cố", "INCIDENT_VIEW"],
                            ["notifications", "Thông báo", ""],
                          ]
                      )
                        .filter(([, , p]) => !p || can(p))
                        .map(([id, name]) => (
                          <button
                            key={id}
                            aria-pressed={active === id}
                            onClick={() => navigate(id)}
                          >
                            {name}
                          </button>
                        ))}
                    </div>
                  )}
                {operationsManager &&
                  ["stock", "documents", "reports", "audit"].includes(
                    active,
                  ) && (
                    <div className="tabs" aria-label="Nội dung">
                      {(["stock", "documents"].includes(active)
                        ? [
                            ["stock", "Tồn kho", "INVENTORY_VIEW"],
                            ["documents", "Yêu cầu & chứng từ", "MR_VIEW"],
                          ]
                        : [
                            ["reports", "Báo cáo", "REPORT_VIEW"],
                            ["audit", "Nhật ký", "AUDIT_VIEW"],
                          ]
                      )
                        .filter(([, , p]) => can(p, false))
                        .map(([id, name]) => (
                          <button
                            key={id}
                            aria-pressed={active === id}
                            onClick={() => navigate(id)}
                          >
                            {name}
                          </button>
                        ))}
                    </div>
                  )}
                {maintenanceLead &&
                  ["requests", "notifications"].includes(active || "") && (
                    <div className="tabs">
                      <a className="button-link" href="#requests">
                        Yêu cầu vật tư
                      </a>
                      <a className="button-link" href="#notifications">
                        Thông báo
                      </a>
                    </div>
                  )}
                {maintenanceStaff &&
                  ["materials", "requests", "otherwork"].includes(
                    active || "",
                  ) && (
                    <div className="tabs" aria-label="Nội dung yêu cầu">
                      {[
                        ["materials", "Vật tư", "MR_VIEW"],
                        ["requests", "Yêu cầu nội bộ", "IR_VIEW"],
                        ["otherwork", "Công việc khác", "TASK_VIEW"],
                      ]
                        .filter(([, , p]) => can(p))
                        .map(([id, name]) => (
                          <a
                            className="button-link"
                            aria-current={active === id ? "page" : undefined}
                            href={`#${id}`}
                            key={id}
                          >
                            {name}
                          </a>
                        ))}
                    </div>
                  )}
                {maintenanceStaff &&
                  ["notifications", "history"].includes(active || "") && (
                    <div className="tabs">
                      <a className="button-link" href="#notifications">
                        Thông báo
                      </a>
                      <a className="button-link" href="#history">
                        Lịch sử công việc
                      </a>
                    </div>
                  )}
                {ownerOnly ? (
                  <OwnerWorkspace page={active} scope={ownerScope} />
                ) : active === "dashboard" && maintenanceStaff ? (
                  <MaintenancePersonal />
                ) : active === "history" && maintenanceStaff ? (
                  <Work
                    personal
                    technicalStaff
                    historyMine
                    onlyKind="MAINTENANCE"
                    title="Lịch sử công việc cá nhân"
                  />
                ) : active === "materials" && maintenanceStaff ? (
                  <Documents materialOnly />
                ) : active === "otherwork" && maintenanceStaff ? (
                  <Work
                    personal
                    technicalStaff
                    onlyKind="OPERATIONAL"
                    title="Công việc khác"
                  />
                ) : active === "dashboard" && maintenanceLead ? (
                  <MaintenanceOverview />
                ) : active === "technicians" && maintenanceLead ? (
                  <MaintenanceOverview staff />
                ) : active === "review" && maintenanceLead ? (
                  <Work
                    onlyKind="MAINTENANCE"
                    coordinator
                    reviewOnly
                    title="Kiểm tra & Nghiệm thu"
                  />
                ) : active === "templates" && maintenanceLead ? (
                  <MaintenancePlans />
                ) : active === "dashboard" && storekeeper ? (
                  <InventoryDashboard />
                ) : storekeeper &&
                  [
                    "transactions",
                    "fulfillment",
                    "stocktaking",
                    "replenishment",
                    "history",
                  ].includes(active || "") ? (
                  <InventoryDocuments
                    group={
                      active as
                        | "transactions"
                        | "fulfillment"
                        | "stocktaking"
                        | "replenishment"
                        | "history"
                    }
                  />
                ) : active === "dashboard" && housekeepingStaff ? (
                  <HousekeepingPersonal />
                ) : active === "dashboard" &&
                  !management &&
                  !can("DASHBOARD_VIEW", false) ? (
                  <PersonalOverview />
                ) : active === "dashboard" && management ? (
                  <Management />
                ) : active === "catalog" ? (
                  <HotelWorkspace />
                ) : active === "settings" ? (
                  <Catalog mode="settings" />
                ) : active === "supplies" ? (
                  <>
                    {storekeeper && (
                      <p className="catalog-scope-note">
                        Danh mục mặt hàng dùng chung; định mức và kho áp dụng
                        theo chi nhánh đang chọn.
                      </p>
                    )}
                    <Catalog mode="supplies" inventoryMode={storekeeper} />
                  </>
                ) : active === "stock" ? (
                  can("INVENTORY_VIEW", false) ? (
                    <Stock />
                  ) : (
                    <Documents materialOnly />
                  )
                ) : active === "templates" ? (
                  <Templates />
                ) : active === "rooms" ? (
                  <Rooms />
                ) : active === "workday" ? (
                  <Workday />
                ) : active === "work" ? (
                  <Work
                    coordinator={maintenanceLead}
                    personal={housekeepingStaff || maintenanceStaff}
                    technicalStaff={maintenanceStaff}
                    title={
                      maintenanceLead || maintenanceStaff
                        ? "Công việc bảo trì"
                        : housekeepingStaff
                          ? "Công việc khác"
                          : undefined
                    }
                    onlyKind={
                      maintenanceLead || maintenanceStaff
                        ? "MAINTENANCE"
                        : departmentHead || housekeepingStaff
                          ? "OPERATIONAL"
                          : undefined
                    }
                    allowedKinds={
                      operationsManager
                        ? ["OPERATIONAL", "INTERNAL_REQUEST"]
                        : undefined
                    }
                  />
                ) : active === "cleaning" ? (
                  <Work
                    personal
                    allowedKinds={["CLEANING", "REWORK"]}
                    title="Công việc dọn phòng"
                  />
                ) : active === "incidents" ? (
                  can("INCIDENT_VIEW") ? (
                    <Work
                      coordinator={maintenanceLead}
                      technicalStaff={maintenanceStaff}
                      onlyKind="INCIDENT"
                      title={maintenanceLead ? "Sự cố kỹ thuật" : "Sự cố"}
                    />
                  ) : (
                    <Insights mode="notifications" />
                  )
                ) : active === "housekeeping" ? (
                  <Work
                    allowedKinds={["CLEANING", "INSPECTION", "REWORK"]}
                    title="Theo dõi buồng phòng"
                  />
                ) : active === "technical" ? (
                  <Work
                    allowedKinds={["INCIDENT", "MAINTENANCE"]}
                    title="Bảo trì & sự cố"
                  />
                ) : active === "requests" ? (
                  maintenanceLead || (housekeepingStaff && !can("IR_VIEW")) ? (
                    <Documents materialOnly />
                  ) : (
                    <Work
                      personal={housekeepingStaff || maintenanceStaff}
                      technicalStaff={maintenanceStaff}
                      onlyKind="INTERNAL_REQUEST"
                    />
                  )
                ) : active === "staff" ? (
                  <SchedulingStaff />
                ) : active === "documents" ? (
                  <Documents
                    materialOnly={departmentHead || housekeepingStaff}
                  />
                ) : active === "assets" ? (
                  <Assets maintenance={maintenanceLead || maintenanceStaff} />
                ) : active === "users" ? (
                  <Admin />
                ) : (
                  <Insights
                    mode={
                      (operationsManager &&
                      active === "reports" &&
                      !can("REPORT_VIEW", false)
                        ? "audit"
                        : active) as
                        "dashboard" | "reports" | "notifications" | "audit"
                    }
                  />
                )}
              </Suspense>
            )}
          </main>
        </div>
      </div>
    </PageTitleTarget.Provider>
  );
}
export default function App() {
  return (
    <Auth>
      <Shell />
    </Auth>
  );
}
