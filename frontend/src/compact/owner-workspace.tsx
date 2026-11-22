import { lazy, useState } from "react";
import { useAuth } from "./auth";
const OwnerOverview = lazy(() => import("./owner-overview")),
  OwnerReports = lazy(() => import("./owner-reports")),
  Rooms = lazy(() => import("./rooms")),
  Work = lazy(() => import("./work")),
  Assets = lazy(() => import("./assets")),
  Stock = lazy(() => import("./stock")),
  Documents = lazy(() => import("./documents")),
  Insights = lazy(() => import("./insights"));
export default function OwnerWorkspace({
  page,
  scope,
}: {
  page: string;
  scope: number;
}) {
  const { canGlobal } = useAuth();
  const params = new URLSearchParams(location.hash.split("?")[1]);
  const route = location.hash.slice(1).split("?")[0];
  const tabs =
    page === "resources"
      ? [
          ["assets", "Tài sản", "ASSET_VIEW"],
          ["stock", "Tồn kho", "INVENTORY_VIEW"],
          ["materials", "Yêu cầu vật tư", "MR_VIEW"],
          ["documents", "Chứng từ kho", "INVENTORY_VIEW"],
        ]
      : page === "technical"
        ? [
            ["incidents", "Sự cố kỹ thuật", "INCIDENT_VIEW"],
            ["maintenance", "Bảo trì", "MAINTENANCE_VIEW"],
            ...(canGlobal("REPORT_VIEW")
              ? [["preventive", "Bảo trì định kỳ", "MAINTENANCE_VIEW"]]
              : []),
          ]
        : [];
  const visible = tabs.filter((t) => canGlobal(t[2]));
  const initial =
    route === "assets"
      ? "assets"
      : route === "stock"
        ? "stock"
        : route === "documents"
          ? params.get("kind") === "MATERIAL_REQUEST"
            ? "materials"
            : "documents"
          : route === "work"
            ? params.get("kind") === "MAINTENANCE"
              ? "maintenance"
              : "incidents"
            : params.get("tab");
  const [tab] = useState(
    visible.find((t) => t[0] === initial)?.[0] || visible[0]?.[0],
  );
  if (page === "dashboard" || page === "branches")
    return <OwnerOverview scope={scope} branchesOnly={page === "branches"} />;
  if (page === "rooms") return <Rooms readScope={scope} />;
  if (page === "reports") return <OwnerReports scope={scope} />;
  if (page === "notifications")
    return <Insights mode="notifications" readScope={scope} />;
  if (page === "audit") return <Insights mode="audit" />;
  if (page === "work")
    return (
      <Work
        readScope={scope}
        allowedKinds={[
          "CLEANING",
          "INSPECTION",
          "REWORK",
          "OPERATIONAL",
          "INTERNAL_REQUEST",
        ]}
        title="Công việc & Vận hành"
      />
    );
  return (
    <>
      {visible.length > 0 && (
        <div className="tabs">
          {visible.map(([v, n]) => (
            <a
              key={v}
              aria-current={v === tab ? "page" : undefined}
              className="button-link"
              href={`#${page}?tab=${v}`}
            >
              {n}
            </a>
          ))}
        </div>
      )}
      {page === "resources" ? (
        tab === "assets" ? (
          <Assets maintenance readScope={scope} />
        ) : tab === "stock" ? (
          <Stock readScope={scope} />
        ) : (
          <Documents readScope={scope} materialOnly={tab === "materials"} />
        )
      ) : tab === "preventive" ? (
        <OwnerReports scope={scope} onlyReport="preventive" />
      ) : (
        <Work
          readScope={scope}
          coordinator
          onlyKind={tab === "maintenance" ? "MAINTENANCE" : "INCIDENT"}
          title={
            tab === "maintenance"
              ? "Theo dõi bảo trì"
              : "Theo dõi sự cố kỹ thuật"
          }
        />
      )}
    </>
  );
}
