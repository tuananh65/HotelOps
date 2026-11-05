import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { pathFor, type RecordData } from "../../domain";
import { t, useLanguage } from "../../i18n";
import * as service from "../../services";
import {
  DataTable,
  PageHeader,
  StatusBadge,
  Summary,
  date,
  name,
  notify,
  useDB,
} from "../../ui";
export default function InventoryDashboardScreen() {
  useLanguage();
  const id: number = 53;
  const db = useDB();
  const nav = useNavigate();
  const warehouses = service.visible("warehouses");
  const [warehouse, setWarehouse] = useState(warehouses[0]?.id || "");
  const [item, setItem] = useState("");
  const balances: RecordData[] = db.items.map((r) => ({
    ...r,
    branch: db.warehouses.find((w) => w.id === warehouse)?.branch || "",
    balance: service.balance(db, warehouse, r.id),
    minimum: service.minimumStock(r, warehouse),
  }));
  return (
    <>
      <PageHeader id={id} />
      <div className="filter-bar">
        <label>
          {t("Warehouse")}{" "}
          <select
            value={warehouse}
            onChange={(e) => setWarehouse(e.target.value)}
          >
            {warehouses.map((w) => (
              <option value={w.id} key={w.id}>
                {t(w.name)} {t("·")}
                {w.branch}
              </option>
            ))}
          </select>
        </label>
        <label>
          {t("Item")}{" "}
          <select value={item} onChange={(e) => setItem(e.target.value)}>
            <option value="">{t("All items")}</option>
            {db.items.map((i) => (
              <option key={i.id} value={i.id}>
                {t(i.name)}
              </option>
            ))}
          </select>
        </label>
      </div>
      <Summary
        items={[
          { label: "Items tracked", value: balances.length },
          {
            label: "Below minimum",
            value: balances.filter((i) => i.balance < Number(i.minimum)).length,
          },
          {
            label: "Posted movements",
            value: db.ledger.filter((l) => l.warehouse === warehouse).length,
          },
        ]}
      />
      <DataTable
        rows={balances.filter(
          (r) =>
            (!item || r.id === item) &&
            (id !== 53 || r.balance < Number(r.minimum)),
        )}
        columns={[
          { key: "name", label: "Item" },
          {
            key: "balance",
            label: "Available quantity",
            render: (r) => (
              <strong className={r.balance < r.minimum ? "overdue-text" : ""}>
                {r.balance}
              </strong>
            ),
          },
          { key: "unit", label: "Base unit" },
          { key: "minimum", label: "Minimum level" },
          {
            key: "status",
            label: "Stock health",
            render: (r) => (
              <StatusBadge
                status={
                  r.balance < Number(r.minimum) ? "Below minimum" : "Available"
                }
              />
            ),
          },
        ]}
        onOpen={(r) => nav(pathFor(55, r.id))}
      />
      {id === 61 && (
        <>
          <h2 className="section-title">{t("Posted stock movement ledger")}</h2>
          <DataTable
            rows={db.ledger.filter(
              (l) => l.warehouse === warehouse && (!item || l.item === item),
            )}
            columns={[
              {
                key: "createdAt",
                label: "Time",
                render: (r) => date(r.createdAt),
              },
              {
                key: "item",
                label: "Item",
                render: (r) => name("items", r.item),
              },
              { key: "name", label: "Document" },
              { key: "delta", label: "Movement" },
              {
                key: "balance",
                label: "Balance at movement",
                render: (r) =>
                  service.balance(db, r.warehouse, r.item, r.createdAt),
              },
            ]}
            onOpen={(r) => {
              if (r.document === "opening")
                notify("Opening balance is a seeded posted receipt.");
              else nav(pathFor(58, r.document));
            }}
          />
        </>
      )}
    </>
  );
}
