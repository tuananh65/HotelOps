import { useState } from "react";
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
export default function WorkdayRecordScreen() {
  useLanguage();
  const db = useDB();
  const user = service.currentUser()!;
  const [starting, setStarting] = useState(false);
  const active = db.workdays.find(
    (w) => w.assignee === user.id && w.status === "Working",
  );
  return (
    <>
      <PageHeader id={73}>
        <button
          className="primary"
          disabled={service.isPending("workday")}
          onClick={async () => {
            if (!active) setStarting(true);
            else {
              try {
                await service.workday(false, "");
                notify("Workday ended.");
              } catch (e) {
                notify((e as Error).message);
              }
            }
          }}
        >
          {active ? "End workday" : "Start workday"}
        </button>
      </PageHeader>
      <Summary
        items={[
          {
            label: "Workday status",
            value: active ? "Working" : "Not started",
          },
          { label: "Actual start", value: active ? date(active.start) : "—" },
          {
            label: "Open work interval",
            value: db.intervals.filter((i) => i.assignee === user.id && !i.end)
              .length,
          },
        ]}
      />
      <DataTable
        rows={db.workdays.filter(
          (w) =>
            service.inScope(w) &&
            (user.roles.includes("Department Head")
              ? w.department === user.department
              : w.assignee === user.id),
        )}
        columns={[
          {
            key: "assignee",
            label: "Employee",
            render: (r) => name("users", r.assignee),
          },
          { key: "start", label: "Actual start", render: (r) => date(r.start) },
          { key: "end", label: "Actual end", render: (r) => date(r.end) },
          {
            key: "status",
            label: "Status",
            render: (r) => <StatusBadge status={r.status} />,
          },
        ]}
      />
      {starting && (
        <Modal title={t("Start workday")} onClose={() => setStarting(false)}>
          <p>
            {t(
              "Starting without a shift assignment requires a reason of at least 10 characters.",
            )}
          </p>
          <Form
            fields={[
              { key: "reason", label: "Reason / notes", type: "textarea" },
            ]}
            label="Start workday"
            onSubmit={async (v) => {
              await service.workday(true, v.reason || "");
              setStarting(false);
              notify("Workday started.");
            }}
          />
        </Modal>
      )}
    </>
  );
}
