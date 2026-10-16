import { Link, useNavigate } from "react-router-dom";
import { StatusChart } from "../../components/StatusChart";
import { pathFor } from "../../domain";
import { t, useLanguage } from "../../i18n";
import * as service from "../../services";
import {
  DataTable,
  PageHeader,
  StatusBadge,
  Summary,
  date,
  useDB,
} from "../../ui";
export default function DashboardScreen() {
  useLanguage();
  const db = useDB();
  const nav = useNavigate();
  const rooms = service.visible("rooms"),
    tasks = service.visible("tasks"),
    incidents = service.visible("incidents");
  const open = (status: string) =>
    !["Closed", "Completed", "Cancelled", "Rejected"].includes(status);
  const warehouse = service.visible("warehouses")[0];
  const low = db.items.filter(
    (item) =>
      warehouse &&
      service.balance(db, warehouse.id, item.id) <
        Number(service.minimumStock(item, warehouse.id)),
  );
  const critical = incidents
    .filter((r) => open(r.status))
    .sort(
      (a, b) =>
        Number(b.severity === "Critical") - Number(a.severity === "Critical"),
    );
  const overdue = tasks.filter(
    (r) => open(r.status) && r.due && new Date(r.due) < new Date(),
  );
  return (
    <>
      <PageHeader id={5}>
        <span className="snapshot">
          {t("Snapshot ·")} {date(new Date().toISOString())}
        </span>
      </PageHeader>
      <Summary
        variant="cards"
        items={[
          {
            label: "Ready rooms",
            value: rooms.filter((r) => r.status === "Ready").length,
            detail: rooms.length + " " + t("Total rooms"),
            to: "/room-readiness",
          },
          { label: "Open incidents", value: critical.length, to: pathFor(39) },
          { label: "Overdue tasks", value: overdue.length, to: pathFor(70) },
          { label: "Items below minimum", value: low.length, to: pathFor(53) },
        ]}
      />
      <nav className="daily-work" aria-label={t("Daily work")}>
        <strong>{t("Daily work")}</strong>
        <Link className="secondary" to={pathFor(24)}>
          {t("Cleaning jobs")}
        </Link>
        <Link className="secondary" to={pathFor(43)}>
          {t("Maintenance jobs")}
        </Link>
        <Link className="secondary" to={pathFor(62)}>
          {t("Material requests")}
        </Link>
        <Link className="secondary" to={pathFor(70)}>
          {t("Overdue tasks")}
        </Link>
        <Link to={pathFor(6)}>
          {t("Notifications")}:{" "}
          {
            service
              .visible("notifications")
              .filter((r) => r.status === "Unread").length
          }
        </Link>
      </nav>
      <div className="prototype-charts">
        <StatusChart rows={rooms} title="Room readiness" />
        <StatusChart
          rows={tasks.filter((r) => open(r.status))}
          title="Task workload"
        />
      </div>
      <div className="dashboard-grid">
        <section className="panel">
          <div className="section-header">
            <h3>{t("Critical incidents")}</h3>
            <Link to={pathFor(39)}>{t("View all")}</Link>
          </div>
          <DataTable
            compact
            rows={critical.slice(0, 4)}
            columns={[
              { key: "name", label: "Incident" },
              { key: "priority", label: "Priority" },
              {
                key: "status",
                label: "Status",
                render: (r) => <StatusBadge status={r.status} />,
              },
            ]}
            onOpen={(r) => nav(pathFor(41, r.id))}
          />
        </section>
        <section className="panel">
          <div className="section-header">
            <h3>{t("Low stock")}</h3>
            <Link to={pathFor(53)}>{t("View all")}</Link>
          </div>
          <DataTable
            compact
            rows={low.slice(0, 4)}
            columns={[
              { key: "name", label: "Item" },
              {
                key: "balance",
                label: "Available",
                render: (r) =>
                  warehouse ? service.balance(db, warehouse.id, r.id) : 0,
              },
              { key: "unit", label: "Base unit" },
            ]}
            onOpen={(r) => nav(pathFor(55, r.id))}
          />
        </section>
      </div>
      <section className="panel">
        <div className="section-header">
          <h3>{t("Today's work")}</h3>
          <Link to={pathFor(66)}>{t("View all")}</Link>
        </div>
        <DataTable
          compact
          rows={tasks.filter((r) => open(r.status)).slice(0, 5)}
          columns={[
            { key: "name", label: "Task" },
            { key: "department", label: "Department" },
            { key: "due", label: "Deadline", render: (r) => date(r.due) },
            {
              key: "status",
              label: "Status",
              render: (r) => <StatusBadge status={r.status} />,
            },
          ]}
          onOpen={(r) => nav(pathFor(67, r.id))}
        />
      </section>
    </>
  );
}
