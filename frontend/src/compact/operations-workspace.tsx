import { useAuth } from "./auth";
import Work from "./work";
import SchedulingStaff from "./scheduling-staff";

export default function OperationsWorkspace({ page }: { page: string }) {
  const { can } = useAuth();
  if (page !== "housekeeping") return can("SHIFT_MANAGE", false) ? <SchedulingStaff/> : <p>Không có quyền xem nhân sự.</p>;
  const tabs = [["CLEANING", "Dọn phòng", "CLEANING_VIEW"], ["INSPECTION", "Kiểm tra phòng", "INSPECTION_VIEW"], ["REWORK", "Làm lại", "CLEANING_VIEW"], ["MAINTENANCE", "Bảo trì", "MAINTENANCE_VIEW"], ["INCIDENT", "Sự cố", "INCIDENT_VIEW"]].filter(([, , permission]) => can(permission, false));
  const requested = new URLSearchParams(location.hash.split("?")[1]).get("kind");
  const selected = tabs.find(([kind]) => kind === requested)?.[0] || tabs[0]?.[0];
  return <><div className="tabs" aria-label="Buồng phòng và bảo trì">{tabs.map(([kind, name]) => <a key={kind} className="button-link" aria-current={kind === selected ? "page" : undefined} href={`#housekeeping?kind=${kind}`}>{name}</a>)}</div>
    {selected ? <Work key={selected} onlyKind={selected} hideKindTabs title={tabs.find(([kind]) => kind === selected)?.[1]}/> : <p>Không có quyền xem nội dung này.</p>}</>;
}
