import { useEffect, useRef, useState } from "react";
import {
  Link,
  Navigate,
  Route,
  Routes,
  useParams,
  useSearchParams,
} from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import { isObject } from "../auth/http";
import { operations } from "./catalog.generated";
import { createDev2Api, operationPath, payload } from "./api";
import { modules, label, type Operation, type Values } from "./contracts";
import { Fields, defaults } from "./Fields";
import { DataView } from "./DataView";
import { IncidentCategoryField } from "./IncidentCategoryField";
import { MaterialApproverField } from "./MaterialApproverField";
import { MaterialCatalogFields } from "./MaterialCatalogFields";

const identities = new WeakMap<object, number>();
let identity = 0;
function key(session: object) {
  if (!identities.has(session)) identities.set(session, ++identity);
  return identities.get(session);
}
function readAction(operation: Operation, fallback: string): string {
  if (["A004A-003", "A004A-004", "A004A-005"].includes(operation.id))
    return "A004A-003";
  if (operation.module === "inventory") {
    if (/^\/api\/inventory\/items/.test(operation.route))
      return operation.route.includes("conversions/")
        ? "inventory:Conversion"
        : "inventory:Item";
    if (/^\/api\/inventory\/units/.test(operation.route))
      return "inventory:Unit";
    if (/^\/api\/inventory\/warehouses/.test(operation.route))
      return operation.route.includes("threshold")
        ? "inventory:Thresholds"
        : "inventory:Warehouse";
    if (/^\/api\/inventory\/stocktakes/.test(operation.route))
      return "inventory:Stocktake";
  }
  return fallback;
}
export function Dev2Workspace({ readOnly = false, enabledModules, enabledActions }: { readOnly?: boolean; enabledModules?: string[]; enabledActions?: string[] } = {}) {
  const { state, actions } = useAuth();
  if (state.status === "RESTRICTED" || state.session?.mustChangePassword)
    return <Navigate to="/change-password" replace />;
  if (state.status === "ANONYMOUS" || state.status === "EXPIRED_INVALID")
    return <Navigate to="/login" replace />;
  if (
    state.status !== "AUTHENTICATED" ||
    !state.session ||
    state.pending !== null
  )
    return (
      <main>
        <p role="status">Confirm your session to load operations.</p>
        <button onClick={() => void actions.revalidate()}>Check session</button>
      </main>
    );
  return (
    <Routes>
      <Route
        path=":module/:id?"
        element={<Workspace key={key(state.session)} readOnly={readOnly} enabledModules={enabledModules} enabledActions={enabledActions} />}
      />
      <Route
        path="*"
        element={<Navigate replace to="/operations/incidents" />}
      />
    </Routes>
  );
}
function Workspace({ readOnly, enabledModules, enabledActions }: { readOnly: boolean; enabledModules?: string[]; enabledActions?: string[] }) {
  const { module = "incidents", id = "" } = useParams();
  const { state, actions } = useAuth();
  const [query, setQuery] = useSearchParams();
  const config = modules.find((m) => m.id === module);
  if (!config || enabledModules && !enabledModules.includes(module)) return <p role="alert">This destination is not activated.</p>;
  const context: Values = {};
  for (const name of [
    "branchId",
    "departmentId",
    "incidentId",
    "sourceWarehouseId",
    "warehouseId",
    "assetId",
    "handoverId",
    "fulfillmentId",
    "conversionId",
    "itemId",
  ])
    if (query.has(name)) context[name] = query.get(name)!;
  return (
    <Screen
      readOnly={readOnly || enabledActions !== undefined && !operations.some(op => op.module === module && op.method !== "GET" && enabledActions.includes(op.id))}
      enabledModules={enabledModules}
      enabledActions={enabledActions}
      key={`${module}:${id}:${query.toString()}`}
      module={module}
      recordId={id}
      actor={state.session!.user.id}
      config={config}
      context={context}
      actionId={query.get("action") ?? undefined}
      select={(a) => {
        const next = new URLSearchParams(query);
        next.set("action", a);
        setQuery(next);
      }}
      expired={() => void actions.revalidate()}
    />
  );
}
function Screen({
  readOnly,
  enabledModules,
  enabledActions,
  module,
  recordId,
  actor,
  config,
  context,
  actionId,
  select,
  expired,
}: {
  readOnly: boolean;
  enabledModules?: string[];
  enabledActions?: string[];
  module: string;
  recordId: string;
  actor: string;
  config: (typeof modules)[number];
  context: Values;
  actionId?: string;
  select: (v: string) => void;
  expired: () => void;
}) {
  const available = operations.filter((x) => x.module === module && (!readOnly || x.method === "GET")
    && (!enabledActions || enabledActions.includes(x.id)));
  const initial = available.find(
    (x) => x.id === (actionId ?? (recordId ? config.detail : config.list)),
  )!;
  const operation = initial ?? available.find((x) => x.id === config.list)!;
  const [parameters, setParameters] = useState<Values>(() => ({
    id: recordId,
    assetId: recordId,
    incidentId: recordId,
    handoverId: recordId,
    fulfillmentId: context.fulfillmentId ?? "",
    conversionId: context.conversionId ?? "",
    warehouseId: context.warehouseId ?? recordId,
    itemId: context.itemId ?? "",
  }));
  const [values, setValues] = useState<Values>(() =>
    defaults(operation.fields, context),
  );
  const [record, setRecord] = useState<Values>({});
  const [data, setData] = useState<unknown>();
  const [message, setMessage] = useState("");
  const [busy, setBusy] = useState(false);
  const [denied, setDenied] = useState(false);
  const [sessionLost, setSessionLost] = useState(false);
  const [uncertain, setUncertain] = useState(false);
  const [revision, setRevision] = useState(0);
  const mounted = useRef(true),
    controller = useRef<AbortController | null>(null),
    submitting = useRef(false),
    generation = useRef(0);
  const api = useRef(createDev2Api());
  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
      generation.current++;
      submitting.current = false;
      controller.current?.abort();
    };
  }, []);
  async function run(target: Operation, input = values, args = parameters) {
    if (submitting.current) return;
    try {
      payload(target.fields, input, actor);
      operationPath(target, args);
    } catch (e) {
      setMessage(e instanceof Error ? e.message : "Check the form.");
      return;
    }
    submitting.current = true;
    const runId = ++generation.current;
    setBusy(true);
    setMessage("");
    setData(undefined);
    const abort = new AbortController();
    controller.current = abort;
    try {
      const response = await api.current(
        target,
        args,
        input,
        actor,
        abort.signal,
      );
      if (!mounted.current || abort.signal.aborted) return;
      if (!response.ok) {
        const error = response.error;
        if (error.kind === "http") {
          if (
            error.status === 401 ||
            error.problem?.code === "PASSWORD_CHANGE_REQUIRED"
          ) {
            setSessionLost(true);
            setRecord({});
            setValues({});
            expired();
            return;
          }
          setDenied(error.status === 403);
          if (error.status === 403) {
            setRecord({});
            setValues({});
          }
          setMessage(
            error.status === 403
              ? "Access denied for this action and resource. No permission was inferred from your role."
              : error.status === 404
                ? "Record not found or outside your permitted scope."
                : error.status === 400 || error.status === 409
                  ? (error.problem?.detail ??
                    error.problem?.title ??
                    "Validation or version conflict. Reload the record before retrying.")
                  : "The request failed. Confirm the record before attempting another change.",
          );
        } else if (error.kind === "stale") {
          setSessionLost(true);
          setRecord({});
          setValues({});
          expired();
        } else {
          if (target.method !== "GET") setUncertain(true);
          setMessage(
            error.kind === "network"
              ? "Connection interrupted. The operation outcome is unknown. Inspect the record before retrying; no automatic retry was made."
              : "The response could not be confirmed. Check your session and reload the record.",
          );
        }
        return;
      }
      setDenied(false);
      setData(response.data);
      const root = isObject(response.data) ? response.data : {};
      setRecord(root);
      if (target.method !== "GET") {
        setMessage(
          "Saved by the server. Related views will load current data when opened.",
        );
        // Read the current record after mutation so subsequent commands use its actual RowVersion.
        setValues(defaults(operation.fields, root));
        const detail = operations.find(
          (x) => x.id === readAction(operation, config.detail),
        );
        const rootKey = (
          {
            assets: "assetId",
            incidents: "incidentId",
            maintenance: "maintenanceWorkId",
            "material-requests": "requestId",
            replenishments: "replenishmentId",
            inventory: "documentId",
          } as Record<string, string>
        )[module];
        const inventoryRoot =
          detail?.id === "inventory:Item"
            ? root.itemId
            : detail?.id === "inventory:Unit"
              ? root.uomId
              : detail?.id === "inventory:Warehouse"
                ? root.warehouseId
                : detail?.id === "inventory:Stocktake"
                  ? root.stocktakeId
                  : undefined;
        const rootId = inventoryRoot ?? root[rootKey] ?? args.id;
        if (detail && rootId) {
          const refreshed = await api.current(
            detail,
            { ...args, id: rootId },
            defaults(detail.fields),
            actor,
            abort.signal,
          );
          if (!mounted.current || abort.signal.aborted) return;
          if (refreshed.ok && isObject(refreshed.data)) {
            setRecord(refreshed.data);
            setData(refreshed.data);
            setValues(defaults(operation.fields, refreshed.data));
          } else if (
            !refreshed.ok &&
            refreshed.error.kind === "http" &&
            refreshed.error.status === 401
          ) {
            setSessionLost(true);
            expired();
            return;
          } else
            setMessage(
              "Saved. The follow-up read is unavailable; reload before the next versioned change.",
            );
        }
      }
    } finally {
      if (generation.current === runId) {
        submitting.current = false;
        if (mounted.current) setBusy(false);
      }
    }
  }
  useEffect(() => {
    if (
      operation.method === "GET" &&
      operation.route
        .match(/\{\w+:long\}/g)
        ?.every((p) => Boolean(parameters[p.slice(1).split(":")[0]])) !== false
    )
      void run(operation);
    else if (
      recordId &&
      operation.method !== "GET" &&
      operation.id !== "A004A-024"
    ) {
      const detail = operations.find(
        (x) => x.id === readAction(operation, config.detail),
      )!;
      const abort = new AbortController();
      controller.current = abort;
      setBusy(true);
      void api
        .current(
          detail,
          { ...parameters, id: recordId },
          defaults(detail.fields),
          actor,
          abort.signal,
        )
        .then((r) => {
          if (!mounted.current || abort.signal.aborted) return;
          if (r.ok && isObject(r.data)) {
            setRecord(r.data);
            setData(r.data);
            setValues(defaults(operation.fields, { ...context, ...r.data }));
            setUncertain(false);
          } else if (
            !r.ok &&
            r.error.kind === "http" &&
            r.error.status === 401
          ) {
            setSessionLost(true);
            expired();
          }
          setBusy(false);
        });
    }
    // A route/operation selection remounts this screen. Revalidation never retries writes.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [revision]);
  if (sessionLost)
    return (
      <p role="alert">
        Session changed. Business data has been cleared. Confirm your session
        before continuing.
      </p>
    );
  const routeFields = [...operation.route.matchAll(/\{(\w+):long\}/g)].map(
    (m) => ({ name: m[1], type: "long", optional: false }),
  );
  const branch = values.branchId ?? record.branchId ?? context.branchId;
  const change = (v: Values) => {
    setValues(v);
    setDenied(false);
  };
  return (
    <main className="dev2-workspace">
      <header>
        <h1>{config.name}</h1>
        <Link to="/profile">Profile</Link>
        <Link to="/dashboard">Workspace home</Link>
      </header>
      <nav aria-label="Operations">
        {modules.filter(m => !enabledModules || enabledModules.includes(m.id)).map((m) => (
          <Link
            key={m.id}
            aria-current={m.id === module ? "page" : undefined}
            to={`/operations/${m.id}${branch && /^[1-9]\d*$/.test(String(branch)) ? "?branchId=" + branch : ""}`}
          >
            {m.name}
          </Link>
        ))}
      </nav>
      {!readOnly && <p>
        Incident → Maintenance Work → Material Request → Fulfillment → Material
        Availability
      </p>}
      <div className="dev2-toolbar">
        <Link to={`/operations/${module}`}>Browse</Link>
        {!readOnly && <Link
          to={`/operations/${module}${recordId ? "/" + recordId : ""}?action=${config.create}`}
        >
          Create
        </Link>}
        <label>
          Action
          <select
            aria-label="Action"
            value={operation.id}
            disabled={busy}
            onChange={(e) => select(e.target.value)}
          >
            {["GET", "WRITE"].map((group) => (
              <optgroup
                key={group}
                label={
                  group === "GET" ? "Browse and history" : "Business actions"
                }
              >
                {available
                  .filter((o) => (o.method === "GET") === (group === "GET"))
                  .map((o) => (
                    <option key={o.id} value={o.id}>
                      {o.name}
                    </option>
                  ))}
              </optgroup>
            ))}
          </select>
        </label>
        <button
          disabled={busy}
          onClick={() => {
            setDenied(false);
            setRevision((v) => v + 1);
          }}
        >
          Reload record
        </button>
      </div>
      <p className="dev2-permission">
        Required access:{" "}
        {operation.permissions.join(" + ") || operation.permissionHint}. The
        server checks your branch, resource, ownership and workflow for every
        request.
      </p>
      <form
        data-operation={operation.id}
        onSubmit={(e) => {
          e.preventDefault();
          void run(operation);
        }}
      >
        <fieldset disabled={busy}>
          <legend>{operation.name}</legend>
          <Fields
            fields={routeFields}
            values={parameters}
            actor={actor}
            onChange={(v) => {
              setParameters(v);
              setDenied(false);
            }}
          />
          <Fields
            fields={operation.id === "A004A-043" ? operation.fields.filter(f => ["branchId", "purpose", "neededAt", "submit"].includes(f.name))
              .map(f => f.name === "neededAt" && values.submit === true ? { ...f, optional: false } : f)
              : ["A004A-021", "A004A-022"].includes(operation.id) ? operation.fields.filter(f => f.name !== "categoryId") : operation.fields}
            values={values}
            actor={actor}
            onChange={v => change(operation.id === "A004A-043" && v.branchId !== values.branchId
              ? { ...v, assignedApproverUserId: "", departmentId: "", sourceWarehouseId: "", receivingRoomId: "", receivingAreaId: "", incidentId: "", priorityId: "", lines: [] }
              : ["A004A-021", "A004A-022"].includes(operation.id) && v.branchId !== values.branchId ? { ...v, categoryId: "" } : v)}
          />
          {["A004A-021", "A004A-022"].includes(operation.id) && <IncidentCategoryField branch={values.branchId}
            value={values.categoryId} onChange={categoryId => change({ ...values, categoryId })} />}
          {operation.id === "A004A-043" && <MaterialCatalogFields values={values} onChange={change} />}
          {operation.id === "A004A-043" && <MaterialApproverField branch={values.branchId} warehouse={values.sourceWarehouseId}
            value={values.assignedApproverUserId} required={values.submit === true}
            onChange={assignedApproverUserId => change({ ...values, assignedApproverUserId })} />}
          {operation.id === "A004A-051" && (
            <p>
              Mode: Partial with explicit quantities, or FullRemaining without
              lines. Requester/approver cannot fulfill their own request.
            </p>
          )}
          <button type="submit" disabled={busy || denied || uncertain || operation.id === "A004A-043" && (!Array.isArray(values.lines) || values.lines.length === 0)}>
            {busy
              ? "Working…"
              : operation.method === "GET"
                ? "Load records"
                : "Save " + operation.name.toLowerCase()}
          </button>
        </fieldset>
      </form>
      {busy && <p role="status">Loading current server data…</p>}
      {message && (
        <p role={denied || !message.startsWith("Saved") ? "alert" : "status"}>
          {message}
        </p>
      )}
      {data !== undefined && <DataView data={data} title={config.name} />}
      {operation.method === "GET" &&
        isObject(data) &&
        typeof data.page === "number" &&
        typeof data.pageSize === "number" && (
          <div className="dev2-toolbar" aria-label="Pagination">
            <button
              disabled={busy || data.page <= 1}
              onClick={() => {
                const next = { ...values, page: Number(data.page) - 1 };
                setValues(next);
                void run(operation, next);
              }}
            >
              Previous page
            </button>
            <span>Page {data.page}</span>
            <button
              disabled={
                busy ||
                typeof (data.total ?? data.totalCount) !== "number" ||
                data.page * data.pageSize >=
                  Number(data.total ?? data.totalCount)
              }
              onClick={() => {
                const next = { ...values, page: Number(data.page) + 1 };
                setValues(next);
                void run(operation, next);
              }}
            >
              Next page
            </button>
          </div>
        )}
      {!readOnly && record.incidentId != null && (
        <p>
          <Link
            to={`/operations/maintenance?action=A004A-035&incidentId=${record.incidentId}`}
          >
            Browse related maintenance works
          </Link>{" "}
          ·{" "}
          <Link
            to={`/operations/maintenance/${record.incidentId}?action=A004A-024`}
          >
            Create work for incident {String(record.incidentId)}
          </Link>{" "}
          ·{" "}
          <Link
            to={`/operations/material-requests?action=A004A-043&incidentId=${record.incidentId}${record.branchId ? "&branchId=" + record.branchId : ""}${record.departmentId ? "&departmentId=" + record.departmentId : ""}`}
          >
            Request materials for this incident
          </Link>
        </p>
      )}
      {!readOnly && available.some(op => op.id === "A004A-051") && module === "material-requests" && record.requestId != null && (
        <p>
          <Link
            to={`/operations/material-requests/${record.requestId}?action=A004A-051`}
          >
            Fulfill approved lines
          </Link>
        </p>
      )}
      {module === "maintenance" && record.maintenanceWorkId != null && (
        <p>
          <Link
            to={`/operations/maintenance/${record.maintenanceWorkId}?action=A004A-034`}
          >
            Issued material availability
          </Link>
        </p>
      )}
      {!readOnly && !enabledActions && <footer>
        <Link to="/inventory/items">Inventory item catalogue</Link> ·{" "}
        <Link to="/operations/inventory?action=inventory:Movements">
          Stock movements
        </Link>{" "}
        ·{" "}
        <Link to="/operations/inventory?action=inventory:Documents">
          Stock documents
        </Link>
      </footer>}
    </main>
  );
}
