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
  name,
  useDB,
} from "../../ui";
export default function AssetListScreen() {
  useLanguage();
  const id: number = 48;
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
  const count = (status: string) =>
    rows.filter((r) => r.status === status).length;
  const overdue = rows.filter(
    (r) =>
      r.due &&
      new Date(r.due) < new Date() &&
      !["Closed", "Completed", "Cancelled"].includes(r.status),
  ).length;
  const columns = [
    { key: "name", label: "Asset" },
    { key: "code", label: "Asset code" },
    {
      key: "room",
      label: "Room",
      render: (r: RecordData) => name("rooms", r.room),
    },
    { key: "category", label: "Category" },
    { key: "condition", label: "Condition" },
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
          { label: "Asset records", value: rows.length },
          {
            label: "Active / published",
            value: rows.filter((r) =>
              ["Active", "Published"].includes(r.status),
            ).length,
          },
          {
            label: "Bui Vien",
            value: rows.filter((r) => r.branch === "Bui Vien").length,
          },
          {
            label: "Cao Thang",
            value: rows.filter((r) => r.branch === "Cao Thang").length,
          },
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
