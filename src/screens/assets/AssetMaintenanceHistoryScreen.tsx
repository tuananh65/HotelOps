import { useState } from "react";
import { useNavigate, useParams, useSearchParams } from "react-router-dom";
import { pathFor } from "../../domain";
import { t, useLanguage } from "../../i18n";
import * as service from "../../services";
import {
  DataTable,
  PageHeader,
  StatusBadge,
  date,
  name,
  useDB,
} from "../../ui";
export default function AssetMaintenanceHistoryScreen() {
  useLanguage();
  const id: number = 50;
  const db = useDB();
  const [params] = useSearchParams();
  const recordId = useParams().id;
  const nav = useNavigate();
  const [room, setRoom] = useState(params.get("room") || "");
  const rooms = service.visible("rooms");
  let rows = service.visible(
    id === 30 ? "cleaning" : id === 50 ? "incidents" : "inspections",
  );
  if (id === 30 && room) rows = rows.filter((r) => r.room === room);
  if (id === 50) rows = rows.filter((r) => r.asset === recordId);
  return (
    <>
      <PageHeader id={id} />
      {id === 30 && (
        <div className="filter-bar">
          <label>
            {t("Room")}{" "}
            <select value={room} onChange={(e) => setRoom(e.target.value)}>
              <option value="">{t("All rooms")}</option>
              {rooms.map((r) => (
                <option key={r.id} value={r.id}>
                  {t(r.name)}
                </option>
              ))}
            </select>
          </label>
        </div>
      )}
      <DataTable
        rows={rows}
        columns={[
          { key: "name", label: "Cycle / record" },
          { key: "room", label: "Room", render: (r) => name("rooms", r.room) },
          {
            key: "assignee",
            label: "Employee",
            render: (r) => name("users", r.assignee),
          },
          {
            key: "status",
            label: "Result",
            render: (r) => <StatusBadge status={r.status} />,
          },
          { key: "createdAt", label: "Time", render: (r) => date(r.createdAt) },
          {
            key: "asset",
            label: "Asset",
            render: (r) => name("assets", r.asset),
          },
          {
            key: "severity",
            label: "Severity",
            render: (r) => t(r.severity || "Normal"),
          },
          { key: "due", label: "Deadline", render: (r) => date(r.due) },
        ]}
        onOpen={(r) => nav(pathFor(id === 30 ? 26 : id === 50 ? 41 : 35, r.id))}
      />
    </>
  );
}
