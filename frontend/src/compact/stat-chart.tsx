import { useId } from "react";
import { ChartNoAxesCombined, ChartPie } from "lucide-react";
export type Stat = { name: string; value: number; color?: string };
export default function StatChart({
  title,
  rows,
  href,
  link = "Xem chi tiết",
  type = "bars",
}: {
  title: string;
  rows: Stat[];
  href?: string;
  link?: string;
  type?: "bars" | "donut";
}) {
  const id = useId();
  const total = rows.reduce((n, r) => n + r.value, 0);
  const max = Math.max(1, ...rows.map((r) => r.value));
  const colors = ["#21665e", "#a56926", "#547a91", "#91564c", "#6b7099"];
  let offset = 0;
  return (
    <section className="panel stat-chart" aria-labelledby={id}>
      <div className="chart-heading">
        <h2 id={id}>
          {type === "donut" ? (
            <ChartPie size={22} aria-hidden="true" />
          ) : (
            <ChartNoAxesCombined size={22} aria-hidden="true" />
          )}
          {title}
        </h2>
        {href && <a href={href}>{link} →</a>}
      </div>
      {!total ? (
        <p className="empty">Chưa có dữ liệu để thống kê.</p>
      ) : (
        <div className={`chart-body ${type}`}>
          {type === "donut" && (
            <svg
              viewBox="0 0 160 160"
              role="img"
              aria-label={`${title}: tổng ${total}`}
            >
              {rows.map((r, i) => {
                const length = (r.value / total) * 100,
                  start = offset;
                offset += length;
                return (
                  <circle
                    key={r.name}
                    cx="80"
                    cy="80"
                    r="58"
                    fill="none"
                    stroke={r.color || colors[i % colors.length]}
                    strokeWidth="20"
                    pathLength="100"
                    strokeDasharray={`${length} ${100 - length}`}
                    strokeDashoffset={-start}
                    transform="rotate(-90 80 80)"
                  >
                    <title>
                      {r.name}: {r.value}
                    </title>
                  </circle>
                );
              })}
              <text x="80" y="79" textAnchor="middle" className="chart-total">
                {total}
              </text>
              <text x="80" y="99" textAnchor="middle" className="chart-caption">
                Tổng cộng
              </text>
            </svg>
          )}
          <ul className="chart-values">
            {rows.map((r, i) => (
              <li key={r.name}>
                <div>
                  <span
                    className="chart-dot"
                    style={{ background: r.color || colors[i % colors.length] }}
                  />
                  <span>{r.name}</span>
                  <strong>{r.value.toLocaleString("vi-VN")}</strong>
                </div>
                {type === "bars" && (
                  <div className="chart-track" aria-hidden="true">
                    <span
                      style={{
                        width: `${(r.value / max) * 100}%`,
                        background: r.color || colors[i % colors.length],
                      }}
                    />
                  </div>
                )}
              </li>
            ))}
          </ul>
        </div>
      )}
    </section>
  );
}
