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
import { IncidentEvidencePanel } from "./IncidentEvidencePanel";
import { IncidentDuplicateReview, incidentLocationKey, type IncidentReview } from "./IncidentDuplicateReview";
import { MaterialApproverField } from "./MaterialApproverField";
import { MaterialCatalogFields } from "./MaterialCatalogFields";
import { MaterialBranchField } from "./MaterialBranchField";
import { MaterialApprovalFields, materialApprovalActions, materialApprovalReady } from "./MaterialApprovalFields";
import { MaterialCancellationFields, materialRequesterActions, materialRequesterReady } from "./MaterialRequesterFields";
import { MaterialIssueFields, materialIssueReady } from "./MaterialIssueFields";
import { MaterialAvailabilityView } from "./MaterialAvailabilityView";
import { MaterialHistoryView } from "./MaterialHistoryView";
import { ReplenishmentCloseFields, replenishmentCanClose } from "./ReplenishmentCloseFields";
import { ReplenishmentReceiptFields } from "./ReplenishmentReceiptFields";
import { ReplenishmentListView } from "./ReplenishmentListView";
import { ReplenishmentDetailView } from "./ReplenishmentDetailView";
import { MaterialListView } from "./MaterialListView";
import { MaterialReviewHistory } from "./MaterialReviewHistory";
import { MaintenanceReadView } from "./MaintenanceReadView";
import { MaterialTaskLinks } from "./MaterialTaskLinks";
import { ReplenishmentWaitingRequests } from "./ReplenishmentWaitingRequests";
import { ReplenishmentPostFields, replenishmentPostReady } from "./ReplenishmentPostFields";
import { ReplenishmentCreateFields, replenishmentCreateReady } from "./ReplenishmentCreateFields";

