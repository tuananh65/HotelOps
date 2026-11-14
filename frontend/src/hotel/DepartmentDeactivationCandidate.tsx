import { useEffect, useMemo, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import { createUsersApi } from "../users/usersApi";
import type { UserDirectoryItemDto } from "../users/contracts";
import { createHotelApi } from "./hotelApi";
import { strongTag, type DepartmentDto } from "./contracts";
import { createDepartmentDeactivationApi } from "./departmentDeactivationApi";
type Accounts =
  | { kind: "loading" }
  | { kind: "ready"; items: readonly UserDirectoryItemDto[]; total: number }
  | { kind: "unavailable"; status?: number };
/** Independent server reads, never a complete dependency/permission proof; no production route imports. */
export function DepartmentDeactivationCandidate({
  branchId,
  departmentId,
  dispatchEnabled = false,
}: {
  branchId: number;
  departmentId: number;
  dispatchEnabled?: boolean;
}) {
  const { state, actions } = useAuth();
  const identity = useMemo(
    () => ({}),
    [state.session, state.status, state.pending, branchId, departmentId],
  );
  const latest = useRef(identity);
  latest.current = identity;
  if (
    state.status !== "AUTHENTICATED" ||
    !state.session ||
    state.pending !== null
  )
    return (
      <p role="status">
        Confirm an unrestricted session before reviewing a department.
      </p>
    );
  return (
    <Bound
      key={key(identity)}
      branchId={branchId}
      departmentId={departmentId}
      enabled={dispatchEnabled}
      current={() => latest.current === identity}
      recover={() => void actions.revalidate()}
    />
  );
}
const keys = new WeakMap<object, number>();
let serial = 0;
function key(v: object) {
  if (!keys.has(v)) keys.set(v, ++serial);
  return keys.get(v)!;
}
function Bound({
  branchId,
  departmentId,
  enabled,
  current,
  recover,
}: {
  branchId: number;
  departmentId: number;
  enabled: boolean;
  current: () => boolean;
  recover: () => void;
}) {
  const [data, setData] = useState<{
      department: DepartmentDto;
      etag: string;
    } | null>(null),
    [accounts, setAccounts] = useState<Accounts>({ kind: "loading" }),
    [phase, setPhase] = useState("loading"),
    [reason, setReason] = useState(""),
    [review, setReview] = useState(false),
    [status, setStatus] = useState<number>();
  const [api] = useState(() =>
    createDepartmentDeactivationApi(globalThis.fetch.bind(globalThis), enabled),
  );
  const alive = useRef(true),
    busy = useRef(false),
    attempted = useRef(false),
    controller = useRef<AbortController | null>(null);
  useEffect(() => {
    alive.current = true;
    const abort = new AbortController();
    controller.current = abort;
    void (async () => {
      const r = await createHotelApi().read(
        "department",
        { branchId, id: departmentId },
        {},
        abort.signal,
      );
      if (!alive.current || !current()) return;
      if (!r.ok || !strongTag(r.etag)) {
        setPhase("unavailable");
        if (!r.ok) setStatus(r.error.status);
        return;
      }
      setData({ department: r.data, etag: r.etag });
      setPhase("ready");
      if (!r.data.isActive) {
        setAccounts({ kind: "unavailable" });
        return;
      }
      const users = await createUsersApi().listUsers(
        {
          branchId,
          departmentId,
          accountStatus: "ACTIVE",
          page: 1,
          pageSize: 20,
        },
        abort.signal,
      );
      if (!alive.current || !current()) return;
      if (!users.ok) {
        const s = users.error.kind === "http" ? users.error.status : undefined;
        setAccounts({ kind: "unavailable", status: s });
        if (s === 401) {
          setData(null);
          setReason("");
          setReview(false);
          setPhase("unavailable");
          setStatus(401);
        }
        return;
      }
      // Never display an unrelated department or a non-ACTIVE account in this scoped dependency view.
      if (
        users.data.items.some(
          (u) =>
            u.department?.id !== departmentId || u.accountStatus !== "ACTIVE",
        )
      ) {
        setAccounts({ kind: "unavailable" });
        return;
      }
      setAccounts({
        kind: "ready",
        items: users.data.items,
        total: users.data.totalCount,
      });
    })();
    return () => {
      alive.current = false;
      abort.abort();
      controller.current?.abort();
    };
  }, [branchId, departmentId]);
  const observedUsers = accounts.kind === "ready" && accounts.total > 0;
  const valid =
    reason.trim().length >= 10 &&
    reason.trim().length <= 1000 &&
    !/[\x00-\x1f\x7f-\x9f]/.test(reason);
  async function confirm() {
    if (
      !enabled ||
      !data ||
      !review ||
      !valid ||
      observedUsers ||
      accounts.kind === "loading" ||
      busy.current ||
      attempted.current ||
      !current()
    )
      return;
    busy.current = true;
    attempted.current = true;
    setPhase("pending");
    const abort = new AbortController();
    controller.current = abort;
    const r = await api.change(
      branchId,
      departmentId,
      data.etag,
      reason,
      () => alive.current && current(),
      abort.signal,
    );
    if (!alive.current || !current()) return;
    setData(null);
    setAccounts({ kind: "unavailable" });
    setReason("");
    setReview(false);
    busy.current = false;
    if (r.outcome === "CONFIRMED") setPhase("confirmed");
    else {
      setStatus(r.status);
      setPhase(r.outcome === "OUTCOME_UNKNOWN" ? "unknown" : "rejected");
    }
  }
  return (
    <main>
      <h1>Department deactivation review</h1>
      <p>
        Business activation remains blocked. These reads are not permission or
        complete dependency clearance.
      </p>
      {data && (
        <>
          <section aria-label="Selected department">
            <h2>{data.department.name}</h2>
            <p>
              Branch {data.department.branchId}; Department {data.department.id}
              ; {data.department.isActive ? "Active" : "Inactive"}
            </p>
          </section>
          <section aria-label="Active account observations">
            <h2>Active accounts visible to you</h2>
            {accounts.kind === "loading" ? (
              <p role="status">Loading scoped accounts...</p>
            ) : accounts.kind === "unavailable" ? (
              <p>
                {accounts.status === 403
                  ? "Account review denied by server; this does not mean zero active users."
                  : "Account coverage unavailable; no clearance can be inferred."}
              </p>
            ) : (
              <>
                <p>
                  Server-visible total: {accounts.total}. First page only;
                  authorization scope may omit other accounts.
                </p>
                {accounts.items.length ? (
                  <ul>
                    {accounts.items.map((u) => (
                      <li key={u.id}>
                        <a href={`/users/${u.id}`}>{u.fullName}</a>
                      </li>
                    ))}
                  </ul>
                ) : (
                  <p>
                    No visible active accounts. This is not complete dependency
                    clearance.
                  </p>
                )}
              </>
            )}
            {observedUsers && (
              <p role="alert">
                Resolve observed active accounts before confirming deactivation.
              </p>
            )}
          </section>
          <section aria-label="Remaining dependency review">
            <h2>Owner verification required</h2>
            <ul>
              {[
                "Warehouses",
                "Open tasks",
                "Open internal requests",
                "Assets in custody",
              ].map((name) => (
                <li key={name}>
                  {name}: not verified; reader and writer acceptance required.
                </li>
              ))}
            </ul>
          </section>
          {!data.department.isActive && (
            <p>
              This department is already inactive; no deactivation controls are
              available.
            </p>
          )}
          {phase === "ready" && data.department.isActive && (
            <>
              <label>
                Reason
                <textarea
                  value={reason}
                  disabled={review}
                  onChange={(e) => setReason(e.target.value)}
                />
              </label>
              {!review ? (
                <button
                  disabled={
                    !valid || observedUsers || accounts.kind === "loading"
                  }
                  onClick={() => setReview(true)}
                >
                  Review deactivation
                </button>
              ) : (
                <>
                  <p>
                    Confirm this department and reason. Server must revalidate
                    every dependency in its transaction.
                  </p>
                  <button
                    disabled={!enabled || observedUsers}
                    onClick={() => void confirm()}
                  >
                    Confirm department deactivation
                  </button>
                  <button onClick={() => setReview(false)}>Edit reason</button>
                </>
              )}
            </>
          )}
        </>
      )}
      {phase === "loading" && <p role="status">Loading department...</p>}
      {phase === "pending" && <p role="status">Department change pending...</p>}
      {phase === "confirmed" && (
        <p role="status">Server confirmed department deactivation.</p>
      )}
      {["unavailable", "rejected", "unknown"].includes(phase) && (
        <p role="alert">
          {phase === "unknown"
            ? "Outcome unknown. Do not replay; verify with an authorized owner."
            : status === 412
              ? "Department version changed. Obtain a fresh read and review in a new flow."
              : status === 403
                ? "Department access denied by server."
                : status === 409
                  ? "Dependencies or department state prevent deactivation."
                  : "Department review or deactivation unavailable. No automatic retry."}
        </p>
      )}
      {status === 401 && <button onClick={recover}>Check session</button>}
    </main>
  );
}
