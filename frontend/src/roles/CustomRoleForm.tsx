import { useEffect, useMemo, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import { Link, useParams } from "react-router-dom";
import { createRoleCatalogApi } from "./roleCatalogApi";
import {
  createCustomRoleApi,
  validCustomRole,
  validMetadata,
  customRoleMessage,
  type CustomRoleInput,
} from "./customRoleApi";
import { type RoleDto, id } from "./contracts";
const keys = new WeakMap<object, number>();
let n = 0;
function key(x: object) {
  if (!keys.has(x)) keys.set(x, ++n);
  return keys.get(x)!;
}
export function CustomRoleEditRoute({ deactivate = false }: { deactivate?: boolean }) {
  const { roleId = "" } = useParams();
  if (!/^[1-9][0-9]*$/.test(roleId) || !id(Number(roleId))) return <p role="alert">Invalid role ID.</p>;
  return <CustomRoleForm roleId={Number(roleId)} dispatchEnabled deactivate={deactivate} />;
}
/** Both operations retain current-session binding and never replay a mutation. */
export function CustomRoleForm({
  roleId,
  dispatchEnabled = false,
  deactivate = false,
}: {
  roleId?: number;
  dispatchEnabled?: boolean;
  deactivate?: boolean;
}) {
  const { state, actions } = useAuth();
  const identity = useMemo(
    () => ({}),
    [state.session, state.status, state.pending],
  );
  const [invalid, setInvalid] = useState<object | null>(null);
  const binding = useMemo(
    () => ({}),
    [state.session, state.status, state.pending, roleId, dispatchEnabled, deactivate],
  );
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
        <p>Confirmed unrestricted session required.</p>
        <button onClick={() => void actions.revalidate()}>
          Revalidate session
        </button>
      </section>
    );
  if (roleId !== undefined && !id(roleId))
    return <p role="alert">Invalid role ID.</p>;
  return (
    <Bound
      key={key(binding)}
      {...{ roleId, dispatchEnabled, current, deactivate }}
      invalidate={() => setInvalid(identity)}
      revalidate={() => void actions.revalidate()}
    />
  );
}
function Bound({
  roleId,
  dispatchEnabled,
  current,
  invalidate,
  deactivate,
  revalidate,
}: {
  roleId?: number;
  dispatchEnabled: boolean;
  current: () => boolean;
  invalidate: () => void;
  deactivate: boolean;
  revalidate: () => void;
}) {
  const [api] = useState(() =>
      createCustomRoleApi(globalThis.fetch.bind(globalThis), dispatchEnabled),
    ),
    [read] = useState(() => createRoleCatalogApi());
  const [input, setInput] = useState<CustomRoleInput>({
      roleCode: "",
      roleName: "",
      description: null,
    }),
    [review, setReview] = useState<CustomRoleInput | null>(null),
    [record, setRecord] = useState<RoleDto | null>(null),
    [version, setVersion] = useState<string | null>(null),
    [loading, setLoading] = useState(roleId !== undefined),
    [pending, setPending] = useState(false),
    [message, setMessage] = useState(""),
    [locked, setLocked] = useState(false),
    [readError, setReadError] = useState(false);
  const mounted = useRef(false),
    attempted = useRef(false),
    controller = useRef<AbortController | null>(null);
  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
      controller.current?.abort();
    };
  }, []);
  useEffect(() => {
    if (roleId === undefined) return;
    const abort = new AbortController();
    let active = true;
    void api.metadata(roleId, abort.signal).then((r) => {
      if (!active || !current()) return;
      setLoading(false);
      if (!r.ok) {
        setReadError(true);
        setMessage("Role unavailable or access denied.");
        return;
      }
      setRecord(r.data.role);
      setVersion(r.data.etag);
      setInput({
        roleCode: r.data.role.roleCode,
        roleName: r.data.role.roleName,
        description: r.data.role.description,
      });
    });
    return () => {
      active = false;
      abort.abort();
    };
  }, [api, roleId, current]);
  async function create() {
    if (
      !review ||
      !dispatchEnabled ||
      attempted.current ||
      !mounted.current ||
      !current() || (deactivate && (!record?.isActive || roleId === undefined))
      || (roleId !== undefined && (!version || !record || record.isSystemRole))
    )
      return;
    attempted.current = true;
    setPending(true);
    controller.current = new AbortController();
    const result = roleId !== undefined ? await api.update(roleId,
      { roleName: review.roleName, description: review.description }, version!,
      () => mounted.current && current(), controller.current.signal, deactivate) : await api.create(
      review,
      () => mounted.current && current(),
      controller.current.signal,
    );
    if (!mounted.current || !current()) return;
    setPending(false);
    setLocked(true);
    setReview(null);
    if (result.outcome !== "CONFIRMED") {
      if (result.status === 401) {
        invalidate();
        return;
      }
      setMessage(customRoleMessage(result));
      return;
    }
    if (deactivate) {
      setRecord(result.data);
      setMessage("Role deactivated. Other role assignments and history are preserved. Affected users must sign in again.");
      return;
    }
    setMessage(
      `Saved custom role ${result.data.roleId}. Refreshing authoritative detail...`,
    );
    const fresh = await read.detail(
      result.data.roleId,
      controller.current.signal,
    );
    if (!mounted.current || !current()) return;
    if (fresh.ok) {
      setRecord(fresh.data.role);
      setMessage(
        `Saved custom role ${fresh.data.role.roleId}. Authoritative detail refreshed.`,
      );
    } else {
      setRecord(null);
      setMessage(
        "Save confirmed, authoritative refresh unavailable. Do not replay Save.",
      );
    }
  }
  return (
    <main>
      <h1>
        {deactivate ? "Deactivate custom role" : roleId === undefined
          ? "Create custom role"
          : "Edit custom role metadata"}
      </h1>
      <Link to="/roles">Back to roles</Link>
      {deactivate && <p>Confirm deactivation. Users will lose access from this role and their current sessions will end. Their other roles remain assigned.</p>}
      {deactivate && locked && <button onClick={revalidate}>Revalidate session</button>}
      <p>
        No automatic grants or assignments. Names do not establish authority.
      </p>
      {!dispatchEnabled && <p>Mutation dispatch OFF.</p>}
      {loading ? (
        <p>Loading role...</p>
      ) : readError ? (
        <p role="alert">{message}</p>
      ) : (
        <>
          <form
            onSubmit={(e) => {
              e.preventDefault();
              if (
                deactivate ? !!record?.isActive && !record.isSystemRole : roleId === undefined
                  ? validCustomRole(input)
                  : validMetadata(input.roleName, input.description)
              ) {
                setReview({ ...input });
                setMessage(
                  deactivate ? "Confirm that this role should become inactive." : roleId === undefined
                    ? "Review metadata before confirming."
                    : "Review the metadata changes before confirming.",
                );
              } else setMessage("Required code/name or metadata is invalid.");
            }}
          >
            {roleId === undefined && (
              <label>
                Role code
                <input
                  value={input.roleCode}
                  disabled={pending || locked}
                  onChange={(e) => {
                    setInput({ ...input, roleCode: e.target.value });
                    setReview(null);
                  }}
                />
              </label>
            )}
            <label>
              Role name
              <input
                value={input.roleName}
                disabled={deactivate || pending || locked || record?.isSystemRole}
                onChange={(e) => {
                  setInput({ ...input, roleName: e.target.value });
                  setReview(null);
                }}
              />
            </label>
            <label>
              Description
              <textarea
                value={input.description ?? ""}
                disabled={deactivate || pending || locked || record?.isSystemRole}
                onChange={(e) => {
                  setInput({ ...input, description: e.target.value });
                  setReview(null);
                }}
              />
            </label>
            {record?.isSystemRole ? (
              <p>System role protected. No metadata changes allowed.</p>
            ) : (
              <button disabled={pending || locked || (deactivate && !record?.isActive)}>{deactivate ? "Review deactivation" : "Review metadata"}</button>
            )}
          </form>
          {review && !locked && (
            <section aria-label="Metadata review">
              <p>
                {review.roleCode} — {review.roleName}
              </p>
              <p>{review.description}</p>
              <button
                disabled={!dispatchEnabled || pending || (roleId !== undefined && (!version || record?.isSystemRole))}
                onClick={() => void create()}
              >
                {deactivate ? "Confirm deactivate" : roleId === undefined ? "Confirm create" : "Confirm update"}
              </button>
            </section>
          )}
          {record && (
            <section aria-label="Authoritative role">
              <p>
                {record.roleCode} — {record.roleName}
              </p>
              <p>
                {record.isSystemRole ? "System" : "Custom"}; grants{" "}
                {record.permissionGrantCount}; users {record.assignedUserCount}
              </p>
            </section>
          )}
        </>
      )}
      {pending && <p role="status">Submitting once...</p>}
      {message && <p role="status">{message}</p>}
    </main>
  );
}
