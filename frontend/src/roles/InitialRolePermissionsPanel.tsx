import {
  useEffect,
  useMemo,
  useRef,
  useState,
  useSyncExternalStore,
} from "react";
import { Link, useParams } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import { createRoleCatalogApi } from "./roleCatalogApi";
import {
  createInitialRolePermissionsApi,
  initialScope,
  type InitialGrant,
} from "./initialRolePermissionsApi";
import { id, type DetailDto, type PermissionDto } from "./contracts";
/** Unmounted workspace for an explicitly approved trial caller; production router unchanged. */
export function InitialRolePermissionsWorkspace() {
  const { roleId = "" } = useParams();
  return (
    <>
      <Link to={`/roles/${Number(roleId)}/permissions`}>
        View read-only permission matrix
      </Link>
      <InitialRolePermissionsPanel roleId={Number(roleId)} />
    </>
  );
}
// Same-origin, page-lifetime reservation, not permission authority or durable idempotency.
// Principal IDs come from the authenticated session; each record is the original operation.
// Reload/restart loses this memory: never use reload to retry an unresolved write.
// An authorized owner must reconcile it with backend evidence before a later trial.
// There is deliberately no timeout, session-change, or client-side unlock for UNKNOWN.
type Reservation = Readonly<{
  operationId: symbol;
  state: "PENDING" | "UNKNOWN";
  hideEditor: boolean;
}>;
const operations = new Map<string, Map<number, Reservation>>();
const listeners = new Set<() => void>();
function subscribe(listener: () => void) {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}
function operation(principal: string, role: number) {
  return operations.get(principal)?.get(role);
}
function notify() {
  for (const listener of listeners) listener();
}
function reserve(principal: string, role: number): Reservation | undefined {
  if (operation(principal, role)) return;
  const original: Reservation = {
    operationId: Symbol("initial-role-permissions"),
    state: "PENDING",
    hideEditor: false,
  };
  const roles = operations.get(principal) ?? new Map<number, Reservation>();
  roles.set(role, original);
  operations.set(principal, roles);
  notify();
  return original;
}
function settle(
  principal: string,
  role: number,
  original: Reservation,
  unknown: boolean,
  hideEditor: boolean,
) {
  if (operation(principal, role) !== original) return;
  if (unknown)
    operations
      .get(principal)!
      .set(role, { ...original, state: "UNKNOWN", hideEditor });
  else {
    operations.get(principal)!.delete(role);
    if (operations.get(principal)!.size === 0) operations.delete(principal);
  }
  notify();
}
const keys = new WeakMap<object, number>();
let next = 0;
function key(o: object) {
  if (!keys.has(o)) keys.set(o, ++next);
  return keys.get(o)!;
}
export function InitialRolePermissionsPanel({
  roleId,
  dispatchEnabled = false,
}: {
  roleId: number;
  dispatchEnabled?: boolean;
}) {
  const { state, actions } = useAuth();
  const identity = useMemo(
    () => ({}),
    [state.session, state.status, state.pending],
  );
  const principal = state.session?.user.id ?? "";
  const reservation = useSyncExternalStore(
    subscribe,
    () => operation(principal, roleId),
    () => undefined,
  );
  const [invalid, setInvalid] = useState<object | null>(null);
  const binding = useMemo(() => ({}), [identity, roleId, dispatchEnabled]);
  const latest = useRef(binding);
  latest.current = binding;
  const current = useMemo(() => () => latest.current === binding, [binding]);
  if (
    state.status !== "AUTHENTICATED" ||
    !state.session ||
    state.session.mustChangePassword ||
    state.pending !== null ||
    invalid === identity
  )
    return (
      <section>
        <p role="alert">Confirmed unrestricted session required.</p>
        <button onClick={() => void actions.revalidate()}>
          Revalidate session
        </button>
      </section>
    );
  if (!id(roleId)) return <p role="alert">Invalid role ID.</p>;
  if (reservation?.hideEditor)
    return (
      <p role="alert">
        Outcome unknown. Do not replay. Verify with an authorized owner.
      </p>
    );
  return (
    <Editor
      key={key(binding)}
      {...{ roleId, dispatchEnabled, current, principal, reservation }}
      invalidate={() => setInvalid(identity)}
    />
  );
}
function Editor({
  roleId,
  dispatchEnabled,
  current,
  invalidate,
  principal,
  reservation,
}: {
  roleId: number;
  dispatchEnabled: boolean;
  current: () => boolean;
  invalidate: () => void;
  principal: string;
  reservation: Reservation | undefined;
}) {
  const [read] = useState(() => createRoleCatalogApi());
  const [write] = useState(() =>
    createInitialRolePermissionsApi(
      globalThis.fetch.bind(globalThis),
      dispatchEnabled,
    ),
  );
  const [detail, setDetail] = useState<DetailDto | null>(null),
    [loadFailed, setLoadFailed] = useState(false),
    [catalog, setCatalog] = useState<PermissionDto[]>([]),
    [selected, setSelected] = useState<InitialGrant[]>([]),
    [review, setReview] = useState<InitialGrant[] | null>(null),
    [message, setMessage] = useState(""),
    [revision, setRevision] = useState(0),
    [pending, setPending] = useState(false),
    [locked, setLocked] = useState(false);
  const mounted = useRef(false),
    busy = useRef(false),
    reviewed = useRef<InitialGrant[] | null>(null),
    controller = useRef<AbortController | null>(null);
  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
      controller.current?.abort();
    };
  }, []);
  useEffect(() => {
    const abort = new AbortController();
    let active = true;
    setDetail(null);
    setLoadFailed(false);
    setCatalog([]);
    setSelected([]);
    setReview(null);
    reviewed.current = null;
    void (async () => {
      const d = await read.detail(roleId, abort.signal);
      if (!active || !current()) return;
      if (!d.ok) {
        setLoadFailed(true);
        if (d.status === 401) invalidate();
        setMessage(
          d.status === 403 ? "Access denied by server." : "Role unavailable.",
        );
        return;
      }
      const permissions: PermissionDto[] = [];
      for (let page = 1; page <= 40; page++) {
        const r = await read.permissions(page, abort.signal);
        if (!active || !current()) return;
        if (!r.ok) {
          setLoadFailed(true);
          if (r.status === 401) invalidate();
          setMessage("Permission catalog unavailable.");
          return;
        }
        if (r.data.total > 1000) {
          setLoadFailed(true);
          setMessage("Catalog exceeds this slice limit.");
          return;
        }
        permissions.push(...r.data.items);
        if (permissions.length >= r.data.total) break;
        if (r.data.items.length === 0) {
          setLoadFailed(true);
          setMessage("Incomplete permission catalog.");
          return;
        }
      }
      if (!active || !current()) return;
      setDetail(d.data);
      setCatalog(permissions);
    })().catch(() => {
      if (active && current()) {
        setLoadFailed(true);
        setMessage("Role catalog unavailable.");
      }
    });
    return () => {
      active = false;
      abort.abort();
    };
  }, [read, roleId, current, revision]);
  const eligible =
    !!detail &&
    detail.role.isActive &&
    !detail.role.isSystemRole &&
    detail.grants.length === 0 &&
    detail.role.assignedUserCount === 0;
  function prepare() {
    if (
      !eligible ||
      !dispatchEnabled ||
      busy.current ||
      operation(principal, roleId) ||
      locked ||
      selected.length === 0
    )
      return;
    const snapshot = selected.map((g) => ({ ...g }));
    reviewed.current = snapshot;
    setReview(snapshot);
  }
  async function confirm() {
    const snapshot = reviewed.current;
    if (!snapshot || busy.current || locked || !current()) return;
    const original = reserve(principal, roleId);
    if (!original) return;
    busy.current = true;
    reviewed.current = null;
    setReview(null);
    setPending(true);
    controller.current = new AbortController();
    let r: Awaited<ReturnType<typeof write.save>>;
    try {
      r = await write.save(
        roleId,
        snapshot,
        () => mounted.current && current(),
        controller.current.signal,
      );
    } catch {
      // Unexpected transport failure cannot prove that no POST reached the server.
      r = { outcome: "OUTCOME_UNKNOWN", reason: "UNCONFIRMED" };
    }
    const live = mounted.current && current();
    // Bookkeeping belongs to the original operation even after the UI authority changes.
    settle(
      principal,
      roleId,
      original,
      r.outcome === "OUTCOME_UNKNOWN",
      live && r.outcome === "OUTCOME_UNKNOWN" && r.status === 401,
    );
    if (!live) return;
    setPending(false);
    if (r.outcome !== "CONFIRMED" && r.status === 401) {
      invalidate();
      return;
    }
    if (r.outcome === "CONFIRMED") {
      setMessage("Initial permissions confirmed. Refreshing server grants.");
      setRevision((n) => n + 1);
    } else if (r.outcome === "OUTCOME_UNKNOWN") {
      setMessage(
        "Outcome unknown. Do not replay. Verify with an authorized owner.",
      );
      setLocked(true);
    } else {
      setMessage(
        r.status === 409
          ? "Role changed or already configured. Reload and review."
          : r.status === 403
            ? "Access denied by server."
            : "Initial configuration not confirmed.",
      );
    }
    busy.current = false;
  }
  return (
    <section aria-label="Initial role permissions">
      <h1>Initial role permission configuration</h1>
      <p>
        Role {roleId}. Server validates global authority and complete assignment
        history.
      </p>
      {!dispatchEnabled && (
        <p>Mutation dispatch OFF pending runtime approval.</p>
      )}
      {reservation && !message && (
        <p role="status">
          {reservation.state === "PENDING"
            ? "Original operation pending. Do not replay."
            : "Outcome unknown. Do not replay. Verify with an authorized owner."}
        </p>
      )}
      {reservation && (
        <p>
          Reloading or restarting does not reconcile this operation. Obtain
          backend-authoritative verification before any later attempt.
        </p>
      )}
      {message && <p role="status">{message}</p>}
      {!detail ? (
        <p>
          {loadFailed
            ? "No editable role snapshot available."
            : "Loading role and catalog..."}
        </p>
      ) : (
        <>
          <h2>{detail.role.roleName}</h2>
          <p>Existing grants: {detail.grants.length}</p>
          {detail.grants.map((g) => (
            <p key={g.rolePermissionId}>
              {g.permission.permissionCode}: {g.dataScope}
            </p>
          ))}
          {!eligible ? (
            <p>This role is not eligible for initial configuration.</p>
          ) : (
            <>
              {[...new Set(catalog.map((p) => p.moduleCode))].map((module) => (
                <fieldset
                  key={module}
                  disabled={pending || locked || !dispatchEnabled}
                >
                  <legend>{module}</legend>
                  {catalog
                    .filter((p) => p.moduleCode === module)
                    .map((p) => {
                      const scope = initialScope(p.permissionCode);
                      return (
                        <label key={p.permissionId}>
                          <input
                            type="checkbox"
                            aria-label={p.permissionCode}
                            disabled={!scope}
                            checked={selected.some(
                              (g) => g.permissionId === p.permissionId,
                            )}
                            onChange={(e) => {
                              setReview(null);
                              reviewed.current = null;
                              setSelected((v) =>
                                e.target.checked
                                  ? [
                                      ...v,
                                      {
                                        permissionId: p.permissionId,
                                        dataScope: scope!,
                                      },
                                    ]
                                  : v.filter(
                                      (g) => g.permissionId !== p.permissionId,
                                    ),
                              );
                            }}
                          />
                          {p.permissionName}
                          {scope ? (
                            <select
                              aria-label={"Scope " + p.permissionCode}
                              value={scope}
                              disabled
                            >
                              <option>{scope}</option>
                            </select>
                          ) : (
                            <span>Unsupported in restricted V1</span>
                          )}
                        </label>
                      );
                    })}
                </fieldset>
              ))}
              <button
                disabled={
                  !dispatchEnabled ||
                  pending ||
                  locked ||
                  !!reservation ||
                  selected.length === 0
                }
                onClick={prepare}
              >
                Review permissions
              </button>
            </>
          )}
        </>
      )}
      {review && (
        <section aria-label="Review permissions">
          <p>
            Configure Role {roleId} once; no assignments or existing grants will
            change.
          </p>
          {review.map((g) => (
            <p key={g.permissionId}>
              Permission {g.permissionId}: {g.dataScope}
            </p>
          ))}
          <button
            disabled={pending || locked || !!reservation}
            onClick={() => void confirm()}
          >
            Confirm and save
          </button>
          <button
            onClick={() => {
              reviewed.current = null;
              setReview(null);
            }}
          >
            Cancel
          </button>
        </section>
      )}
    </section>
  );
}
