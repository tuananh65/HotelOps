import { lazy, Suspense } from "react";
import { useAuth } from "./auth";
import PageTitle, { PageTitleTarget } from "./page-title";
import { Empty } from "./ui";
import "./operations-configuration.css";

const WorkdaySettings = lazy(() => import("./workday-settings"));
const SupplyConfiguration = lazy(() => import("./supply-configuration"));
const InternalSettings = lazy(() => import("./internal-settings"));
const HousekeepingChecklists = lazy(() => import("./housekeeping-checklists"));
const MaintenanceTemplates = lazy(() => import("./templates"));

export default function OperationsConfiguration() {
  const { can, branch } = useAuth();
  const tabs = [
    { id: "checklists", name: "Checklist", show: can("CHECKLIST_MANAGE", false) },
    { id: "maintenance", name: "Mẫu & lịch bảo trì", show: can("CHECKLIST_MANAGE", false) || can("PM_MANAGE", false) },
    { id: "shifts", name: "Ca làm việc", show: can("SHIFT_MANAGE", false) },
    { id: "supplies", name: "Vật tư phòng", show: can("ROOM_SUPPLY_CONFIG", false) },
    { id: "requests", name: "Yêu cầu nội bộ", show: can("IR_VIEW", false) },
    { id: "room", name: "Báo cáo phòng", show: can("ROOM_SUPPLY_CONFIG", false) && (can("IR_CREATE") || can("INCIDENT_VIEW", false)) },
  ].filter(t => t.show);
  const [route, query] = location.hash.slice(1).split("?");
  const requested = route === "templates" ? "maintenance" : route === "supply-config" ? "supplies"
    : route === "internal-settings" ? new URLSearchParams(query).get("tab") === "room" ? "room" : "requests"
    : new URLSearchParams(query).get("tab") || tabs[0]?.id;
  const selected = tabs.find(t => t.id === requested);
  return <section className="operations-configuration">
    <PageTitle><h1>Cấu hình</h1><p>Quản lý cấu hình vận hành theo chi nhánh</p></PageTitle>
    <nav className="configuration-tabs" aria-label="Nhóm cấu hình">
      {tabs.map(tab => <a className="button-link" key={tab.id} href={`#configuration?tab=${tab.id}`} aria-current={selected?.id === tab.id ? "page" : undefined}>{tab.name}</a>)}
    </nav>
    {!selected ? <Empty>Bạn chưa có quyền truy cập nhóm cấu hình này.</Empty> : <>
      <h2 className="configuration-heading">{selected.name}</h2>
      <PageTitleTarget.Provider value={null}>
        <Suspense fallback={<Empty>Đang tải cấu hình…</Empty>}>
          <div key={`${branch}-${selected.id}`}>
            {selected.id === "shifts" ? <WorkdaySettings close={() => { location.hash = "workday"; }} />
              : selected.id === "checklists" ? <HousekeepingChecklists embedded />
              : selected.id === "maintenance" ? <MaintenanceTemplates embedded />
              : selected.id === "supplies" ? <SupplyConfiguration />
              : <InternalSettings section={selected.id === "room" ? "room" : "requests"} />}
          </div>
        </Suspense>
      </PageTitleTarget.Provider>
    </>}
  </section>;
}
