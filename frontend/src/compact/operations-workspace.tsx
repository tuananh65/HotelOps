import { useAuth } from "./auth";
import Work from "./work";
import SchedulingStaff from "./scheduling-staff";

/** Shared branch workspace; every tab still requires its server-issued grant. */
export default function OperationsWorkspace({ page }: { page: string }) {
  const { can } = useAuth();
  const monitoring = page === "housekeeping";
  const tabs = (monitoring
    ? [["CLEANING", "Dọn phòng", "CLEANING_VIEW"], ["INSPECTION", "Kiểm tra phòng", "INSPECTION_VIEW"], ["REWORK", "Làm lại", "CLEANING_VIEW"], ["MAINTENANCE", "Bảo trì", "MAINTENANCE_VIEW"], ["INCIDENT", "Sự cố", "INCIDENT_VIEW"]]
    : [["OPERATIONAL", "Công việc vận hành", "TASK_VIEW"], ["staff", "Nhân sự", "SHIFT_MANAGE"]])
    .filter(([, , permission]) => can(permission, false));
  const params = new URLSearchParams(location.hash.split("?")[1]);
  const requested = page === "staff" ? "staff" : params.get("kind");
  const selected = tabs.find(([kind]) => kind === requested)?.[0] || tabs[0]?.[0];
  return <>
    <div className="tabs" aria-label={monitoring ? "Buồng phòng và bảo trì" : "Nhân sự và công việc"}>
      {tabs.map(([kind, name]) => <a key={kind} className="button-link" aria-current={kind === selected ? "page" : undefined} href={`#${monitoring ? "housekeeping" : "work"}?kind=${kind}`}>{name}</a>)}
    </div>
    {selected === "staff" ? <SchedulingStaff /> : selected ? <Work key={selected} onlyKind={selected} hideKindTabs title={tabs.find(([kind]) => kind === selected)?.[1]} /> : <p>Không có quyền xem nội dung này.</p>}
  </>;
}
