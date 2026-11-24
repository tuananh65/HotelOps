import { assetConditionCounts } from "./asset-presentation";
import "./operations.css";

export default function AssetConditionBar({
  rows,
  href,
}: {
  rows: { condition: string; count: number }[];
  href?: string;
}) {
  const counts = assetConditionCounts(rows);
  const total = counts.good + counts.faulty + counts.other;
  const segments = [
    { key: "good", name: "Tốt", count: counts.good },
    { key: "faulty", name: "Hỏng", count: counts.faulty },
    { key: "other", name: "Khác", count: counts.other },
  ];
  return (
    <section className="panel asset-condition-summary">
      <h2>Tình trạng tài sản đang sử dụng</h2>
      <p>{total} tài sản</p>
      <div
        className="asset-condition-bar"
        role="img"
        aria-label={segments.map((s) => `${s.name}: ${s.count}`).join(", ")}
      >
        {segments
          .filter((s) => s.count > 0)
          .map((s) => (
            <span
              key={s.key}
              className={`asset-condition-${s.key}`}
              style={{ width: `${(s.count / total) * 100}%` }}
              title={`${s.name}: ${s.count}`}
            />
          ))}
      </div>
      <ul className="asset-condition-legend">
        {segments.map((s) => (
          <li key={s.key}>
            <i className={`asset-condition-${s.key}`} aria-hidden="true" />
            {s.name}: <strong>{s.count}</strong>
          </li>
        ))}
      </ul>
      {!total && (
        <p className="hint">
          Chưa có tài sản đang sử dụng trong phạm vi đã chọn.
        </p>
      )}
      <p className="hint">
        Khác gồm trung bình, kém, đang sửa chữa và thất lạc.
      </p>
      {href && <a href={href}>Xem tài sản →</a>}
    </section>
  );
}
