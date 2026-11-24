import Work from "./work";
import HousekeepingHistory from "./housekeeping-history";
export default function CleaningPersonal({historyPage=false}:{historyPage?:boolean}) {
  const history = historyPage || new URLSearchParams(location.hash.split("?")[1]).get("section") === "history";
  return <>
    <nav className="tabs housekeeping-tabs" aria-label="Công việc dọn phòng">
      <a className="button-link" aria-current={!history ? "page" : undefined} href="#cleaning">Công việc hiện tại</a>
      <a className="button-link" aria-current={history ? "page" : undefined} href="#cleaning?section=history">Lịch sử vệ sinh</a>
    </nav>
    {history ? <HousekeepingHistory personal /> : <Work personal cleaningCurrent hideKindTabs allowedKinds={["CLEANING", "REWORK"]} title="Công việc dọn phòng" />}
  </>;
}
