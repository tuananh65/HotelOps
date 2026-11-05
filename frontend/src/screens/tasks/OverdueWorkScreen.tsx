import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { domains, pathFor, type RecordData } from "../../domain";
import { t, useLanguage } from "../../i18n";
import * as service from "../../services";
import {
  DataTable,
  Form,
  Modal,
  PageHeader,
  StatusBadge,
  Summary,
  date,
  name,
  notify,
  useDB,
} from "../../ui";
export default function OverdueWorkScreen() {
  useLanguage();
  const id: number = 70;
  const db = useDB();
  const nav = useNavigate();
  const [correction, setCorrection] = useState<RecordData>();
  const user = service.currentUser()!;
  const items: RecordData[] = [
    "tasks",
    "incidents",
    "cleaning",
    "rework",
    "requests",
  ].flatMap((key) => service.visible(key).map((r) => ({ ...r, entity: key })));
  const overdue = items.filter(
    (r) =>
      r.due &&
      new Date(r.due) < new Date() &&
      !["Closed", "Completed", "Cancelled", "Rejected"].includes(r.status),
  );
  const intervals = db.intervals.filter(
    (i) =>
      service.inScope(i) &&
      (user.roles.includes("Department Head")
        ? i.department === user.department
        : i.assignee === user.id),
  );
  return (
    <>
      <PageHeader id={id} />
      {id === 70 ? (
        <DataTable
          rows={overdue}
          columns={[
            { key: "name", label: "Overdue work" },
            { key: "entity", label: "Type" },
            {
              key: "assignee",
              label: "Employee",
              render: (r) => name("users", r.assignee),
            },
            { key: "due", label: "Deadline", render: (r) => date(r.due) },
            {
              key: "status",
              label: "Status",
              render: (r) => <StatusBadge status={r.status} />,
            },
          ]}
          onOpen={(r) =>
            nav(pathFor(domains.find((d) => d.key === r.entity)!.detail, r.id))
          }
        />
      ) : (
        <>
          <Summary
            items={[
              {
                label: "Open work",
                value: items.filter(
                  (r) =>
                    !["Closed", "Completed", "Cancelled"].includes(r.status),
                ).length,
              },
              { label: "Overdue work", value: overdue.length },
              {
                label: "Closed interval effort",
                value: `${Math.round(intervals.filter((i) => i.end).reduce((sum, i) => sum + (new Date(i.end).getTime() - new Date(i.start).getTime()) / 60000, 0))} min`,
                detail: "Provisional open intervals excluded",
              },
            ]}
          />
          <DataTable
            rows={items}
            onOpen={(r) =>
              nav(
                pathFor(domains.find((d) => d.key === r.entity)!.detail, r.id),
              )
            }
          />
          <h2 className="section-title">{t("Work intervals")}</h2>
          <DataTable
            rows={intervals}
            columns={[
              { key: "name", label: "Work item" },
              {
                key: "assignee",
                label: "Employee",
                render: (r) => name("users", r.assignee),
              },
              { key: "start", label: "Start", render: (r) => date(r.start) },
              {
                key: "end",
                label: "End",
                render: (r) => (r.end ? date(r.end) : "Open · provisional"),
              },
              {
                key: "effort",
                label: "Effort",
                render: (r) =>
                  r.end
                    ? `${Math.round((new Date(r.end).getTime() - new Date(r.start).getTime()) / 60000)} min`
                    : "Provisional",
              },
            ]}
            onOpen={(r) => setCorrection(r)}
          />
          {service.can("UC-111") && (
            <>
              <h2 className="section-title">{t("Time corrections")}</h2>
              {db.corrections
                .filter(
                  (c) => service.inScope(c) && c.department === user.department,
                )
                .map((c) => (
                  <div className="attention-row" key={c.id}>
                    <span>
                      {name("users", c.assignee)} {t("·")}
                      {date(c.start)} {t("→")} {date(c.end)}
                      <small>{c.reason}</small>
                    </span>
                    <StatusBadge status={c.status} />
                    {c.status === "Pending" &&
                      user.roles.includes("Department Head") && (
                        <div className="header-actions">
                          <button
                            className="secondary"
                            onClick={() => {
                              try {
                                service.applyCorrection(c, true);
                                notify("Correction applied.");
                              } catch (e) {
                                notify((e as Error).message);
                              }
                            }}
                          >
                            {t("Approve")}
                          </button>
                          <button
                            className="secondary"
                            onClick={() => {
                              try {
                                service.applyCorrection(c, false);
                                notify("Correction rejected.");
                              } catch (e) {
                                notify((e as Error).message);
                              }
                            }}
                          >
                            {t("Reject")}
                          </button>
                        </div>
                      )}
                  </div>
                ))}
            </>
          )}
        </>
      )}
      {correction && (
        <Modal
          title={t("Request time correction")}
          onClose={() => setCorrection(undefined)}
        >
          <Form
            fields={[
              {
                key: "start",
                label: "Proposed start",
                type: "datetime-local",
                required: true,
              },
              {
                key: "end",
                label: "Proposed end",
                type: "datetime-local",
                required: true,
              },
              {
                key: "reason",
                label: "Reason (10+ characters)",
                type: "textarea",
                required: true,
              },
            ]}
            onSubmit={(v) => {
              service.requestCorrection(correction, v.start, v.end, v.reason);
              notify("Correction submitted to Department Head.");
              setCorrection(undefined);
            }}
          />
        </Modal>
      )}
    </>
  );
}
