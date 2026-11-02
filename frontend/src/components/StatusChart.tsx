import type { RecordData } from "../domain";
import { t, useLanguage } from "../i18n";
export function StatusChart({
  rows,
  title,
}: {
  rows: RecordData[];
  title: string;
}) {
  useLanguage();
  const counts = Object.entries(
    rows.reduce<Record<string, number>>((result, row) => {
      result[row.status] = (result[row.status] || 0) + 1;
      return result;
    }, {}),
  );
  const maximum = Math.max(...counts.map(([, count]) => count), 1);
  return (
    <section className="panel status-chart">
      <div className="chart-heading">
        <h2>{t(title)}</h2>
        <span>
          {rows.length} {t("records")}
        </span>
      </div>
      <div role="img" aria-label={t(title)}>
        {counts.map(([status, count]) => (
          <div className="chart-row" key={status}>
            <span>{t(status)}</span>
            <div className="chart-track">
              <div
                style={{
                  width: `${(count / maximum) * 100}%`,
                  background: /Blocked|Critical|Failed/.test(status)
                    ? "var(--chart-critical)"
                    : /Rework|Overdue|Rejected/.test(status)
                      ? "var(--chart-danger)"
                      : /Inactive|Disabled|Cancelled/.test(status)
                        ? "var(--gray-400)"
                        : /Ready|Active|Completed|Passed|Available|Accepted/.test(
                              status,
                            )
                          ? "var(--chart-success)"
                          : /Paused|Pending Cleaning/.test(status)
                            ? "var(--chart-orange)"
                            : /Pending|Waiting|Draft/.test(status)
                              ? "var(--chart-warning)"
                              : /Progress|Inspecting/.test(status)
                                ? "var(--chart-progress)"
                                : "var(--chart-primary)",
                }}
              />
            </div>
            <strong>{count}</strong>
          </div>
        ))}
      </div>
    </section>
  );
}
