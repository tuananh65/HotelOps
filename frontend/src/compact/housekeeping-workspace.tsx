import { lazy } from "react";
const Work = lazy(() => import("./work")),
  Workday = lazy(() => import("./workday")),
  Insights = lazy(() => import("./insights")),
  Overview = lazy(() => import("./housekeeping-overview")),
  CompletedHistory = lazy(() => import("./housekeeping-history"));

export default function HousekeepingWorkspace({ page }: { page: string }) {
  const params = new URLSearchParams(location.hash.split("?")[1]);
  if (page === "dashboard") return <Overview />;
  if (page === "workday") return <Workday />;
  if (page === "notifications") return <Insights mode="notifications" />;
  const history = page === "staff" || params.get("section") === "history";
  return <>
    <nav className="tabs housekeeping-tabs" aria-label="Công việc buồng phòng">
      <a className="button-link" aria-current={!history ? "page" : undefined} href="#work">Công việc hiện tại</a>
      <a className="button-link" aria-current={history ? "page" : undefined} href="#work?section=history">Lịch sử vệ sinh</a>
    </nav>
    {history ? <CompletedHistory /> : <Work housekeeping hideKindTabs title="Công việc hiện tại" allowedKinds={["CLEANING", "REWORK", "INSPECTION"]} />}
  </>;
}
