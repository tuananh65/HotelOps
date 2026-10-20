import { useState } from "react";
import { Link } from "react-router-dom";
import { pathFor, type Domain, type RecordData } from "./domain";
import { t } from "./i18n";
import * as service from "./services";
export function EntityInsights({
  domain,
  rows,
  onFilter,
}: {
  domain: Domain;
  rows: RecordData[];
  onFilter: (key: string, value: string) => void;
}) {
  const [group, setGroup] = useState("condition");
  if (domain.key === "assets") {
    const db = service.snapshot();
    const groups = new Map<
      string,
      { count: number; label: string; key: string; value: string }
    >();
    for (const r of rows) {
      const key = group === "location" ? (r.room ? "room" : "area") : group;
      const value = r[key] || "Unassigned";
      const label =
        group === "location"
          ? db.rooms.find((v) => v.id === value)?.name ||
            db.locations.find((v) => v.id === value)?.name ||
            "Unassigned"
          : value;
      const current = groups.get(key + value);
      groups.set(key + value, {
        count: (current?.count || 0) + 1,
        label,
        key,
        value,
      });
    }
    return (
      <section className="panel entity-insights">
        <div className="section-header">
          <h3>{t("Assets by location and condition")}</h3>
          <select
            aria-label={t("Group assets")}
            value={group}
            onChange={(e) => setGroup(e.target.value)}
          >
            <option value="condition">{t("Condition")}</option>
            <option value="location">{t("Location")}</option>
            <option value="category">{t("Category")}</option>
            <option value="department">{t("Custodian department")}</option>
          </select>
        </div>
        <div className="insight-groups">
          {Array.from(groups.values()).map((g) => (
            <button
              key={g.key + g.value}
              onClick={() =>
                onFilter(g.key, g.value === "Unassigned" ? "" : g.value)
              }
            >
              <span>{t(g.label)}</span>
              <strong>{g.count}</strong>
            </button>
          ))}
        </div>
      </section>
    );
  }
  if (domain.key === "shifts") {
    const db = service.snapshot();
    const dates = Array.from(new Set(rows.map((r) => r.date)))
      .filter(Boolean)
      .sort();
    return (
      <details className="panel entity-insights roster-overview">
        <summary>{t("Workday calendar & roster")}</summary>
        {dates.length ? (
          <div className="roster-grid">
            {dates.map((date) => (
              <div className="roster-day" key={date}>
                <h4>{date}</h4>
                {rows
                  .filter((r) => r.date === date)
                  .map((r) => (
                    <Link key={r.id} to={pathFor(72, r.id)}>
                      <strong>{t(r.name)}</strong>
                      <span>
                        {r.start} {t("—")}
                        {r.end}
                      </span>
                      <small>
                        {(r.assignees || [])
                          .map(
                            (id: string) =>
                              db.users.find((u) => u.id === id)?.name,
                          )
                          .join(", ") || "No employees assigned"}
                      </small>
                    </Link>
                  ))}
              </div>
            ))}
          </div>
        ) : (
          <p>
            {t(
              "No shifts scheduled in the current scope. Create a shift to plan the workday.",
            )}
          </p>
        )}
      </details>
    );
  }
  return null;
}
