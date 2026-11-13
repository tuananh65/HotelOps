import { useEffect, useMemo, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
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
/** Standalone candidate only. Production routing is unchanged. */
export function CustomRoleForm({
  roleId,
  dispatchEnabled = false,
}: {
  roleId?: number;
  dispatchEnabled?: boolean;
}) {
  const { state, actions } = useAuth();
  const identity = useMemo(
    () => ({}),
    [state.session, state.status, state.pending],
  );
  const [invalid, setInvalid] = useState<object | null>(null);
  const binding = useMemo(
    () => ({}),
    [state.session, state.status, state.pending, roleId, dispatchEnabled],
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
      {...{ roleId, dispatchEnabled, current }}
      invalidate={() => setInvalid(identity)}
    />
  );
}
function Bound({
  roleId,
  dispatchEnabled,
  current,
  invalidate,
}: {
  roleId?: number;
  dispatchEnabled: boolean;
  current: () => boolean;
  invalidate: () => void;
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
    void read.detail(roleId, abort.signal).then((r) => {
      if (!active || !current()) return;
      setLoading(false);
      if (!r.ok) {
        setReadError(true);
        setMessage("Role unavailable or access denied.");
        return;
      }
      setRecord(r.data.role);
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
  }, [read, roleId, current]);
  async function create() {
    if (
      !review ||
      !dispatchEnabled ||
      attempted.current ||
      !mounted.current ||
      !current()
    )
      return;
    attempted.current = true;
    setPending(true);
    controller.current = new AbortController();
    const result = await api.create(
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
    setMessage(
      `Created custom role ${result.data.roleId}. Refreshing authoritative detail...`,
    );
    const fresh = await read.detail(
      result.data.roleId,
      controller.current.signal,
    );
    if (!mounted.current || !current()) return;
    if (fresh.ok) {
      setRecord(fresh.data.role);
      setMessage(
        `Created custom role ${fresh.data.role.roleId}. Authoritative detail refreshed.`,
      );
    } else {
      setRecord(null);
      setMessage(
        "Create confirmed, authoritative refresh unavailable. Do not replay Create.",
      );
    }
  }
  return (
    <main>
      <h1>
        {roleId === undefined
          ? "Create custom role"
          : "Edit custom role metadata"}
      </h1>
      <p>
        No automatic grants or assignments. Names do not establish authority.
      </p>
      {!dispatchEnabled && <p>Mutation dispatch OFF.</p>}
      {roleId !== undefined && (
        <p>
          Update disabled: durable stale-update contract is not approved. No
          synthetic ETag.
        </p>
      )}
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
                roleId === undefined
                  ? validCustomRole(input)
                  : validMetadata(input.roleName, input.description)
              ) {
                setReview({ ...input });
                setMessage(
                  roleId === undefined
                    ? "Review metadata before confirming."
                    : "Metadata valid; update remains fail-closed.",
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
                disabled={pending || locked || record?.isSystemRole}
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
                disabled={pending || locked || record?.isSystemRole}
                onChange={(e) => {
                  setInput({ ...input, description: e.target.value });
                  setReview(null);
                }}
              />
            </label>
            {record?.isSystemRole ? (
              <p>System role protected. No metadata changes allowed.</p>
            ) : (
              <button disabled={pending || locked}>Review metadata</button>
            )}
          </form>
          {review && !locked && (
            <section aria-label="Metadata review">
              <p>
                {review.roleCode} — {review.roleName}
              </p>
              <p>{review.description}</p>
              <button
                disabled={roleId !== undefined || !dispatchEnabled || pending}
                onClick={() => void create()}
              >
                Confirm create
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
