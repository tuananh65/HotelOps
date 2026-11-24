import { lazy } from "react";
import { useAuth } from "./auth";
const Work = lazy(() => import("./work")),
  Rooms = lazy(() => import("./rooms")),
  Workday = lazy(() => import("./workday")),
  Insights = lazy(() => import("./insights")),
  Overview = lazy(() => import("./housekeeping-overview")),
  CompletedHistory = lazy(() => import("./housekeeping-history")),
  Queue = lazy(() => import("./housekeeping-review-queue"));

export default function HousekeepingWorkspace({ page: workspacePage }: { page: string }) {
  const { can } = useAuth();
  const params = new URLSearchParams(location.hash.split("?")[1]);
  const rawPage = location.hash.slice(1).split("?")[0];
  const groups = [
    ["cleaning", "Phân công dọn phòng", "CLEANING_VIEW"],
    ["inspection", "Kiểm tra phòng", "INSPECTION_VIEW"],
    ["rework", "Xử lý làm lại", "CLEANING_VIEW"],
    ["history", "Lịch sử vệ sinh", "CLEANING_VIEW"],
  ].filter(([, , permission]) => can(permission));
  const requestedGroup = rawPage === "templates" ? "inspection" :
    ["cleaning", "inspection", "rework"].includes(rawPage) ? rawPage :
    params.get("section") || ({CLEANING:"cleaning", INSPECTION:"inspection", REWORK:"rework"} as Record<string,string>)[params.get("kind") || ""];
  const page = workspacePage === "work" ? groups.find(([key]) => key === requestedGroup)?.[0] || groups[0]?.[0] || "cleaning" : workspacePage;
  const tabs =
    page === "inspection"
      ? [
          ["waiting", "Phòng chờ kiểm tra", "INSPECTION_VIEW"],
          ["records", "Kết quả kiểm tra", "INSPECTION_VIEW"],
        ]
      : page === "rework"
        ? [
            ["work", "Công việc làm lại", "CLEANING_VIEW"],
            ["defects", "Lỗi kiểm tra", "INSPECTION_VIEW"],
          ]
        : page === "incidents"
          ? [
              ["incidents", "Sự cố tại phòng", "INCIDENT_VIEW"],
              ["notifications", "Thông báo", ""],
            ]
          : [];
  const visible = tabs.filter(
    ([, , permission]) => !permission || can(permission),
  );
  const tab = (
    visible.find(
      (t) =>
        t[0] ===
        (params.get("tab") ||
          (rawPage === "templates"
            ? "checklists"
            : params.has("id") || params.has("create")
              ? "records"
              : undefined)),
    )?.[0] || visible[0]?.[0]
  );
  if (page === "dashboard") return <Overview />;
  if (page === "rooms") return <Rooms housekeeping />;
  if (page === "staff") return <CompletedHistory />;
  if (page === "workday") return <Workday />;
  if (page === "notifications") return <Insights mode="notifications" />;
  return (
    <>
      {workspacePage === "work" && <div className="tabs" aria-label="Công việc buồng phòng">
        {groups.map(([key, name]) => <a key={key} className="button-link" aria-current={page === key ? "page" : undefined} href={`#work?section=${key}`}>{name}</a>)}
      </div>}
      {!!visible.length && (
        <div className="tabs" aria-label="Nội dung buồng phòng">
          {visible.map(([key, name]) => (
            <a
              key={key}
              className="button-link"
              aria-current={tab === key ? "page" : undefined}
              href={workspacePage === "work" ? `#work?section=${page}&tab=${key}` : `#${page}?tab=${key}`}
            >
              {name}
            </a>
          ))}
        </div>
      )}
      {page === "history" ? <CompletedHistory /> : page === "inspection" && tab === "waiting" ? (
        <Queue />
      ) : page === "rework" && tab === "defects" ? (
        <Queue defects />
      ) : page === "incidents" && tab === "notifications" ? (
        <Insights mode="notifications" />
      ) : (
        <Work
          key={page}
          housekeeping
          hideKindTabs
          initialKind={
            page === "inspection"
              ? "INSPECTION"
              : page === "rework"
                ? "REWORK"
                : page === "incidents"
                  ? "INCIDENT"
                  : "CLEANING"
          }
          allowedKinds={
            page === "incidents"
              ? ["INCIDENT"]
              : ["CLEANING", "INSPECTION", "REWORK"]
          }
        />
      )}
    </>
  );
}
