import { BedDouble } from "lucide-react";
import { useState } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";
import { pathFor } from "../../domain";
import { t, useLanguage } from "../../i18n";
import * as service from "../../services";
import { PageHeader, StatusBadge, Summary, name, useDB } from "../../ui";
export default function RoomReadinessBoardScreen() {
  useLanguage();
  useDB();
  const [params] = useSearchParams();
  const [status, setStatus] = useState(params.get("status") || "");
  const [floor, setFloor] = useState("");
  const nav = useNavigate();
  const rooms = service.visible("rooms");
  const floors = service.visible("locations").filter((l) => l.kind === "Floor");
  const statuses = [
    "Ready",
    "Pending Cleaning",
    "Cleaning In Progress",
    "Pending Inspection",
    "Rework Required",
    "Blocked",
  ];
  return (
    <>
      <PageHeader id={23} />
      <Summary
        items={["", ...statuses].map((s) => ({
          label: s || "All room statuses",
          value: rooms.filter(
            (r) => (!floor || r.floor === floor) && (!s || r.status === s),
          ).length,
          selected: status === s,
          onClick: () => setStatus(s),
        }))}
      />
      <div className="filter-bar board-floor-filter">
        <select
          aria-label={t("Floor")}
          value={floor}
          onChange={(e) => setFloor(e.target.value)}
        >
          <option value="">{t("All floors")}</option>
          {floors.map((f) => (
            <option key={f.id} value={f.id}>
              {t(f.name)}
            </option>
          ))}
        </select>
        <span className="quiet">
          {t("Statuses update automatically from workflow events.")}
        </span>
      </div>
      {floors
        .filter((f) => !floor || f.id === floor)
        .map((f) => (
          <section className="floor-section" key={f.id}>
            <div className="section-header">
              <h2>{t(f.name)}</h2>
              <span>{f.branch}</span>
            </div>
            <div className="room-grid">
              {rooms
                .filter(
                  (r) => r.floor === f.id && (!status || r.status === status),
                )
                .map((r) => (
                  <button
                    className={
                      "room-tile " + r.status.toLowerCase().replaceAll(" ", "-")
                    }
                    key={r.id}
                    onClick={() => nav(pathFor(20, r.id))}
                  >
                    <BedDouble size={24} strokeWidth={1} />
                    <strong>{t(r.name)}</strong>
                    <span>{name("roomTypes", r.roomType)}</span>
                    <StatusBadge status={r.status} />
                  </button>
                ))}
            </div>
          </section>
        ))}
    </>
  );
}
