import { StructureDeactivationPanel } from "../structure-deactivation/StructureDeactivationPanel";
import { DepartmentDeactivationPanel } from "./DepartmentDeactivationPanel";
import { RoomTypeDirectoryResults } from "./RoomTypeDirectoryResults";
import { RoomDirectoryResults } from "./RoomDirectoryResults";
import { useRef, useState } from "react";
import { Link, Navigate, useLocation, useParams } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import { decimalId, object } from "./contracts";
import type {
  Context,
  Filters,
  MutationKind,
  MutationRequests,
  ReadTypes,
} from "./contracts";
import {
  draftRequest,
  fields,
  mutationFor,
  readFor,
  titles,
} from "./operations";
import type { Action, Screen } from "./operations";
import { unknownHotelOutcome, useHotelFlow } from "./useHotelFlow";
import type { HotelSnapshot } from "./useHotelFlow";

function Values({ value }: { value: unknown }) {
  if (value === null) return <span>—</span>;
  if (Array.isArray(value))
    return value.length ? (
      <ol>
        {value.map((item, i) => (
          <li key={i}>
            <Values value={item} />
          </li>
        ))}
      </ol>
    ) : (
      <p>No records on this page.</p>
    );
  if (object(value))
    return (
      <dl>
        {Object.entries(value)
          .filter(([key]) => !key.endsWith("Url"))
          .map(([key, item]) => (
            <div key={key}>
              <dt>{key.replace(/([A-Z])/g, " $1")}</dt>
              <dd>
                <Values value={item} />
              </dd>
            </div>
          ))}
      </dl>
    );
  return <span>{String(value)}</span>;
}
function MutationForm({
  kind,
  context,
  snapshot,
  save,
}: {
  kind: MutationKind;
  context: Context;
  snapshot: HotelSnapshot | null;
  save(
    kind: MutationKind,
    c: Context,
    request: MutationRequests[MutationKind],
    snapshot: HotelSnapshot | null,
  ): Promise<void>;
}) {
  const raw: unknown = snapshot?.data;
  const source: Record<string, unknown> = object(raw)
    ? object(raw.details)
      ? raw.details
      : raw
    : {};
  const [draft, setDraft] = useState<Record<string, string>>(() =>
    Object.fromEntries(
      fields[kind].map((f) => [
        f.key,
        (kind === "updateType" && f.key === "code") || source[f.key] == null
          ? ""
          : String(source[f.key]),
      ]),
    ),
  );
  const [review, setReview] = useState<MutationRequests[MutationKind] | null>(
    null,
  );
  const reviewed = useRef<typeof review>(null);
  const [error, setError] = useState("");
  function preview() {
    reviewed.current = null;
    setReview(null);
    const request = draftRequest(kind, draft, context);
    if (!request) {
      setError(
        "Check required fields, lengths and numeric IDs. IDs accept positive safe decimal integers only.",
      );
      return;
    }
    setError("");
    reviewed.current = request;
    setReview(request);
  }
  function confirm() {
    const request = reviewed.current;
    if (!request) return;
    reviewed.current = null;
    setReview(null);
    void save(kind, context, request, snapshot);
  }
  return (
    <form
      aria-label="Hotel structure change"
      noValidate
      onSubmit={(e) => e.preventDefault()}
    >
      {fields[kind].map((f) => (
        <label key={f.key}>
          {f.label}
          {f.type === "longtext" ? (
            <textarea
              name={f.key}
              required={f.required}
              maxLength={f.max}
              value={draft[f.key]}
              onChange={(e) => {
                setDraft({ ...draft, [f.key]: e.target.value });
                reviewed.current = null;
                setReview(null);
                setError("");
              }}
            />
          ) : (
            <input
              name={f.key}
              type="text"
              inputMode={
                f.type === "id" || f.type === "integer" ? "numeric" : "text"
              }
              autoComplete="off"
              required={f.required}
              maxLength={f.max}
              value={draft[f.key]}
              onChange={(e) => {
                setDraft({ ...draft, [f.key]: e.target.value });
                reviewed.current = null;
                setReview(null);
                setError("");
              }}
            />
          )}
        </label>
      ))}
      {kind.includes("Type") && (
        <p>
          Use the ID of an eligible published checklist supplied by your
          administrator. This screen does not create or modify checklists.
        </p>
      )}
      {kind === "deactivateType" && (
        <p>
          MSG33: Deactivation requires explicit confirmation. The server checks
          active-room dependencies.
        </p>
      )}
      <button type="button" onClick={preview}>
        Review change
      </button>
      {review && (
        <section aria-label="Review hotel structure change">
          <p>Branch ID: {context.branchId}</p>
          {context.id && <p>Record ID: {context.id}</p>}
          <Values value={review} />
          <p>
            The server checks permissions and dependencies again. Loading a
            record does not grant permission to save it.
          </p>
          <button type="button" onClick={confirm}>
            Confirm change
          </button>
        </section>
      )}
      {error && <p role="alert">{error}</p>}
    </form>
  );
}
export function HotelWorkspace({ screen = "structure", readOnly = false, directoryBasePath = "/rooms", canonicalStructure = false }: {
  screen?: Screen; readOnly?: boolean; directoryBasePath?: string; canonicalStructure?: boolean;
}) {
  const { state, actions } = useAuth();
  const route = useLocation(),
    params = useParams();
  const search = new URLSearchParams(route.search);
  const [branch, setBranch] = useState(
    params.branchId ?? search.get("branchId") ?? "",
  );
  const [record, setRecord] = useState(params.id ?? "");
  const [action, setAction] = useState<Action>(
    canonicalStructure && search.get("action") === "create" ? "create" : params.id
      ? screen === "rooms" && search.get("view") === "history"
        ? "history"
        : "detail"
      : "browse",
  );
  const [query, setQuery] = useState("");
  const [active, setActive] = useState("");
  const [page, setPage] = useState("1");
  const [sort, setSort] = useState(screen === "rooms" ? "floor" : "code");
  const [descending, setDescending] = useState(false);
  const [allBranches, setAllBranches] = useState(false);
  const [collection, setCollection] = useState("floors");
  const [nodeType, setNodeType] = useState("branch"),
    [nodeId, setNodeId] = useState("");
  const [floorFilter, setFloorFilter] = useState(""),
    [areaFilter, setAreaFilter] = useState(""),
    [typeFilter, setTypeFilter] = useState(""),
    [statusFilter, setStatusFilter] = useState("");
  const branchId = decimalId(branch),
    id = decimalId(record),
    pageNumber = decimalId(page);
  const kind = mutationFor(screen, action);
  const readKind = readFor(screen, action);
  const key = JSON.stringify([
    route.pathname,
    route.search,
    screen,
    action,
    branch,
    record,
    query,
    active,
    page,
    sort,
    descending,
    allBranches,
    collection,
    nodeType,
    nodeId,
    floorFilter,
    areaFilter,
    typeFilter,
    statusFilter,
  ]);
  const flow = useHotelFlow(key);
  if (state.status === "ANONYMOUS" || state.status === "EXPIRED_INVALID")
    return <Navigate to="/login" replace />;
  if (state.status === "RESTRICTED")
    return <Navigate to="/change-password" replace />;
  if (state.status !== "AUTHENTICATED" || !state.session)
    return (
      <main>
        <p role="status">
          Your session could not be confirmed. Account data is hidden.
        </p>
        {state.status !== "INITIALIZING" && (
          <button type="button" onClick={() => void actions.revalidate()}>
            Check session
          </button>
        )}
      </main>
    );
  const needsId = !["browse", "create"].includes(action);
  const globalList =
    (screen === "rooms" || screen === "roomTypes") &&
    action === "browse" &&
    allBranches;
  const valid =
    (globalList || branchId !== null) &&
    (!needsId || id !== null) &&
    (screen !== "structure" ||
      nodeType === "branch" ||
      decimalId(nodeId) !== null) &&
    pageNumber !== null &&
    [floorFilter, areaFilter, typeFilter].every(
      (v) => v === "" || decimalId(v) !== null,
    );
  const context: Context | null = valid
    ? { branchId: globalList ? 0 : branchId!, ...(needsId ? { id: id! } : {}) }
    : null;
  const filters: Filters = {
    page: pageNumber ?? 1,
    pageSize: 20,
    isActive: active,
    search: query || undefined,
  };
  if (globalList) filters.allBranches = true;
  if (
    screen === "structure" ||
    screen === "floors" ||
    screen === "areas" ||
    screen === "departments"
  )
    filters.collection = screen === "structure" ? collection : screen;
  if (screen === "structure") {
    filters.nodeType = nodeType;
    filters.nodeId =
      nodeType === "branch" ? undefined : (decimalId(nodeId) ?? undefined);
    if (nodeType === "room" || nodeType === "department")
      filters.collection = undefined;
  }
  if (screen === "roomTypes" || screen === "rooms") {
    filters.sort = sort;
    filters.descending = descending;
  }
  if (screen === "rooms") {
    filters.floorId = decimalId(floorFilter) ?? undefined;
    filters.areaId = decimalId(areaFilter) ?? undefined;
    filters.roomTypeId = decimalId(typeFilter) ?? undefined;
    filters.operationalStatusCode = statusFilter || undefined;
  }
  const snapshot = flow.snapshot;
  const typeReadOnly =
    readKind === "roomType" &&
    snapshot &&
    (!(snapshot.data as ReadTypes["roomType"]).canUpdate ||
      (snapshot.data as ReadTypes["roomType"]).details.branchId !== branchId);
  const snapshotValue: unknown = snapshot?.data;
  const inactive =
    object(snapshotValue) &&
    (snapshotValue.isActive === false ||
      (object(snapshotValue.details) &&
        snapshotValue.details.isActive === false));
  const formAllowed =
    !readOnly &&
    !(action === "deactivate" && snapshot && readKind === "roomType" && (snapshot.data as ReadTypes["roomType"]).activeRooms > 0) &&
    context &&
    kind &&
    !flow.unknown &&
    !["pending", "reading", "confirmed", "stale", "denied"].includes(
      flow.phase,
    ) &&
    (action === "create" || (snapshot && !typeReadOnly && !inactive));
  const pending = flow.phase === "pending";
  return (
    <main className="hotel-workspace">
      <h1>Hotel structure — {titles[screen]}</h1>
      <nav aria-label="Hotel structure navigation">
        <Link to="/profile">Profile</Link>
        {screen === "structure" && (
          <Link to="/branches/new">Create Branch</Link>
        )}
        {screen === "structure" && branchId && (
          <Link to={`/branches/${branchId}/edit`}>
            Update branch information
          </Link>
        )}
        {Object.entries(canonicalStructure ? { "/hotel": "Branch structure", "/structure/floors": "Floors", "/structure/areas": "Areas", "/structure/departments": "Departments", "/structure/room-types": "Room Types", "/structure/rooms": "Rooms" }
          : readOnly ? { "/hotel": "Branch structure", [directoryBasePath]: "Room directory" } : {
          "/hotel": "Branch structure",
          "/floors": "Floors",
          "/areas": "Areas",
          "/room-types": "Room types",
          "/rooms": "Rooms",
          "/departments": "Departments",
        }).map(([to, label]) => (
          <Link key={to} to={branchId ? `${to}?branchId=${branchId}` : to}>
            {label}
          </Link>
        ))}
      </nav>
      {!readOnly && !canonicalStructure && <p>
        Data and decisions come from the server. No demo identity or permission
        graph is used.
      </p>}
      {!readOnly && !canonicalStructure && <p>
        Branch listing, branch deactivation, floor/area deactivation, room
        deactivation and department deactivation are outside this integration
        slice.
      </p>}
      <section aria-label="Hotel structure selectors">
        <label>
          Branch ID
          <input
            name="branchId"
            type="text"
            inputMode="numeric"
            value={branch}
            disabled={pending || globalList}
            onChange={(e) => {
              setBranch(e.target.value);
              setPage("1");
            }}
          />
        </label>
        <label>
          Action
          <select
            aria-label="Action"
            value={action}
            disabled={pending}
            onChange={(e) => setAction(e.target.value as Action)}
          >
            <option value="browse">Browse</option>
            {screen !== "structure" && (
              <>
                <option value="detail">Read record</option>
                {!readOnly && <option value="create">Create</option>}
                {!readOnly && <option value="edit">Update</option>}
              </>
            )}
            {!readOnly && screen === "roomTypes" && (
              <option value="deactivate">Deactivate room type</option>
            )}
            {screen === "rooms" && (
              <option value="history">Recorded status history</option>
            )}
          </select>
        </label>
        {needsId && (
          <label>
            Record ID
            <input
              name="recordId"
              type="text"
              inputMode="numeric"
              value={record}
              disabled={pending}
              onChange={(e) => setRecord(e.target.value)}
            />
          </label>
        )}
        {action === "history" && (
          <label>
            History page
            <input
              name="page"
              value={page}
              inputMode="numeric"
              onChange={(e) => setPage(e.target.value)}
            />
          </label>
        )}
        {action === "browse" && (
          <>
            {(screen === "rooms" || screen === "roomTypes") && (
              <label>
                <input
                  type="checkbox"
                  checked={allBranches}
                  onChange={(e) => {
                    setAllBranches(e.target.checked);
                    setPage("1");
                  }}
                />
                All branches (server-authorized)
              </label>
            )}
            <label>
              Active filter
              <select
                aria-label="Active filter"
                value={active}
                onChange={(e) => {
                  setActive(e.target.value);
                  setPage("1");
                }}
              >
                <option value="">All</option>
                <option value="true">Active</option>
                <option value="false">Inactive</option>
              </select>
            </label>
            <label>
              Page
              <input
                name="page"
                inputMode="numeric"
                value={page}
                onChange={(e) => setPage(e.target.value)}
              />
            </label>
            {screen === "structure" && (
              <>
                <label>
                  Structure node
                  <select
                    aria-label="Structure node"
                    value={nodeType}
                    onChange={(e) => {
                      const v = e.target.value;
                      setNodeType(v);
                      setNodeId("");
                      setCollection(
                        v === "branch"
                          ? "floors"
                          : v === "floor"
                            ? "areas"
                            : "rooms",
                      );
                      setPage("1");
                    }}
                  >
                    {["branch", "floor", "area", "room", "department"].map(
                      (v) => (
                        <option key={v}>{v}</option>
                      ),
                    )}
                  </select>
                </label>
                {nodeType !== "branch" && (
                  <label>
                    Node ID
                    <input
                      name="nodeId"
                      inputMode="numeric"
                      value={nodeId}
                      onChange={(e) => setNodeId(e.target.value)}
                    />
                  </label>
                )}
                {["branch", "floor", "area"].includes(nodeType) && (
                  <label>
                    Collection
                    <select
                      aria-label="Collection"
                      value={collection}
                      onChange={(e) => {
                        setCollection(e.target.value);
                        setPage("1");
                      }}
                    >
                      {(nodeType === "branch"
                        ? ["floors", "areas", "departments"]
                        : nodeType === "floor"
                          ? ["areas", "rooms"]
                          : ["rooms"]
                      ).map((v) => (
                        <option key={v}>{v}</option>
                      ))}
                    </select>
                  </label>
                )}
              </>
            )}
            {(screen === "roomTypes" || screen === "rooms") && (
              <>
                <label>
                  Search
                  <input
                    name="search"
                    value={query}
                    maxLength={screen === "rooms" ? 200 : 150}
                    onChange={(e) => {
                      setQuery(e.target.value);
                      setPage("1");
                    }}
                  />
                </label>
                <label>
                  Sort
                  <select
                    aria-label="Sort"
                    value={sort}
                    onChange={(e) => setSort(e.target.value)}
                  >
                    {(screen === "rooms"
                      ? ["floor", "roomNumber", "roomType", "status"]
                      : ["code", "name", "status"]
                    ).map((v) => (
                      <option key={v}>{v}</option>
                    ))}
                  </select>
                </label>
                <label>
                  <input
                    type="checkbox"
                    checked={descending}
                    onChange={(e) => setDescending(e.target.checked)}
                  />
                  Descending
                </label>
              </>
            )}
            {screen === "rooms" && (
              <>
                <label>
                  Filter Floor ID
                  <input
                    name="floorFilter"
                    value={floorFilter}
                    onChange={(e) => setFloorFilter(e.target.value)}
                  />
                </label>
                <label>
                  Filter Area ID
                  <input
                    name="areaFilter"
                    value={areaFilter}
                    onChange={(e) => setAreaFilter(e.target.value)}
                  />
                </label>
                <label>
                  Filter Room Type ID
                  <input
                    name="typeFilter"
                    value={typeFilter}
                    onChange={(e) => setTypeFilter(e.target.value)}
                  />
                </label>
                <label>
                  Stored status
                  <select
                    aria-label="Stored status"
                    value={statusFilter}
                    onChange={(e) => setStatusFilter(e.target.value)}
                  >
                    <option value="">All</option>
                    {[
                      "AVAILABLE",
                      "OCCUPIED",
                      "DIRTY",
                      "CLEANING",
                      "INSPECTION",
                      "READY",
                      "OUT_OF_SERVICE",
                    ].map((v) => (
                      <option key={v}>{v}</option>
                    ))}
                  </select>
                </label>
              </>
            )}
          </>
        )}
        {action !== "create" && (
          <button
            type="button"
            disabled={
              !context ||
              pending ||
              flow.phase === "reading" ||
              flow.phase === "confirmed"
            }
            onClick={() =>
              context && void flow.load(readKind, context, filters)
            }
          >
            Load from server
          </button>
        )}
        {!valid && (
          <p role="status">
            Enter a positive safe integer Branch ID, record ID when required,
            and page. Numeric selectors accept decimal digits only.
          </p>
        )}
      </section>
      {flow.message && <p role="alert">{flow.message}</p>}
      {flow.phase === "reading" && <p role="status">Loading server data...</p>}
      {pending && <p role="status">Change pending...</p>}
      {flow.unknown && <p role="alert">{unknownHotelOutcome}</p>}
      {action === "deactivate" && snapshot && readKind === "roomType" && (snapshot.data as ReadTypes["roomType"]).activeRooms > 0 && <section aria-label="Room type dependencies">
        <p>MSG34: Active rooms use this room type. Change those rooms' type before deactivation.</p>
        <ul>{(snapshot.data as ReadTypes["roomType"]).activeRoomReferences?.map(r => <li key={r.id}>{r.roomNumber} (#{r.id})</li>)}</ul>
      </section>}
      {snapshot && screen === "rooms" && context?.id && <Link to={`/rooms/${context.id}/departments?branchId=${context.branchId}`}>Manage explicit room departments</Link>}
      {snapshot && (
        <section aria-label="Server hotel data">
          <h2>Server record snapshot</h2>
          {screen === "rooms" && action === "browse" ? (
            <RoomDirectoryResults data={snapshot.data as ReadTypes["rooms"]} basePath={directoryBasePath} />
          ) : canonicalStructure && screen === "roomTypes" && action === "browse" ? (
            <RoomTypeDirectoryResults data={snapshot.data as ReadTypes["roomTypes"]} />
          ) : canonicalStructure && action === "browse" ? (
            <>
              <Values value={snapshot.data} />
              {(() => {
                const page = (snapshot.data as ReadTypes["structure"])[screen as "floors" | "areas" | "departments"];
                return page?.items.length ? <ul>{page.items.map(node => <li key={node.id}>
                  <Link to={`/structure/${screen}/${node.id}?branchId=${branchId}`}>Open {node.name} ({node.code})</Link>
                </li>)}</ul> : <p>MSG10: No records match these filters.</p>;
              })()}
            </>
          ) : (
            <Values value={snapshot.data} />
          )}
        </section>
      )}
      {canonicalStructure && screen === "roomTypes" && action === "detail" && context?.id && snapshot &&
        (snapshot.data as ReadTypes["roomType"]).canUpdate && <nav aria-label="Room type actions">
          <Link to={`/structure/room-types/${context.id}?branchId=${context.branchId}&action=edit`}>Update room type</Link>
          <Link to={`/structure/room-types/${context.id}?branchId=${context.branchId}&action=deactivate`}>Review deactivation</Link>
        </nav>}
      {canonicalStructure && (screen === "floors" || screen === "areas") && action === "detail" && context?.id && snapshot &&
        <StructureDeactivationPanel key={`${screen}:${context.branchId}:${context.id}`} kind={screen === "floors" ? "floor" : "area"}
          branchId={context.branchId} id={context.id} dispatchEnabled />}
      {canonicalStructure && screen === "departments" && action === "detail" && context?.id && snapshot && <DepartmentDeactivationPanel key={`${context.branchId}:${context.id}`} branchId={context.branchId} departmentId={context.id} onSaved={() => void flow.load(readKind, context, filters)} />}
      {!canonicalStructure && snapshot &&
        context?.id &&
        (screen === "floors" || screen === "areas") &&
        action === "detail" && (
          <Link
            to={`/${screen}/${context.id}/deactivation-review?branchId=${context.branchId}`}
          >
            Review deactivation dependencies
          </Link>
        )}
      {(typeReadOnly || inactive) && (
        <p>This server record is read-only in this flow.</p>
      )}
      {formAllowed && (
        <>
        {canonicalStructure && screen === "departments" && <p>Changing Department Head also transfers designation authority in the same transaction. Independent mappings and work already assigned are preserved. The new Head must already have the required role, branch access and grants.</p>}
        <MutationForm
          key={`${key}:${snapshot?.revision ?? 0}`}
          kind={kind}
          context={context}
          snapshot={snapshot}
          save={flow.save}
        />
        </>
      )}
      {flow.phase === "confirmed" && (
        <section role="status" aria-label="Confirmed hotel change">
          <h2>Change confirmed by the server</h2>
          {canonicalStructure && <p>MSG08: Changes saved.</p>}
          <Values value={flow.receipt} />
          <p>
            No request has been replayed. Select a different operation to
            continue.
          </p>
        </section>
      )}
    </main>
  );
}
