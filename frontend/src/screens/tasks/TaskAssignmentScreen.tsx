import { useState } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";
import { domainFor, pathFor } from "../../domain";
import { t, useLanguage } from "../../i18n";
import * as service from "../../services";
import { Form, PageHeader, name, notify, useDB } from "../../ui";
export default function TaskAssignmentScreen() {
  useLanguage();
  const id: number = 68;
  const db = useDB();
  const d = domainFor(id);
  const [params] = useSearchParams();
  const [chosen, setChosen] = useState(
    params.get("record") || service.visible(d.key)[0]?.id || "",
  );
  const nav = useNavigate();
  const r = service.visible(d.key).find((r) => r.id === chosen);
  const allowedRoles =
    d.key === "cleaning"
      ? ["Housekeeping Staff"]
      : d.key === "incidents"
        ? ["Maintenance Staff", "Maintenance Lead"]
        : ["Housekeeping Staff", "Maintenance Staff", "Storekeeper"];
  const employees = db.users.filter(
    (u) =>
      u.status === "Active" &&
      service.inScope(u) &&
      u.roles.some((role: string) => allowedRoles.includes(role)) &&
      (!r ||
        (u.branch === r.branch &&
          (d.key === "incidents" || u.department === r.department))),
  );
  return (
    <>
      <PageHeader id={id} />
      <div className="detail-grid">
        <section className="panel">
          <h3>{t("Select work")}</h3>
          <select
            aria-label={t("Work record")}
            value={chosen}
            onChange={(e) => setChosen(e.target.value)}
          >
            <option value="">{t("Select a task")}</option>
            {service
              .visible(d.key)
              .filter(
                (r) =>
                  ![
                    "Closed",
                    "Completed",
                    "Cancelled",
                    "Pending Verification",
                    "Pending Inspection",
                  ].includes(r.status),
              )
              .map((r) => (
                <option key={r.id} value={r.id}>
                  {t(r.name)} {t("·")}
                  {t(r.status)}
                </option>
              ))}
          </select>
          {r && (
            <div key={r.id}>
              <h2>{t(r.name)}</h2>
              <p>
                {t("Current assignee:")}
                {name("users", r.assignee)}
              </p>
              <Form
                fields={[
                  {
                    key: "assignee",
                    label: "Assign employee",
                    type:
                      d.key === "cleaning"
                        ? "housekeeper"
                        : d.key === "incidents"
                          ? "technician"
                          : "user",
                    required: true,
                  },
                  {
                    key: "reason",
                    label: r.assignee
                      ? "Reassignment reason (10+ characters)"
                      : "Assignment notes",
                    type: "textarea",
                    required: !!r.assignee,
                  },
                ]}
                label="Confirm assignment"
                onSubmit={(v) => {
                  service.assign(d, r, v.assignee, v.reason || "");
                  notify("Assignment saved.");
                  nav(pathFor(d.detail, r.id));
                }}
              />
            </div>
          )}
        </section>
        <section className="panel">
          <h3>{t("Eligible employees & workload")}</h3>
          {employees.map((u) => (
            <div className="attention-row" key={u.id}>
              <span>
                <strong>{t(u.name)}</strong>
                <small>
                  {t(u.department)} {t("·")}
                  {u.username}
                </small>
              </span>
              <strong>
                {
                  ["cleaning", "tasks", "incidents", "rework"]
                    .flatMap((k) => db[k])
                    .filter(
                      (t) =>
                        t.assignee === u.id &&
                        !["Closed", "Completed", "Cancelled"].includes(
                          t.status,
                        ),
                    ).length
                }{" "}
                {t("open")}
              </strong>
            </div>
          ))}
        </section>
      </div>
    </>
  );
}