const identities = new WeakMap<object, number>();
let identity = 0;
function key(session: object) {
  if (!identities.has(session)) identities.set(session, ++identity);
  return identities.get(session);
}
function operationDefaults(operation: Operation, source: Values = {}): Values {
  if (operation.id === "A004A-056") return { ...defaults(operation.fields, source), branchId: source.branchId,
    sourceRequestId: source.sourceRequestId, shortageSource: source.shortageSource };
  if (operation.id === "A004A-051") return defaults(operation.fields, { ...source, mode: "FullRemaining", lines: [] });
  if (operation.id === "A004A-044" && Array.isArray(source.lines))
    return defaults(operation.fields, { ...source, lines: source.lines.filter(isObject).filter(l => l.isActive === true) });
  if (operation.id === "A004A-047" && Array.isArray(source.lines))
    return defaults(operation.fields, { ...source, lines: source.lines.filter(isObject).filter(l => l.isActive === true)
      .map(l => ({ requestLineId: l.requestLineId, approvedQty: l.requestedQty })) });
  return defaults(operation.fields, source);
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
    "requesterId",
    "internalRequestId",
    "sourceVersion",
    "sourceRequestId",
    "shortageSource",
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
  // Hold/Resume use versioned detail, never the unversioned generic form.
  const available = operations.filter((x) => !["A004A-026", "A004A-027", "A004A-028"].includes(x.id) && x.module === module && (!readOnly || x.method === "GET")
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
    operationDefaults(operation, context),
  );
  const [record, setRecord] = useState<Values>({});
  const [reviewedIncidentLocation, setReviewedIncidentLocation] = useState<IncidentReview | null>(null);
  const [incidentFiles, setIncidentFiles] = useState<File[]>([]);
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
    let commandInput = input;
    try {
      if (target.id === "A004A-059") {
        if (!replenishmentPostReady(record, input) || String(args.id) !== String(record.replenishmentId))
          throw Error("Load an eligible Draft receipt, verify every line and confirm posting.");
        commandInput = { ...input, expectedRowVersion: record.rowVersion };
      }
      if (target.id === "A004A-056" && !replenishmentCreateReady(input))
        throw Error("Select authorized shortages and enter positive base quantities before creating a replenishment.");
      if (target.id === "A004A-058") {
        if (!replenishmentCanClose(record) || String(args.id) !== String(record.replenishmentId)
          || input.receiptSelectionVersion !== record.rowVersion || !Array.isArray(input.lines) || !input.lines.length)
          throw Error("Load the current open replenishment and select eligible posted receipts.");
        commandInput = { ...input, expectedRowVersion: record.rowVersion };
      }
      if (target.id === "A004A-057") {
        if (!replenishmentCanClose(record) || String(args.id) !== String(record.replenishmentId))
          throw Error("Load the current open replenishment before closing its remainder.");
        if (input.confirmRemainderClosure !== true || String(input.reason ?? "").trim().length < 10)
          throw Error("Confirm remainder closure and enter a reason of at least 10 non-padding characters.");
        commandInput = { ...input, reason: String(input.reason).trim(), expectedRowVersion: record.rowVersion };
      }
      if (target.id === "A004A-051") {
        if (!materialIssueReady(record, actor) || String(args.id) !== String(record.requestId))
          throw Error("Load the current approved request before issuing materials.");
        const enteredLines = Array.isArray(input.lines) ? input.lines.filter(isObject) : [];
        if (input.mode === "Partial" && enteredLines.some(l => !Number.isFinite(Number(l.quantity)) || Number(l.quantity) < 0))
          throw Error("Issue quantities must be valid nonnegative numbers.");
        const issueLines = enteredLines.filter(l => Number(l.quantity) > 0);
        if (input.mode === "Partial" && issueLines.length === 0) throw Error("Enter at least one positive issue quantity.");
        commandInput = { ...input, lines: input.mode === "FullRemaining" ? [] : issueLines };
      }
      if (["A004A-021", "A004A-043"].includes(target.id) && (context.internalRequestId != null || context.sourceVersion != null)) {
        if (!Number.isSafeInteger(Number(context.internalRequestId)) || Number(context.internalRequestId) <= 0
          || !Number.isSafeInteger(Number(context.branchId)) || Number(context.branchId) <= 0
          || !["required", "optional"].includes(String(input.materialRequirement))
          || typeof context.sourceVersion !== "string" || !/^[A-Za-z0-9+/]{11}=$/.test(context.sourceVersion))
          throw Error("Reload the Internal Request and choose whether this work is required for completion.");
        commandInput = { ...input, branchId: context.branchId, internalRequestId: context.internalRequestId,
          requestLink: { version: context.sourceVersion, mandatory: input.materialRequirement === "required" } };
      }
      if (target.id === "A004A-021") {
        if (!reviewedIncidentLocation || reviewedIncidentLocation.key !== incidentLocationKey(commandInput))
          throw Error("Review similar incidents for the current location before creating a new incident.");
        commandInput = { ...commandInput, similarReviewToken: reviewedIncidentLocation.token };
      }
      payload(target.fields, commandInput, actor);
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
      if (target.id === "A004A-021") {
        if (incidentFiles.length > 5 || incidentFiles.some(f => f.size < 1 || f.size > 10 * 1024 * 1024
          || !["image/jpeg", "image/png", "application/pdf"].includes(f.type)))
          throw Error("MSG23: Select up to five JPG, PNG or PDF files, at most 10 MiB each.");
        const files = await Promise.all(incidentFiles.map(async file => {
          const bytes = new Uint8Array(await file.arrayBuffer());
          let binary = "";
          for (let offset = 0; offset < bytes.length; offset += 8192) binary += String.fromCharCode(...bytes.subarray(offset, offset + 8192));
          return { name: file.name, mimeType: file.type, base64: btoa(binary) };
        }));
        if (!mounted.current || abort.signal.aborted) return;
        commandInput = { ...commandInput, files };
      }
      const response = await api.current(
        target,
        args,
        commandInput,
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
        if (target.id === "A004A-021") setIncidentFiles([]);
        setMessage(
          "Saved by the server. Related views will load current data when opened.",
        );
        // Read the current record after mutation so subsequent commands use its actual RowVersion.
        setValues(operationDefaults(operation, root));
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
            setValues(operationDefaults(operation, refreshed.data));
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
            setValues(operationDefaults(operation, { ...context, ...r.data }));
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
  const materialWrite = ["A004A-043", "A004A-044"].includes(operation.id);
  const materialSubmit = operation.id === "A004A-045";
  const needsApproval = materialSubmit || values.submit === true || operation.id === "A004A-044"
    && ["PENDING_APPROVAL", "APPROVED"].includes(String(record.rawStatus));
  const historicalLineMax = Array.isArray(record.lines)
    ? Math.max(0, ...record.lines.filter(isObject).map(l => Number(l.lineNo))) : 0;
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
      {context.internalRequestId != null && actionId === config.create && !available.some(op => op.id === actionId) && <p role="status">
        Creating this linked work is unavailable with the current access. No linked work has been created.{" "}
        <Link to={`/internal-requests?requestId=${encodeURIComponent(String(context.internalRequestId))}`}>Return to source</Link>
      </p>}
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
        {!readOnly && available.some(op => op.id === config.create) && <Link
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
        {materialApprovalActions.includes(operation.id) ? "Current assigned approver with mapped approval authority and MR_VIEW"
          : materialRequesterActions.includes(operation.id) ? "Current requester with MR_CREATE and independent MR_VIEW"
          : operation.permissions.join(" + ") || operation.permissionHint}. The
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
            fields={materialRequesterActions.includes(operation.id) || ["A004A-051", "A004A-057", "A004A-058", "A004A-059"].includes(operation.id) ? [] : routeFields}
            values={parameters}
            actor={actor}
            onChange={(v) => {
              setParameters(v);
              setDenied(false);
            }}
          />
          {["A004A-021", "A004A-043"].includes(operation.id) && context.internalRequestId != null && <section aria-label="Source Internal Request">
            <p>This {operation.id === "A004A-021" ? "incident" : "material request"} will be linked to the source Internal Request when saved.</p>
            <Link to={`/internal-requests?requestId=${encodeURIComponent(String(context.internalRequestId))}`}>Return to source and reload</Link>
            <label>Completion requirement<select required value={String(values.materialRequirement ?? "")}
              onChange={event => change({ ...values, materialRequirement: event.target.value })}>
              <option value="">Select requirement</option><option value="required">Required for completion</option><option value="optional">Optional</option>
            </select></label>
          </section>}
          {operation.id === "A004A-043" && <MaterialBranchField value={values.branchId} fixed={context.internalRequestId != null}
            onChange={branchId => change({ ...values, branchId, assignedApproverUserId: "", departmentId: "", sourceWarehouseId: "",
              receivingRoomId: "", receivingAreaId: "", incidentId: "", priorityId: "", lines: [] })} />}
          <Fields
            fields={materialApprovalActions.includes(operation.id) || materialSubmit || ["A004A-049", "A004A-056", "A004A-057", "A004A-058", "A004A-059"].includes(operation.id) ? []
              : operation.id === "A004A-051" ? operation.fields.filter(f => ["mode", "documentDate"].includes(f.name))
              : materialWrite ? operation.fields.filter(f => ["purpose", "neededAt", "submit"].includes(f.name))
              .map(f => f.name === "neededAt" && needsApproval ? { ...f, optional: false } : f)
              : ["A004A-021", "A004A-022"].includes(operation.id) ? operation.fields.filter(f => f.name !== "categoryId"
                && !(operation.id === "A004A-021" && context.internalRequestId != null && f.name === "branchId")) : operation.fields}
            values={values}
            actor={actor}
            onChange={v => change(operation.id === "A004A-043" && v.branchId !== values.branchId
              ? { ...v, assignedApproverUserId: "", departmentId: "", sourceWarehouseId: "", receivingRoomId: "", receivingAreaId: "", incidentId: "", priorityId: "", lines: [] }
              : ["A004A-021", "A004A-022"].includes(operation.id) && v.branchId !== values.branchId ? { ...v, categoryId: "" } : v)}
          />
          {["A004A-021", "A004A-022"].includes(operation.id) && <IncidentCategoryField branch={values.branchId}
            value={values.categoryId} onChange={categoryId => change({ ...values, categoryId })} />}
          {operation.id === "A004A-021" && <IncidentDuplicateReview values={values} onReviewed={setReviewedIncidentLocation} />}
          {operation.id === "A004A-021" && <label>Evidence (optional, up to five JPG, PNG or PDF files, 10 MiB each)
            <input key={String(record.incidentId ?? "new")} type="file" multiple accept=".jpg,.jpeg,.png,.pdf" disabled={busy}
              onChange={e => setIncidentFiles(Array.from(e.target.files ?? []))} /></label>}
          {materialWrite && <MaterialCatalogFields values={values} onChange={change}
            historicalLineMax={operation.id === "A004A-044" ? historicalLineMax : undefined} />}
          {materialRequesterActions.includes(operation.id) && !materialRequesterReady(record, actor, operation.id)
            && <p role="status">Open your current request in an eligible state before saving. Changes are unavailable after the first issue.</p>}
          {operation.id === "A004A-049" && <MaterialCancellationFields values={values} onChange={change} />}
          {materialApprovalActions.includes(operation.id) && <>
            {!materialApprovalReady(record, actor, operation.id) && <p role="status">Load a request currently awaiting your decision before saving.</p>}
            <MaterialApprovalFields record={record} values={values} action={operation.id} onChange={change} />
          </>}
          {(materialWrite || materialSubmit) && <MaterialApproverField branch={materialSubmit ? record.branchId : values.branchId}
            warehouse={materialSubmit ? record.sourceWarehouseId : values.sourceWarehouseId}
            value={values.assignedApproverUserId} required={needsApproval}
            onChange={assignedApproverUserId => change({ ...values, assignedApproverUserId })} />}
          {operation.id === "A004A-051" && (
            <><p>Current Storekeeper access, MR_FULFILL, STOCK_ISSUE_POST, INVENTORY_VIEW and MR_VIEW are required. Requester and approver cannot issue this request.</p>
              {!materialIssueReady(record, actor) && <p role="status">Open an eligible approved request before issuing materials.</p>}
              <MaterialIssueFields record={record} values={values} onChange={change} /></>
          )}
          {operation.id === "A004A-057" && <ReplenishmentCloseFields record={record} values={values} onChange={change} />}
          {operation.id === "A004A-056" && <ReplenishmentCreateFields values={values} onChange={change} />}
          {operation.id === "A004A-058" && <ReplenishmentReceiptFields record={record} values={values} onChange={change} />}
          {operation.id === "A004A-059" && <ReplenishmentPostFields record={record} values={values} onChange={change} />}
          <button type="submit" disabled={busy || denied || uncertain
            || operation.id === "A004A-059" && (!replenishmentPostReady(record, values) || String(parameters.id) !== String(record.replenishmentId))
            || operation.id === "A004A-056" && !replenishmentCreateReady(values)
            || operation.id === "A004A-058" && (!replenishmentCanClose(record) || !Array.isArray(values.lines)
              || !values.lines.length || values.receiptSelectionVersion !== record.rowVersion)
            || operation.id === "A004A-057" && (!replenishmentCanClose(record) || values.confirmRemainderClosure !== true
              || String(parameters.id) !== String(record.replenishmentId))
            || operation.id === "A004A-051" && !materialIssueReady(record, actor)
            || materialApprovalActions.includes(operation.id) && !materialApprovalReady(record, actor, operation.id)
            || materialRequesterActions.includes(operation.id) && (!materialRequesterReady(record, actor, operation.id)
              || String(parameters.id) !== String(record.requestId))
            || materialWrite && (!Array.isArray(values.lines) || values.lines.length === 0)}>
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
      {data !== undefined && (["A004A-035", "A004A-036"].includes(operation.id) && isObject(data)
        ? <MaintenanceReadView data={data} detail={operation.id === "A004A-036"} canDetail={available.some(op => op.id === "A004A-036")} canStart={!readOnly && (!enabledActions || enabledActions.includes("A004A-026"))} canResume={!readOnly && (!enabledActions || enabledActions.includes("A004A-028"))} canHold={!readOnly && (!enabledActions || enabledActions.includes("A004A-027"))} />
        : operation.id === "A004A-040" && isObject(data)
        ? <MaterialAvailabilityView data={data} /> : operation.id === "A004A-052" && isObject(data)
        ? <ReplenishmentListView data={data} /> : operation.id === "A004A-053" && isObject(data)
        ? <ReplenishmentDetailView record={data} /> : operation.id === "A004A-037" && isObject(data)
        ? <MaterialListView data={data} /> : <DataView data={data} title={config.name} />)}
      {module === "material-requests" && record.requestId != null && Array.isArray(record.cycles)
        && <MaterialHistoryView record={record} />}
      {operation.id === "A004A-023" && data !== undefined && !denied && !sessionLost && typeof record.incidentId === "number" && record.incidentId > 0
        && <IncidentEvidencePanel key={record.incidentId} incidentId={record.incidentId} />}
      {module === "material-requests" && operation.id === "A004A-038" && record.requestId != null
        && <><MaterialTaskLinks record={record} /><MaterialTaskLinks record={record} kind="cleaning" /></>}
      {module === "material-requests" && materialApprovalReady(record, actor, "A004A-047")
        && <MaterialReviewHistory record={record} />}
      {module === "material-requests" && record.rawStatus === "PARTIALLY_ISSUED" && record.branchId != null
        && record.sourceWarehouseId != null && (!enabledModules || enabledModules.includes("replenishments"))
        && (!enabledActions || enabledActions.includes("A004A-056"))
        && <Link to={`/operations/replenishments?action=A004A-056&branchId=${encodeURIComponent(String(record.branchId))}&warehouseId=${encodeURIComponent(String(record.sourceWarehouseId))}&shortageSource=material-requests&sourceRequestId=${encodeURIComponent(String(record.requestId))}`}>Check shortage and create replenishment for the remainder</Link>}
      {module === "replenishments" && operation.id === "A004A-053" && record.replenishmentId != null
        && <ReplenishmentWaitingRequests record={record} />}
      {module === "replenishments" && record.replenishmentId != null && available.some(op => op.id === "A004A-057")
        && replenishmentCanClose(record) && <Link to={`/operations/replenishments/${encodeURIComponent(String(record.replenishmentId))}?action=A004A-057`}>Close unreceived remainder</Link>}
      {module === "replenishments" && record.replenishmentId != null && available.some(op => op.id === "A004A-058")
        && replenishmentCanClose(record) && <Link to={`/operations/replenishments/${encodeURIComponent(String(record.replenishmentId))}?action=A004A-058`}>Link posted receipts</Link>}
      {module === "replenishments" && record.replenishmentId != null && available.some(op => op.id === "A004A-059")
        && replenishmentCanClose(record) && <Link to={`/operations/replenishments/${encodeURIComponent(String(record.replenishmentId))}?action=A004A-059`}>Verify and post Draft receipt</Link>}
      {["A004A-040", "A004A-041", "A004A-042"].includes(operation.id) && recordId && <Link to={`/operations/material-requests/${encodeURIComponent(recordId)}`}>Return to material request</Link>}
      {module === "material-requests" && record.requestId != null && Array.isArray(record.fulfillments) && (
        <section aria-label="Material issue receipts"><p>Issue receipts</p>
          <ul>{record.fulfillments.filter(isObject).map(f => <li key={String(f.fulfillmentId)}>
            <Link to={`/operations/material-requests/${encodeURIComponent(String(record.requestId))}?action=A004A-042&fulfillmentId=${encodeURIComponent(String(f.fulfillmentId))}`}>
              {String(f.fulfillmentNo)}
            </Link>
          </li>)}</ul>
        </section>
      )}
      {module === "material-requests" && record.requestId != null && record.requesterId != null && Array.isArray(record.lines) && (
        <section aria-label="Material request review context">
          {(record.rawStatus === "APPROVED" || record.rawStatus === "PARTIALLY_ISSUED"
            || materialApprovalReady(record, actor, "A004A-047")) &&
            <Link to={`/operations/material-requests/${record.requestId}?action=A004A-040`}>View available stock</Link>}
          <p>Stock is a read-only preview in base units; reviewing a request does not reserve materials.</p>
          <p>Recent requests by this requester, subject to your current read scope:</p>
          <ul>{record.lines.filter(isObject).filter((line, index, lines) => line.isActive === true
            && lines.findIndex(other => other.isActive === true && other.itemId === line.itemId) === index).map(line => (
            <li key={String(line.itemId)}><Link to={`/operations/material-requests?branchId=${encodeURIComponent(String(record.branchId))}&requesterId=${encodeURIComponent(String(record.requesterId))}&itemId=${encodeURIComponent(String(line.itemId))}`}>
              {String(line.itemName ?? `Material ${line.itemId}`)}
            </Link></li>
          ))}</ul>
        </section>
      )}
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
            to={`/operations/maintenance?action=A004A-035&incidentId=${record.incidentId}${record.branchId ? "&branchId=" + record.branchId : ""}`}
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
