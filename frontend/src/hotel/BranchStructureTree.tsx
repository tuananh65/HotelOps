import { useEffect, useRef, useState } from "react";
import { Link, Navigate, useLocation, useParams } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import type { PublicSession } from "../auth/authStore";
import { decimalId, type StructureDto } from "./contracts";
import { useHotelFlow } from "./useHotelFlow";
import {
  boundStructure,
  collectionsFor,
  defaultCollection,
  type StructureNode,
  type StructureQuery,
  type StructureCollection,
} from "./structureView";
type Trail = { type: StructureNode; id: number; label: string };
type Request = { owner: PublicSession; query: StructureQuery; trail: Trail[] };
const heading = {
  floors: "Floors",
  areas: "Areas",
  rooms: "Rooms",
  departments: "Departments",
};
const singular = {
  floors: "floor",
  areas: "area",
  rooms: "room",
  departments: "department",
} as const;
export function BranchStructureTree() {
  const { state, actions } = useAuth();
  const params = useParams(),
    location = useLocation();
  const owner =
    state.status === "AUTHENTICATED" && state.pending === null
      ? state.session
      : null;
  const [branch, setBranch] = useState(
    params.branchId ??
      new URLSearchParams(location.search).get("branchId") ??
      "",
  );
  const [active, setActive] = useState("");
  const [request, setRequest] = useState<Request | null>(null);
  const [branchOptions, setBranchOptions] = useState<{ owner: PublicSession; items: { id: number; code: string; name: string; isActive: boolean }[] } | null>(null);
  const [branchError, setBranchError] = useState("");
  const [optionsReload, setOptionsReload] = useState(0);
  useEffect(() => {
    setBranchOptions(null); setBranchError("");
    if (!owner || owner.mustChangePassword) return;
    const abort = new AbortController();
    void (async () => {
      try {
        const response = await fetch("/api/branches/structure-options", { credentials: "include", cache: "no-store", redirect: "error",
          signal: abort.signal, headers: { Accept: "application/json" } });
        if (!response.ok || response.headers.get("Content-Type")?.split(";")[0] !== "application/json") throw new Error();
        const items: unknown = await response.json();
        if (!Array.isArray(items) || !items.every(item => item && typeof item === "object" && Number.isSafeInteger(item.id)
          && item.id > 0 && typeof item.code === "string" && typeof item.name === "string" && typeof item.isActive === "boolean")
          || new Set(items.map(item => item.id)).size !== items.length) throw new Error();
        if (!abort.signal.aborted) setBranchOptions({ owner, items });
      } catch { if (!abort.signal.aborted) setBranchError("Authorized branches could not be loaded. Refresh or check your session."); }
    })();
    return () => abort.abort();
  }, [owner, optionsReload]);
  const key = JSON.stringify([branch, active, request?.query]);
  const flow = useHotelFlow(key);
  const load = useRef(flow.load);
  load.current = flow.load;
  useEffect(() => {
    if (request && request.owner === owner)
      void load.current(
        "structure",
        { branchId: request.query.branchId },
        { ...request.query },
      );
  }, [request, owner]);
  if (state.status === "ANONYMOUS" || state.status === "EXPIRED_INVALID")
    return <Navigate replace to="/login" />;
  if (state.status === "RESTRICTED")
    return <Navigate replace to="/change-password" />;
  if (!owner || state.session?.mustChangePassword)
    return (
      <main>
        <p role="status">
          Your session could not be confirmed. Structure data is hidden.
        </p>
        {state.status !== "INITIALIZING" && (
          <button onClick={() => void actions.revalidate()}>
            Check session
          </button>
        )}
      </main>
    );
  const selected = request?.owner === owner ? request : null;
  const raw = selected && (flow.snapshot?.data as StructureDto | null);
  const data =
    raw && selected && boundStructure(raw, selected.query) ? raw : null;
  const page = data?.collection
    ? data[data.collection as StructureCollection]
    : null;
  function root() {
    const id = decimalId(branch);
    if (!id) return;
    setRequest({
      owner: owner!,
      query: {
        branchId: id,
        nodeType: "branch",
        collection: "floors",
        isActive: active,
        page: 1,
        pageSize: 20,
      },
      trail: [],
    });
  }
  function select(type: StructureNode, id: number, label: string) {
    if (!selected) return;
    setRequest({
      owner: owner!,
      query: {
        ...selected.query,
        nodeType: type,
        nodeId: id,
        collection: defaultCollection(type),
        page: 1,
      },
      trail: [...selected.trail, { type, id, label }],
    });
  }
  function ancestor(index: number) {
    if (!selected) return;
    if (index < 0) {
      setRequest({
        ...selected,
        query: {
          ...selected.query,
          nodeType: "branch",
          nodeId: undefined,
          collection: "floors",
          page: 1,
        },
        trail: [],
      });
      return;
    }
    const node = selected.trail[index];
    setRequest({
      ...selected,
      query: {
        ...selected.query,
        nodeType: node.type,
        nodeId: node.id,
        collection: defaultCollection(node.type),
        page: 1,
      },
      trail: selected.trail.slice(0, index + 1),
    });
  }
  const details =
    data?.nodeType === "branch"
      ? data.branch
      : data?.nodeType === "floor"
        ? data.selectedFloor
        : data?.nodeType === "area"
          ? data.selectedArea
          : data?.nodeType === "room"
            ? data.selectedRoom
            : data?.selectedDepartment;
  return (
    <main className="hotel-workspace">
      <h1>Hotel structure — Branch structure</h1>
      <nav aria-label="Hotel structure navigation">
        <Link to="/profile">Profile</Link>
        <Link to={`/structure/floors?branchId=${encodeURIComponent(branch)}&action=create`}>Add Floor</Link>
        <Link to={`/structure/areas?branchId=${encodeURIComponent(branch)}&action=create`}>Add Area</Link>
        <Link to={`/structure/floors?branchId=${encodeURIComponent(branch)}`}>Manage Floors</Link>
        <Link to={`/structure/areas?branchId=${encodeURIComponent(branch)}`}>Manage Areas</Link>
        <Link to={`/structure/departments?branchId=${encodeURIComponent(branch)}&action=create`}>Add Department</Link>
        <Link to={`/structure/departments?branchId=${encodeURIComponent(branch)}`}>Departments</Link>
        <Link to={`/structure/room-types?branchId=${encodeURIComponent(branch)}`}>Room Types</Link>
        <Link to={`/structure/rooms?branchId=${encodeURIComponent(branch)}&action=create`}>Add Room</Link>
        <Link to={`/structure/rooms?branchId=${encodeURIComponent(branch)}`}>Manage Rooms</Link>
      </nav>
      <p>
        Browse one server-authorized collection page at a time. Counts do not
        imply all children are loaded.
      </p>
      <form
        onSubmit={(event) => {
          event.preventDefault();
          root();
        }}
      >
        <label>
          Branch
          <select name="branchId" value={branch} disabled={branchOptions?.owner !== owner}
            onChange={event => { setBranch(event.target.value); setRequest(null); }}>
            <option value="">Choose an authorized branch</option>
            {branchOptions?.owner === owner && branchOptions.items.map(item => <option key={item.id} value={item.id}>
              {item.code} ? {item.name}{item.isActive ? "" : " (Inactive)"}
            </option>)}
          </select>
        </label>
        <button disabled={!decimalId(branch) || branchOptions?.owner !== owner || !branchOptions.items.some(item => String(item.id) === branch) || flow.phase === "reading"}>
          Load from server
        </button>
      </form>
      {branchError && <p role="alert">{branchError}</p>}
      <button type="button" onClick={() => setOptionsReload(value => value + 1)}>Refresh authorized branches</button>
      <label>
        Active filter
        <select
          aria-label="Active filter"
          value={active}
          onChange={(event) => {
            setActive(event.target.value);
            if (selected)
              setRequest({
                ...selected,
                query: {
                  ...selected.query,
                  isActive: event.target.value,
                  page: 1,
                },
              });
          }}
        >
          <option value="">All</option>
          <option value="true">Active</option>
          <option value="false">Inactive</option>
        </select>
      </label>
      {flow.phase === "reading" && <p role="status">Loading server data...</p>}
      {flow.message && <p role="alert">{flow.message}</p>}
      {raw && !data && (
        <p role="alert">
          Structure response does not match the selected context. No data
          displayed.
        </p>
      )}
      {data && selected && (
        <section aria-label="Progressive branch structure">
          <p aria-label="Applied structure filters">Branch: {data.branch.code}; status: {selected.query.isActive === "" ? "All" : selected.query.isActive === "true" ? "Active" : "Inactive"}; collection: {data.collection ?? "Selected node"}.</p>
          <nav aria-label="Structure path">
            <button onClick={() => ancestor(-1)}>
              Branch: {data.branch.name}
            </button>
            {selected.trail.map((node, index) => (
              <button key={node.type + node.id} onClick={() => ancestor(index)}>
                {node.type}: {node.label}
              </button>
            ))}
          </nav>
          {data.visibility === "management" && <nav aria-label="Selected node management">
            {data.nodeType === "branch" && <Link to={`/structure/branches/${data.branch.id}/edit`}>Update branch information</Link>}
            {(data.nodeType === "floor" || data.nodeType === "area") && selected.query.nodeId &&
              <Link to={`/structure/${data.nodeType}s/${selected.query.nodeId}?branchId=${data.branch.id}`}>Manage selected {data.nodeType}</Link>}
          </nav>}
          <section aria-label="Selected node details">
            <h2>Selected {data.nodeType}</h2>
            <dl>
              {Object.entries(details ?? {}).map(([field, value]) => (
                <div key={field}>
                  <dt>{field}</dt>
                  <dd>{value === null ? "Not provided" : String(value)}</dd>
                </div>
              ))}
            </dl>
          </section>
          <nav aria-label="Available collections">
            {collectionsFor(data.nodeType as StructureNode)
              .filter((collection) =>
                data.availableCollections.includes(collection),
              )
              .map((collection) => (
                <button
                  key={collection}
                  aria-pressed={data.collection === collection}
                  onClick={() =>
                    setRequest({
                      ...selected,
                      query: { ...selected.query, collection, page: 1 },
                    })
                  }
                >
                  {heading[collection]}
                </button>
              ))}
          </nav>
          {page && data.collection && (
            <section
              aria-label={`${heading[data.collection as StructureCollection]} page`}
            >
              <h2>{heading[data.collection as StructureCollection]}</h2>
              <p>
                {page.totalCount} records; page {page.page} of{" "}
                {Math.max(1, Math.ceil(page.totalCount / page.pageSize))}; page
                size {page.pageSize}.
              </p>
              {page.items.length === 0 ? (
                <p>MSG10: No records match the applied filters on this page.</p>
              ) : (
                <ul aria-label="Structure nodes">
                  {page.items.map((item) => {
                    const label =
                      "roomNumber" in item ? item.roomNumber : item.name;
                    return (
                      <li key={item.id}>
                        <button
                          onClick={() =>
                            select(
                              singular[data.collection as StructureCollection],
                              item.id,
                              label,
                            )
                          }
                        >
                          {singular[data.collection as StructureCollection]}:{" "}
                          {label}
                        </button>
                        <span> — {item.isActive ? "Active" : "Inactive"}</span>
                      </li>
                    );
                  })}
                </ul>
              )}
              <button
                disabled={page.page <= 1}
                onClick={() =>
                  setRequest({
                    ...selected,
                    query: { ...selected.query, page: page.page - 1 },
                  })
                }
              >
                Previous page
              </button>
              <button
                disabled={page.page * page.pageSize >= page.totalCount}
                onClick={() =>
                  setRequest({
                    ...selected,
                    query: { ...selected.query, page: page.page + 1 },
                  })
                }
              >
                Next page
              </button>
            </section>
          )}
        </section>
      )}
    </main>
  );
}
