import { Plus } from "lucide-react";
import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { domainFor, pathFor, type RecordData } from "../../domain";
import { t, useLanguage } from "../../i18n";
import { EntityInsights } from "../../insights";
import * as service from "../../services";
import "../../styles.css";
import {
  DataTable,
  createLabel,
  DomainFilterPanel,
  EntityForm,
  FieldControl,
  Modal,
  PageHeader,
  StatusBadge,
  Summary,
  date,
  name,
  notify,
  useDB,
} from "../../ui";
export default function TaskListScreen() {
  useLanguage();
  const id: number = 66;
  const db = useDB();
  const d = domainFor(id);
  const nav = useNavigate();
  const [creating, setCreating] = useState(false);
  const [editing, setEditing] = useState<RecordData>();
  const [filters, setFilters] = useState<Record<string, string>>({});
  let rows = service
    .visible(d.key)
    .filter((r) =>
      Object.entries(filters).every(
        ([key, value]) => !value || r[key] === value,
      ),
    );
  const filterFields = d.fields.filter(
    (f) =>
      ["select", "room", "roomType", "location", "asset", "warehouse"].includes(
        f.type || "",
      ) && !["branch", "department", "submission"].includes(f.key),
  );
  filterFields.push({
    key: "assignee",
    label: "Assigned employee",
    type: "user",
  });
  const count = (status: string) =>
    rows.filter((r) => r.status === status).length;
  const overdue = rows.filter(
    (r) =>
      r.due &&
      new Date(r.due) < new Date() &&
      !["Closed", "Completed", "Cancelled"].includes(r.status),
  ).length;
  const columns = [
    { key: "name", label: "Operational task" },
    {
      key: "room",
      label: "Room",
      render: (r: RecordData) => name("rooms", r.room),
    },
    {
      key: "assignee",
      label: "Assigned employee",
      render: (r: RecordData) => name("users", r.assignee),
    },
    { key: "priority", label: "Priority" },
    { key: "due", label: "Deadline", render: (r: RecordData) => date(r.due) },
    {
      key: "status",
      label: "Status",
      render: (r: RecordData) => <StatusBadge status={r.status} />,
    },
  ];
  const createRoute = undefined;
  return (
    <>
      <PageHeader id={id}>
        {service.can("UC-102") && (
          <button
            className="secondary"
            onClick={() => {
              try {
                notify(
                  service.generateRecurringTasks() +
                    " recurring tasks generated.",
                );
              } catch (e) {
                notify((e as Error).message);
              }
            }}
          >
            {t("Generate recurring work")}
          </button>
        )}

        {d.createUC && service.can(d.createUC) && (
          <button
            className="primary"
            onClick={() =>
              createRoute ? nav(pathFor(createRoute)) : setCreating(true)
            }
          >
            <Plus size={16} />
            {t(createLabel(d.key))}
          </button>
        )}
      </PageHeader>
      <Summary
        items={[
          {
            label: "Open tasks",
            value: rows.filter(
              (r) => !["Closed", "Cancelled"].includes(r.status),
            ).length,
          },
          { label: "In progress", value: count("In Progress") },
          {
            label: "Awaiting verification",
            value: count("Pending Verification"),
          },
          { label: "Overdue tasks", value: overdue },
        ]}
      />
      <EntityInsights
        domain={d}
        rows={rows}
        onFilter={(key, value) => setFilters({ ...filters, [key]: value })}
      />
      <DataTable
        extraFilters={
          filterFields.length > 0 && (
            <DomainFilterPanel
              activeCount={Object.values(filters).filter(Boolean).length}
            >
              {filterFields.map((f) => (
                <FieldControl
                  key={f.key}
                  field={{ ...f, required: false }}
                  value={filters[f.key]}
                  onChange={(v) => setFilters({ ...filters, [f.key]: v })}
                />
              ))}
              <button className="secondary" onClick={() => setFilters({})}>
                {t("Clear domain filters")}
              </button>
            </DomainFilterPanel>
          )
        }
        columns={columns}
        rows={rows}
        onOpen={(r) => nav(pathFor(d.detail, r.id))}
      />
      {creating && (
        <Modal title={t(createLabel(d.key))} onClose={() => setCreating(false)}>
          <EntityForm
            d={d}
            onCancel={() => setCreating(false)}
            onDone={(r) => {
              setCreating(false);
              nav(pathFor(d.detail, r.id));
            }}
          />
        </Modal>
      )}
      {editing && (
        <Modal title={editing.name} onClose={() => setEditing(undefined)}>
          <EntityForm
            d={d}
            record={editing}
            onDone={() => setEditing(undefined)}
          />
        </Modal>
      )}
    </>
  );
}
