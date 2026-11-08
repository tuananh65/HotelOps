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
        <Link to="/branches/new">Create Branch</Link>
        {decimalId(branch) && (
          <Link to={`/branches/${decimalId(branch)}/edit`}>
            Update branch information
          </Link>
        )}
        <Link to="/floors">Floors</Link>
        <Link to="/areas">Areas</Link>
        <Link to="/rooms">Rooms</Link>
        <Link to="/room-types">Room types</Link>
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
          Branch ID
          <input
            name="branchId"
            inputMode="numeric"
            value={branch}
            onChange={(event) => {
              setBranch(event.target.value);
              setRequest(null);
            }}
          />
        </label>
        <button disabled={!decimalId(branch) || flow.phase === "reading"}>
          Load from server
        </button>
      </form>
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
                <p>No records on this page.</p>
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
