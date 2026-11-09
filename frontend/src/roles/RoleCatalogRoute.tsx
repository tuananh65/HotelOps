import { useEffect, useMemo, useRef, useState } from "react";
import { Link, Navigate, useParams } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import { createRoleCatalogApi, type CatalogResult } from "./roleCatalogApi";
import {
  type RoleDto,
  type DetailDto,
  type PermissionDto,
  type Page,
} from "./contracts";
const keys = new WeakMap<object, number>();
let next = 0;
function key(x: object) {
  if (!keys.has(x)) keys.set(x, ++next);
  return keys.get(x)!;
}
export function RoleCatalogRoute({
  mode,
}: {
  mode: "list" | "detail" | "matrix" | "catalog";
}) {
  const { state, actions } = useAuth();
  const { roleId = "" } = useParams();
  const binding = useMemo(
    () => ({}),
    [state.session, state.status, state.pending, mode, roleId],
  );
  const latest = useRef(binding);
  latest.current = binding;
  const current = useMemo(() => () => latest.current === binding, [binding]);
  if (state.status === "RESTRICTED" || state.session?.mustChangePassword)
    return <Navigate replace to="/change-password" />;
  if (state.status === "ANONYMOUS" || state.status === "EXPIRED_INVALID")
    return <Navigate replace to="/login" />;
  if (
    state.status !== "AUTHENTICATED" ||
    !state.session ||
    state.pending !== null
  )
    return (
      <section>
        <p>Session not confirmed.</p>
        <button onClick={() => void actions.revalidate()}>
          Revalidate session
        </button>
      </section>
    );
  if (mode !== "list" && mode !== "catalog" && !/^[1-9][0-9]*$/.test(roleId))
    return <p role="alert">Invalid role ID.</p>;
  return (
    <Catalog
      key={key(binding)}
      {...{ mode, roleId: Number(roleId), current }}
      revalidate={() => void actions.revalidate()}
    />
  );
}
function ErrorView({
  error,
  revalidate,
}: {
  error: { status?: number; kind: string };
  revalidate: () => void;
}) {
  return (
    <section role="alert">
      <p>
        {error.status === 401
          ? "Session invalid. Revalidate before continuing."
          : error.status === 403
            ? "Access denied by server."
            : error.status === 404
              ? "Role not found."
              : error.status === 503
                ? "Catalog unavailable."
                : "Catalog could not be loaded."}
      </p>
      {error.status === 401 && (
        <button onClick={revalidate}>Revalidate session</button>
      )}
    </section>
  );
}
function Catalog({
  mode,
  roleId,
  current,
  revalidate,
}: {
  mode: "list" | "detail" | "matrix" | "catalog";
  roleId: number;
  current: () => boolean;
  revalidate: () => void;
}) {
  const [api] = useState(() => createRoleCatalogApi());
  const [p, setP] = useState(1),
    [draft, setDraft] = useState(""),
    [search, setSearch] = useState(""),
    [active, setActive] = useState("");
  const [result, setResult] = useState<{
      binding: object;
      value: CatalogResult<Page<RoleDto> | DetailDto | Page<PermissionDto>>;
    } | null>(null),
    [composition, setComposition] = useState<{
      binding: object;
      value: CatalogResult<{ roleId: number; grants: DetailDto["grants"] }>;
    } | null>(null);
  const request = useMemo(() => ({}), [p, search, active, mode, roleId]);
  const latest = useRef(request);
  latest.current = request;
  useEffect(() => {
    const abort = new AbortController();
    let mounted = true;
    const accept = () => mounted && current() && latest.current === request;
    const read =
      mode === "list"
        ? api.roles(p, search, active, abort.signal)
        : mode === "detail"
          ? api.detail(roleId, abort.signal)
          : api.permissions(p, abort.signal);
    void read.then((r) => {
      if (accept()) setResult({ binding: request, value: r });
    });
    if (mode === "matrix")
      void api.grants(roleId, abort.signal).then((r) => {
        if (accept()) setComposition({ binding: request, value: r });
      });
    return () => {
      mounted = false;
      abort.abort();
    };
  }, [api, p, search, active, mode, roleId, current, request]);
  const r = result?.binding === request ? result.value : null,
    g = composition?.binding === request ? composition.value : null;
  const pagination = (total: number) => (
    <nav aria-label="Pagination">
      <button disabled={p === 1} onClick={() => setP((n) => n - 1)}>
        Previous
      </button>
      <span>Page {p}</span>
      <button disabled={p * 25 >= total} onClick={() => setP((n) => n + 1)}>
        Next
      </button>
    </nav>
  );
  return (
    <main>
      <h1>
        {mode === "list"
          ? "Role directory"
          : mode === "detail"
            ? "Role detail"
            : mode === "catalog"
              ? "Permission catalog"
              : "Permission matrix"}
      </h1>
      <nav>
        <Link to="/profile">Profile</Link>
        <Link to="/roles">Roles</Link>
        <Link to="/permissions">Permission catalog</Link>
      </nav>
      <p>
        Read only. No role or permission mutation is available. Server
        authorization is required.
      </p>
      {mode === "list" && (
        <form
          onSubmit={(e) => {
            e.preventDefault();
            setP(1);
            setSearch(draft);
          }}
        >
          <label>
            Search roles
            <input
              maxLength={100}
              value={draft}
              onChange={(e) => setDraft(e.target.value)}
            />
          </label>
          <label>
            Role status
            <select
              aria-label="Role status"
              value={active}
              onChange={(e) => {
                setP(1);
                setActive(e.target.value);
              }}
            >
              <option value="">All</option>
              <option value="true">Active</option>
              <option value="false">Inactive</option>
            </select>
          </label>
          <button>Search</button>
        </form>
      )}
      {!r ? (
        <p role="status">Loading catalog...</p>
      ) : !r.ok ? (
        <ErrorView error={r} revalidate={revalidate} />
      ) : mode === "list" ? (
        (() => {
          const d = r.data as Page<RoleDto>;
          return (
            <>
              <p>
                Assigned users: distinct ACTIVE employed nondeleted accounts
                with currently active assignments; not effective-access count.
              </p>
              {!d.items.length && <p>No roles found.</p>}
              <ul>
                {d.items.map((x) => (
                  <li key={x.roleId}>
                    <Link to={`/roles/${x.roleId}`}>
                      {x.roleCode} — {x.roleName}
                    </Link>{" "}
                    {x.isActive ? "Active" : "Inactive"};{" "}
                    {x.isSystemRole ? "System" : "Custom"}; users{" "}
                    {x.assignedUserCount}; grants {x.permissionGrantCount}
                  </li>
                ))}
              </ul>
              {pagination(d.total)}
            </>
          );
        })()
      ) : mode === "detail" ? (
        (() => {
          const d = r.data as DetailDto;
          return (
            <>
              <h2>
                {d.role.roleCode} — {d.role.roleName}
              </h2>
              <p>{d.role.description}</p>
              <p>
                {d.role.isActive ? "Active" : "Inactive"};{" "}
                {d.role.isSystemRole
                  ? "System role — protected"
                  : "Custom role"}
                . Editing and deactivation are unavailable for every role.
              </p>
              <Link to={`/roles/${roleId}/permissions`}>Permission matrix</Link>
              <ul>
                {d.grants.map((x) => (
                  <li key={x.rolePermissionId}>
                    {x.permission.permissionCode}: {x.dataScope}
                  </li>
                ))}
              </ul>
              {!d.grants.length && <p>No grants.</p>}
            </>
          );
        })()
      ) : mode === "catalog" ? (
        (() => {
          const d = r.data as Page<PermissionDto>;
          return (
            <>
              {!d.items.length && <p>No permissions found.</p>}
              {[...new Set(d.items.map((x) => x.moduleCode))].map((module) => (
                <section key={module}>
                  <h2>{module}</h2>
                  {d.items
                    .filter((x) => x.moduleCode === module)
                    .map((x) => (
                      <p key={x.permissionId}>
                        {x.permissionCode} — {x.permissionName};{" "}
                        {x.resourceCode}/{x.actionCode}
                      </p>
                    ))}
                </section>
              ))}
              {pagination(d.total)}
            </>
          );
        })()
      ) : !g ? (
        <p role="status">Loading grants...</p>
      ) : !g.ok ? (
        <ErrorView error={g} revalidate={revalidate} />
      ) : (
        (() => {
          const d = r.data as Page<PermissionDto>;
          return (
            <>
              <p>
                Catalog and grants are separate current read snapshots; no
                aggregate ETag or write authority.
              </p>
              {!d.items.length && <p>No permissions found.</p>}
              {[...new Set(d.items.map((x) => x.moduleCode))].map((module) => (
                <section key={module}>
                  <h2>{module}</h2>
                  {d.items
                    .filter((x) => x.moduleCode === module)
                    .map((x) => {
                      const found = g.data.grants.filter(
                        (a) => a.permission.permissionId === x.permissionId,
                      );
                      return (
                        <p key={x.permissionId}>
                          {x.permissionCode} — {x.permissionName}:{" "}
                          {found.length
                            ? `Granted (${found.map((a) => a.dataScope).join(", ")})`
                            : "Not Granted"}
                        </p>
                      );
                    })}
                </section>
              ))}
              {pagination(d.total)}
            </>
          );
        })()
      )}
    </main>
  );
}
