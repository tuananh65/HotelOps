import { StatusBadge } from "../components/design-system";
import { Link } from "react-router-dom";
import { isObject } from "../auth/http";
import { label } from "./contracts";
import { fieldValue } from "../i18n/vi";
import { staffDate } from "../internalRequests/staffDate";

const technical =
  /(?:version|token|etag|eligible|restriction|unavailableReason|messageCode|^isNew|^linkedWorkAvailable|^availableLinkKinds|^submission|^mutation)/i;
const identity = /(?:Id|Ids)$/;
function visibleField(key: string, value: unknown, firstId?: string) {
  if (technical.test(key) || /^can[A-Z]/.test(key)) return false;
  if (["categoryCode", "severityCode", "priorityCode"].includes(key))
    return false;
  if (identity.test(key)) return key === firstId;
  if (typeof value === "number" && /(?:By|User|Actor)$/.test(key)) return false;
  return true;
}

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
function Scalar({
  name,
  value,
  linkTargets,
}: {
  name: string;
  value: unknown;
  linkTargets?: readonly string[];
}) {
  if (value === null || value === undefined) return <span>—</span>;
  if (
    typeof value === "string" &&
    /(?:At|Utc|Date)$/.test(name) &&
    /^\d{4}-\d\d-\d\dT/.test(value)
  )
    return <span>{staffDate(value)}</span>;
  if (typeof value === "number" && Number.isSafeInteger(value) && value > 0) {
    if (
      name === "handoverId" &&
      (!linkTargets || linkTargets.includes("assets"))
    )
      return (
        <Link to={`/operations/assets/${value}?action=A004A-003`}>{value}</Link>
      );
    if (
      name === "itemId" &&
      (!linkTargets || linkTargets.includes("inventory"))
    )
      return (
        <Link to={`/operations/inventory/${value}?action=inventory:Item`}>
          {value}
        </Link>
      );
    if (
      name === "warehouseId" &&
      (!linkTargets || linkTargets.includes("inventory"))
    )
      return (
        <Link to={`/operations/inventory/${value}?action=inventory:Warehouse`}>
          {value}
        </Link>
      );
  }
  if (/status$/i.test(name) && typeof value === "string")
    return <StatusBadge>{fieldValue(name, value)}</StatusBadge>;
  const target = destinations[name];
  if (
    target &&
    (!linkTargets || linkTargets.includes(target)) &&
    typeof value === "number" &&
    Number.isSafeInteger(value) &&
    value > 0
  )
    return <Link to={`/operations/${target}/${value}`}>{String(value)}</Link>;
  return <span>{fieldValue(name, value)}</span>;
}
export function DataView({
  data,
  title = "Kết quả",
  linkTargets,
}: {
  data: unknown;
  title?: string;
  linkTargets?: readonly string[];
}) {
  if (Array.isArray(data)) {
    if (!data.length) return <p>Không có dữ liệu.</p>;
    if (!data.every(isObject))
      return (
        <ul>
          {data.map((v, i) => (
            <li key={i}>
              <Scalar name="" value={v} linkTargets={linkTargets} />
            </li>
          ))}
        </ul>
      );
    const allKeys = [...new Set(data.flatMap((v) => Object.keys(v)))];
    const firstId = identity.test(allKeys[0] ?? "") ? allKeys[0] : undefined;
    const keys = allKeys.filter(
      (k) =>
        visibleField(k, data.find((v) => v[k] != null)?.[k], firstId) &&
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
                    <Scalar name={k} value={row[k]} linkTargets={linkTargets} />
                  </td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
        {data.map((row, i) =>
          Object.entries(row)
            .filter(
              ([k, v]) =>
                visibleField(k, v) && v !== null && typeof v === "object",
            )
            .map(([k, v]) => (
              <details key={`${i}:${k}`}>
                <summary>
                  Hồ sơ {i + 1}: {label(k)}
                </summary>
                <DataView data={v} title={label(k)} linkTargets={linkTargets} />
              </details>
            )),
        )}
      </div>
    );
  }
  if (!isObject(data))
    return <Scalar name="" value={data} linkTargets={linkTargets} />;
  return (
    <section aria-label={title}>
      <dl className="dev2-record">
        {Object.entries(data)
          .filter(
            ([k, v]) =>
              visibleField(k, v) && (v === null || typeof v !== "object"),
          )
          .map(([k, v]) => (
            <div key={k}>
              <dt>{label(k)}</dt>
              <dd>
                <Scalar name={k} value={v} linkTargets={linkTargets} />
              </dd>
            </div>
          ))}
      </dl>
      {Object.entries(data)
        .filter(
          ([k, v]) => visibleField(k, v) && v !== null && typeof v === "object",
        )
        .map(([k, v]) => (
          <section key={k}>
            <h3>{label(k)}</h3>
            <DataView data={v} title={label(k)} linkTargets={linkTargets} />
          </section>
        ))}
    </section>
  );
}
