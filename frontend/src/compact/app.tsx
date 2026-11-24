import { AlertPopup } from "./alert-popup";
import PageTitle, { PageTitleTarget } from "./page-title";
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
  Wrench,
  ListTodo,
  ArrowLeftRight,
  ClipboardCheck,
  Truck,
  History,
  PackageSearch,
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
const HousekeepingPersonal = lazy(() => import("./housekeeping-personal"));
const RoomIssues = lazy(() => import("./room-issues"));
const MaintenanceWorkspace = lazy(() => import("./maintenance-workspace"));
const InventoryDashboard = lazy(() => import("./inventory-dashboard"));
const InventoryDocuments = lazy(() => import("./inventory-documents"));
const MaintenanceOverview = lazy(() => import("./maintenance-overview"));
const OwnerWorkspace = lazy(() => import("./owner-workspace"));
const HousekeepingWorkspace = lazy(() => import("./housekeeping-workspace"));
const CleaningPersonal = lazy(() => import("./cleaning-personal"));
const PersonalProfile = lazy(() => import("./personal-profile"));
const OperationsWorkspace = lazy(() => import("./operations-workspace"));
const AttendanceSummary = lazy(() => import("./attendance-summary"));
const SupplyConfiguration = lazy(() => import("./supply-configuration"));
const StaffSupplies = lazy(() => import("./staff-supplies"));
const Requests = lazy(() => import("./requests"));
const InternalSettings = lazy(() => import("./internal-settings"));
const OperationsConfiguration = lazy(() => import("./operations-configuration"));
function Shell() {
  const [profileOpen, setProfileOpen] = useState(false);
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
  const operationsManager =
    branchRoles.length > 0 &&
    branchRoles.every((r) => r.code === "OPERATION_MANAGER");
  const housekeepingStaff =
    branchRoles.length > 0 && branchRoles.every((r) => r.code === "HK_STAFF");
  const housekeepingSupervisor =
    branchRoles.length > 0 &&
    branchRoles.every((r) => r.code === "HOUSEKEEPING_SUPERVISOR");
  const maintenanceSupervisor = branchRoles.length > 0 && branchRoles.every(r => r.code === "MAINTENANCE_SUPERVISOR");
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
      : maintenanceSupervisor
        ? [
            {id:"dashboard",name:"Tổng quan bảo trì",icon:LayoutDashboard,show:can("DASHBOARD_VIEW") && can("MAINTENANCE_VIEW")},
            {id:"work",name:"Công việc bảo trì",icon:Wrench,show:can("MAINTENANCE_VIEW")},
            {id:"assets",name:"Tài sản & Thiết bị",icon:HardDrive,show:can("ASSET_VIEW")},
            {id:"history",name:"Lịch sử sửa chữa",icon:History,show:can("MAINTENANCE_VIEW")},
            {id:"requests",name:"Yêu cầu nội bộ",icon:Inbox,show:can("IR_VIEW")},
            {id:"workday",name:"Lịch làm việc",icon:CalendarDays,show:can("ATTENDANCE_RECORD")},
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
            : housekeepingSupervisor
              ? [
                  {
                    id: "dashboard",
                    name: "Tổng quan Housekeeping",
                    icon: LayoutDashboard,
                    show: can("DASHBOARD_VIEW", false),
                  },
                  {
                    id: "rooms",
                    name: "Tình trạng phòng",
                    icon: BedDouble,
                    show: false,
                  },
                  {
                    id: "work",
                    name: "Công việc",
                    icon: ClipboardList,
                    show: can("CLEANING_VIEW") || can("INSPECTION_VIEW"),
                  },
                  {
                    id: "workday",
                    name: "Lịch làm việc",
                    icon: CalendarDays,
                    show: can("ATTENDANCE_RECORD"),
                  },
                  {
                    id: "staff",
                    name: "Lịch sử vệ sinh",
                    icon: History,
                    show: false,
                  },
                  {
                    id: "incidents",
                    name: "Sự cố & Thông báo",
                    icon: Bell,
                    show: false,
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
                      id: "work",
                      name: "Công việc khác",
                      icon: ListTodo,
                      show: false,
                    },
                    {
                      id: "requests",
                      name: "Yêu cầu nội bộ",
                      icon: Package,
                      show: can("IR_VIEW"),
                    },
                    {
                      id: "workday",
                      name: "Lịch làm việc",
                      icon: CalendarDays,
                      show: can("ATTENDANCE_RECORD"),
                    },
                    {
                      id: "incidents",
                      name: "Thông báo",
                      icon: Bell,
                      show: false,
                    },
                  ]
                : operationsManager
                  ? [
                      {id:"dashboard",name:"Tổng quan vận hành",icon:LayoutDashboard,show:can("DASHBOARD_VIEW",false)},
                      {id:"rooms",name:"Tình trạng phòng",icon:BedDouble,show:can("ROOM_VIEW",false)},
                      {id:"workday",name:"Lịch làm việc",icon:CalendarDays,show:can("SHIFT_MANAGE",false)},
                      {id:"work",name:"Nhân sự & Công việc",icon:Users,show:can("TASK_VIEW",false)||can("SHIFT_MANAGE",false)},
                      {id:"housekeeping",name:"Buồng phòng & Bảo trì",icon:ListChecks,show:["CLEANING_VIEW","INSPECTION_VIEW","MAINTENANCE_VIEW","INCIDENT_VIEW"].some(p=>can(p,false))},
                      {id:"requests",name:"Yêu cầu nội bộ",icon:Inbox,show:can("IR_VIEW",false)},
                      {id:"assets",name:"Tài sản",icon:Boxes,show:can("ASSET_VIEW",false)},
                      {id:"stock",name:"Kho & Vật tư",icon:Package,show:can("INVENTORY_VIEW",false)},
                      {id:"documents",name:"Lịch sử yêu cầu vật tư",icon:ClipboardList,show:can("MR_VIEW",false)||can("INVENTORY_VIEW",false)},
                      {id:"reports",name:"Báo cáo & Nhật ký",icon:ChartNoAxesCombined,show:can("REPORT_VIEW",false)||can("AUDIT_VIEW",false)},
                    ]
                    : [
                        {
                          id: "dashboard",
                          name: "Tổng quan",
                          icon: LayoutDashboard,
                          show: !receptionOnly || can("ATTENDANCE_RECORD"),
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
                          show: receptionOnly ? can("IR_VIEW") : kinds.some((k) => can(k[2])),
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
                          show: ["MASTER_DATA_MANAGE", "ROLE_MANAGE"].some(
                            (p) => can(p, false),
                          ),
                        },
                        {
                          id: "supplies",
                          name: "Danh mục vật tư & tài sản",
                          icon: Boxes,
                          show: ["ITEM_MASTER_MANAGE", "ASSET_MANAGE"].some(
                            (p) => can(p, false),
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
  if (operationsManager && ["SHIFT_MANAGE","ROOM_SUPPLY_CONFIG","IR_VIEW"].some(p=>can(p,false)))
    nav.push({id:"configuration",name:"Cấu hình",icon:Settings,show:true});
  if (!operationsManager && can("ROOM_SUPPLY_CONFIG",false)) nav.push({id:"supply-config",name:"Cấu hình vật tư",icon:Settings,show:true});
  if (!housekeepingStaff && !housekeepingSupervisor && !storekeeper && can("STAFF_SUPPLY_VIEW")) nav.push({id:"staff-supplies",name:"Vật tư nhân viên",icon:Package,show:true});
  if (storekeeper && can("ATTENDANCE_RECORD")) nav.push({id:"workday",name:"Lịch làm việc",icon:CalendarDays,show:true});
  if ((housekeepingSupervisor || storekeeper) && can("IR_VIEW")) nav.push({id:"requests",name:"Yêu cầu nội bộ",icon:Inbox,show:true});
  if (!housekeepingStaff && !maintenanceSupervisor && !storekeeper && (can("INCIDENT_VIEW",false) || (receptionOnly && can("IR_CREATE")) || (storekeeper && can("MR_FULFILL",false))))
    nav.push({id:"room-issues",name:"Sự cố phòng",icon:TriangleAlert,show:true});
  const preferred = receptionOnly && can("ROOM_VIEW") ? "rooms" : nav[0]?.id || "dashboard";
  const params = new URLSearchParams(route.split("?")[1]);
  const kind = params.get("kind");
  let requestedPage = page;
  if (maintenanceSupervisor && ["room-issues","in-progress"].includes(page)) requestedPage="work";
  if (operationsManager && ["supply-config","internal-settings"].includes(page)) requestedPage="configuration";
  if (receptionOnly && page==="work" && can("IR_VIEW")) requestedPage="requests";
  if (housekeepingSupervisor && ["cleaning","inspection","rework","templates"].includes(page)) requestedPage="work";
  if (ownerOnly && ["assets","stock","documents"].includes(page)) requestedPage="resources";
  if (ownerOnly && page==="work" && ["INCIDENT","MAINTENANCE"].includes(kind || "")) requestedPage="technical";
  if (storekeeper && page==="documents" && params.get("transfer")!=="1") requestedPage=documentRoute(params.get("kind") || "RECEIPT");
  if (housekeepingStaff && page==="work" && ["CLEANING","REWORK"].includes(kind || "")) requestedPage="cleaning";
  if (page==="work" && kind==="INTERNAL_REQUEST" && can("IR_VIEW")) requestedPage="requests";
  if (operationsManager && (page==="technical" || (page==="work" && ["CLEANING","REWORK","INSPECTION","INCIDENT","MAINTENANCE"].includes(kind || "")))) requestedPage="housekeeping";
  const extraPages: Record<string,boolean> = {
    configuration:operationsManager && ["SHIFT_MANAGE","ROOM_SUPPLY_CONFIG","IR_VIEW"].some(p=>can(p,false)),
    "room-issues":can("INCIDENT_VIEW") || can("IR_CREATE") || (storekeeper && can("MR_FULFILL",false)),
    "supply-config":can("ROOM_SUPPLY_CONFIG",false), "staff-supplies":can("STAFF_SUPPLY_VIEW"),
    "internal-settings":operationsManager && can("IR_VIEW",false),
    notifications:true, documents:can("MR_VIEW") || can("INVENTORY_VIEW",false), audit:can("AUDIT_VIEW",false),
    staff:operationsManager && can("SHIFT_MANAGE",false), workday:can("ATTENDANCE_RECORD") || can("SHIFT_MANAGE",false),
    requests:can("IR_VIEW"), history:housekeepingStaff && can("CLEANING_VIEW"),
  };
  const active = nav.some(n=>n.id===requestedPage) || extraPages[requestedPage] ? requestedPage : preferred;
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
                aria-current={active === n.id ||
                  (operationsManager && ((active === "staff" && n.id === "work") || (active === "audit" && n.id === "reports")))
                    ? "page" : undefined}
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
              <button
                className="account account-profile-button"
                aria-label="Thông tin cá nhân"
                onClick={() => setProfileOpen(true)}
              >
                <strong>{profile.user.fields.fullName}</strong>
                <small>
                  {profile.roles
                    .filter((r) => r.branchId === null || r.branchId === branch)
                    .map((r) => r.name)
                    .join(" · ")}
                </small>
              </button>
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
              <AlertPopup>
                {error}
              </AlertPopup>
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
                {operationsManager && ["reports","audit"].includes(active) && (
                  <div className="tabs" aria-label="Nội dung">
                    {([["reports","Báo cáo","REPORT_VIEW"],["audit","Nhật ký","AUDIT_VIEW"]])
                      .filter(([, , permission])=>can(permission,false)).map(([id,name])=>(
                        <a className="button-link" key={id} href={"#"+id} aria-current={active===id?"page":undefined}>{name}</a>
                      ))}
                  </div>
                )}
                {active === "room-issues" ? <RoomIssues />
                  : ownerOnly ? <OwnerWorkspace page={active} scope={ownerScope} />
                  : active === "requests" ? <Requests />
                  : active === "configuration" ? <OperationsConfiguration />
                  : active === "internal-settings" ? <InternalSettings />
                  : active === "supply-config" ? <SupplyConfiguration />
                  : active === "staff-supplies" ? <StaffSupplies onRequestRefill={allocation=>{location.hash=`requests?create=CONSUMABLE_REFILL_REQUEST&allocation=${allocation.allocationId}`;}} />
                  : housekeepingSupervisor ? <HousekeepingWorkspace page={active} />
                  : operationsManager && ["work","staff","housekeeping"].includes(active) ? <OperationsWorkspace page={active} />
                  : maintenanceSupervisor && active === "dashboard" ? <MaintenanceOverview />
                  : maintenanceSupervisor && active === "work" ? <MaintenanceWorkspace />
                  : maintenanceSupervisor && active === "history" ? <Work onlyKind="MAINTENANCE" personal technicalStaff initialStatus="CLOSED" historyMine title="Lịch sử sửa chữa" />
                  : housekeepingStaff && active === "history" ? <CleaningPersonal historyPage />
                  : active === "dashboard" && housekeepingStaff ? <HousekeepingPersonal />
                  : active === "dashboard" && storekeeper ? <InventoryDashboard />
                  : active === "dashboard" && receptionOnly ? <><PageTitle><h1>Tổng quan cá nhân</h1><p>Lịch làm việc và thời gian đã làm của bạn</p></PageTitle><AttendanceSummary /></>
                  : active === "dashboard" && management ? <Management />
                  : active === "dashboard" && !can("DASHBOARD_VIEW",false) ? <PersonalOverview />
                  : storekeeper && ["transactions","fulfillment","stocktaking","replenishment","history"].includes(active) ? <InventoryDocuments group={active as "transactions"|"fulfillment"|"stocktaking"|"replenishment"|"history"} />
                  : active === "catalog" ? <HotelWorkspace />
                  : active === "settings" ? <Catalog mode="settings" />
                  : active === "supplies" ? <Catalog mode="supplies" inventoryMode={storekeeper} />
                  : active === "stock" ? (can("INVENTORY_VIEW",false)?<Stock />:<Documents materialOnly />)
                  : active === "templates" ? <Templates />
                  : active === "rooms" ? <Rooms />
                  : active === "workday" ? <Workday />
                  : active === "cleaning" ? <CleaningPersonal />
                  : active === "work" ? <Work personal={housekeepingStaff} onlyKind={housekeepingStaff?"OPERATIONAL":receptionOnly?"INTERNAL_REQUEST":undefined} />
                  : active === "incidents" ? <Insights mode="notifications" />
                  : active === "documents" ? <Documents materialOnly={housekeepingStaff} historyTitle={operationsManager} />
                  : active === "assets" ? <Assets maintenance={maintenanceSupervisor} />
                  : active === "users" ? <Admin />
                  : <Insights mode={(active === "reports" && !can("REPORT_VIEW",false)?"audit":active) as "dashboard"|"reports"|"notifications"|"audit"} />}
              </Suspense>
            )}
          </main>
        </div>
      </div>
      {profileOpen && (
        <Suspense fallback={<p role="status">Đang tải hồ sơ…</p>}>
          <PersonalProfile close={() => setProfileOpen(false)} />
        </Suspense>
      )}
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
