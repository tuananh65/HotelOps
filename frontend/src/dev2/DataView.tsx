import { Link } from "react-router-dom";
import { isObject } from "../auth/http";
import { label } from "./contracts";
import { fieldValue } from "../i18n/vi";

const destinations: Record<string, string> = {
  assetId: "assets",
  incidentId: "incidents",
  maintenanceWorkId: "maintenance",
  requestId: "material-requests",
  sourceMaterialRequestId: "material-requests",
  replenishmentId: "replenishments",
  existingActiveReplenishmentId: "replenishments",
  documentId: "inventory",
  stockDocumentId: "inventory",
};
function Scalar({ name, value }: { name: string; value: unknown }) {
  if (value === null || value === undefined) return <span>—</span>;
  if (typeof value === "number" && Number.isSafeInteger(value) && value > 0) {
    if (name === "handoverId")
      return (
        <Link to={`/operations/assets/${value}?action=A004A-003`}>{value}</Link>
      );
    if (name === "itemId")
      return (
        <Link to={`/operations/inventory/${value}?action=inventory:Item`}>
          {value}
        </Link>
      );
    if (name === "warehouseId")
      return (
        <Link to={`/operations/inventory/${value}?action=inventory:Warehouse`}>
          {value}
        </Link>
      );
  }
  const target = destinations[name];
  if (
    target &&
    typeof value === "number" &&
    Number.isSafeInteger(value) &&
    value > 0
  )
    return <Link to={`/operations/${target}/${value}`}>{String(value)}</Link>;
  return (
    <span>
      {fieldValue(name, value)}
    </span>
  );
}
export function DataView({
  data,
  title = "Kết quả",
}: {
  data: unknown;
  title?: string;
}) {
  if (Array.isArray(data)) {
    if (!data.length) return <p>Không có dữ liệu.</p>;
    if (!data.every(isObject))
      return (
        <ul>
          {data.map((v, i) => (
            <li key={i}>
              <Scalar name="" value={v} />
            </li>
          ))}
        </ul>
      );
    const keys = [...new Set(data.flatMap((v) => Object.keys(v)))].filter((k) =>
      data.every((v) => v[k] === null || typeof v[k] !== "object"),
    );
    return (
      <div className="dev2-table">
        <table>
          <caption>{title}</caption>
          <thead>
            <tr>
              {keys.map((k) => (
                <th key={k}>{label(k)}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {data.map((row, i) => (
              <tr key={i}>
                {keys.map((k) => (
                  <td key={k}>
                    <Scalar name={k} value={row[k]} />
                  </td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
        {data.map((row, i) =>
          Object.entries(row)
            .filter(([, v]) => v !== null && typeof v === "object")
            .map(([k, v]) => (
              <details key={`${i}:${k}`}>
                <summary>
                  Hồ sơ {i + 1}: {label(k)}
                </summary>
                <DataView data={v} title={label(k)} />
              </details>
            )),
        )}
      </div>
    );
  }
  if (!isObject(data)) return <Scalar name="" value={data} />;
  return (
    <section aria-label={title}>
      <dl className="dev2-record">
        {Object.entries(data)
          .filter(([, v]) => v === null || typeof v !== "object")
          .map(([k, v]) => (
            <div key={k}>
              <dt>{label(k)}</dt>
              <dd>
                <Scalar name={k} value={v} />
              </dd>
            </div>
          ))}
      </dl>
      {Object.entries(data)
        .filter(([, v]) => v !== null && typeof v === "object")
        .map(([k, v]) => (
          <section key={k}>
            <h3>{label(k)}</h3>
            <DataView data={v} title={label(k)} />
          </section>
        ))}
    </section>
  );
}
