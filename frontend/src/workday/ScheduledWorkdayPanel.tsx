import { useEffect, useMemo, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import { createWorkdayApi } from "./workdayApi";
import { createScheduledWorkdayApi } from "./scheduledWorkdayApi";
import { scheduledMessage, strongWorkdayTag } from "./scheduledContracts";
import { safeId, type OwnShift, type OwnWorkday } from "./contracts";
const keys = new WeakMap<object, number>();
let generation = 0;
function key(x: object) {
  if (!keys.has(x)) keys.set(x, ++generation);
  return keys.get(x)!;
}
/** Standalone integration candidate. WorkdayRoute and production flags are unchanged. */
export function ScheduledWorkdayPanel({
  branchId,
  dispatchEnabled = false,
}: {
  branchId: number;
  dispatchEnabled?: boolean;
}) {
  const { state, actions } = useAuth();
  const identity = useMemo(
    () => ({}),
    [state.session, state.status, state.pending],
  );
  const [invalidIdentity, setInvalidIdentity] = useState<object | null>(null);
  const binding = useMemo(
    () => ({}),
    [state.session, state.status, state.pending, branchId, dispatchEnabled],
  );
  const latest = useRef(binding);
  latest.current = binding;
  const current = useMemo(() => () => latest.current === binding, [binding]);
  if (
    state.status !== "AUTHENTICATED" ||
    !state.session ||
    state.session.mustChangePassword ||
    state.pending !== null
  )
    return (
      <section>
        <p>Confirmed unrestricted session required.</p>
        <button onClick={() => void actions.revalidate()}>
          Revalidate session
        </button>
      </section>
    );
  if (invalidIdentity === identity)
    return (
      <section>
        <p role="alert">
          Session invalid. Revalidate before reading or changing workday.
        </p>
        <button onClick={() => void actions.revalidate()}>
          Revalidate session
        </button>
      </section>
    );
  if (!safeId(branchId)) return <p role="alert">Choose a valid branch ID.</p>;
  return (
    <Bound
      key={key(binding)}
      {...{ branchId, dispatchEnabled, current }}
      invalidate={() => setInvalidIdentity(identity)}
      revalidate={() => void actions.revalidate()}
    />
  );
}
type Review = {
  operation: "start" | "end";
  shift: OwnShift | undefined;
  day: OwnWorkday | null | undefined;
};
function Bound({
  branchId,
  dispatchEnabled,
  current,
  revalidate,
  invalidate,
}: {
  branchId: number;
  dispatchEnabled: boolean;
  current: () => boolean;
  revalidate: () => void;
  invalidate: () => void;
}) {
  const [read] = useState(() => createWorkdayApi()),
    [write] = useState(() =>
      createScheduledWorkdayApi(
        globalThis.fetch.bind(globalThis),
        dispatchEnabled,
      ),
    );
  const [shifts, setShifts] = useState<OwnShift[] | null>(null),
    [day, setDay] = useState<OwnWorkday | null | undefined>(undefined),
    [shiftError, setShiftError] = useState(""),
    [dayError, setDayError] = useState(""),
    [selection, setSelection] = useState(""),
    [refresh, setRefresh] = useState(0),
    [pending, setPending] = useState(false),
    [message, setMessage] = useState(""),
    [sessionInvalid, setSessionInvalid] = useState(false),
    [locked, setLocked] = useState(false);
  const [review, setReview] = useState<Review | null>(null);
  const reviewed = useRef<Review | null>(null);
  function clearReview() {
    reviewed.current = null;
    setReview(null);
  }
  const mounted = useRef(false),
    busy = useRef(false),
    uncertain = useRef(false),
    invalid = useRef(false),
    controller = useRef<AbortController | null>(null);
  function invalidateSession() {
    clearReview();
    invalid.current = true;
    invalidate();
    busy.current = true;
    controller.current?.abort();
    setSessionInvalid(true);
    setShifts(null);
    setDay(undefined);
    setSelection("");
    setMessage("");
    setPending(false);
  }
  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
      controller.current?.abort();
    };
  }, []);
  useEffect(() => {
    clearReview();
    const abort = new AbortController();
    let active = true;
    setShifts(null);
    setDay(undefined);
    setSelection("");
    setShiftError("");
    setDayError("");
    void read.ownShifts(branchId, abort.signal).then((r) => {
      if (!active || !current() || invalid.current) return;
      if (!r.ok && r.error === "unauthorized") {
        invalidateSession();
        return;
      }
      if (r.ok) setShifts(r.data);
      else setShiftError(`Assigned shifts unavailable: ${r.error}.`);
    });
    void read.current(branchId, abort.signal).then((r) => {
      if (!active || !current() || invalid.current) return;
      if (!r.ok && r.error === "unauthorized") {
        invalidateSession();
        return;
      }
      if (r.ok) {
        setDay(r.data);
        if (!uncertain.current) busy.current = false;
      } else setDayError(`Current workday unavailable: ${r.error}.`);
    });
    return () => {
      active = false;
      abort.abort();
    };
  }, [read, branchId, current, refresh]);
  const selected = shifts?.find(
    (s) => s.shiftAssignmentId === Number(selection),
  );
  const startAllowed =
    day === null && !!selected && selected.branchId === branchId;
  const endAllowed =
    !!day &&
    day.branchId === branchId &&
    !day.isUnscheduled &&
    safeId(day.shiftAssignmentId) &&
    day.openInterval === null &&
    strongWorkdayTag(`"${day.rowVersion}"`);
  function prepare(operation: "start" | "end") {
    if (
      !dispatchEnabled ||
      busy.current ||
      uncertain.current ||
      invalid.current ||
      !current() ||
      (operation === "start" ? !startAllowed : !endAllowed)
    )
      return;
    const next = { operation, shift: selected, day };
    reviewed.current = next;
    setReview(next);
    setMessage("");
  }
  async function confirm() {
    const snapshot = reviewed.current;
    if (!snapshot) return;
    const operation = snapshot.operation;
    if (
      !dispatchEnabled ||
      busy.current ||
      uncertain.current ||
      invalid.current ||
      !mounted.current ||
      !current() ||
      (operation === "start"
        ? !startAllowed || selected !== snapshot.shift || day !== snapshot.day
        : !endAllowed || day !== snapshot.day)
    )
      return;
    clearReview();
    busy.current = true;
    setPending(true);
    setMessage("");
    controller.current = new AbortController();
    const result =
      operation === "start"
        ? await write.start(
            branchId,
            snapshot.shift!.shiftAssignmentId,
            () => mounted.current && current() && !invalid.current,
            controller.current.signal,
          )
        : await write.end(
            branchId,
            snapshot.day!.workdayId,
            snapshot.day!.shiftAssignmentId!,
            `"${snapshot.day!.rowVersion}"`,
            () => mounted.current && current() && !invalid.current,
            controller.current.signal,
          );
    if (!mounted.current || !current() || invalid.current) return;
    if (result.outcome !== "CONFIRMED" && result.error.status === 401) {
      invalidateSession();
      return;
    }
    setPending(false);
    if (result.outcome === "CONFIRMED") {
      setDay(undefined);
      setMessage(
        `${operation === "start" ? "Start" : "End"} confirmed. Refreshing authoritative snapshot.`,
      );
      setRefresh((n) => n + 1);
    } else {
      setMessage(scheduledMessage(result));
      if (result.outcome === "OUTCOME_UNKNOWN") {
        uncertain.current = true;
        setLocked(true);
      }
    }
    // Keep guard until a fresh read renders, preventing a second click against the old snapshot.
    if (result.outcome !== "CONFIRMED") busy.current = false;
  }

  if (sessionInvalid)
    return (
      <section>
        <p role="alert">
          Session invalid. Revalidate before reading or changing workday.
        </p>
        <button onClick={revalidate}>Revalidate session</button>
      </section>
    );
  return (
    <main>
      <h1>Scheduled workday</h1>
      <p>
        Branch {branchId}. Server confirms access and assignment eligibility.
      </p>
      {!dispatchEnabled && (
        <p>Mutation dispatch disabled pending runtime approval.</p>
      )}
      <section aria-label="Assigned shifts">
        <h2>Assigned shifts</h2>
        {shiftError ? (
          <p role="alert">{shiftError}</p>
        ) : shifts === null ? (
          <p>Loading shifts...</p>
        ) : shifts.length === 0 ? (
          <p>No assigned shifts.</p>
        ) : (
          <label>
            Scheduled shift
            <select
              aria-label="Scheduled shift"
              value={selection}
              disabled={pending || locked}
              onChange={(e) => {
                clearReview();
                setSelection(e.target.value);
              }}
            >
              <option value="">Choose shift</option>
              {shifts.map((s) => (
                <option key={s.shiftAssignmentId} value={s.shiftAssignmentId}>
                  {s.shiftDate} — {s.scheduledStartAtUtc}
                </option>
              ))}
            </select>
          </label>
        )}
      </section>
      <section aria-label="Current workday">
        <h2>Current workday</h2>
        {dayError ? (
          <p role="alert">{dayError}</p>
        ) : day === undefined ? (
          <p>Loading current workday...</p>
        ) : day === null ? (
          <p>No current working day.</p>
        ) : (
          <>
            <p>Working day {day.workdayId}</p>
            {day.openInterval && (
              <p role="alert">
                End blocked: an interval is open. Close it through its owning
                workflow. Pause All is not supported.
              </p>
            )}
            {day.isUnscheduled && (
              <p>Unscheduled workday is outside this slice.</p>
            )}
          </>
        )}
      </section>
      <button
        disabled={!dispatchEnabled || !startAllowed || pending || locked}
        onClick={() => prepare("start")}
      >
        Start scheduled workday
      </button>
      <button
        disabled={!dispatchEnabled || !endAllowed || pending || locked}
        onClick={() => prepare("end")}
      >
        End workday
      </button>
      {review && (
        <section aria-label="Review scheduled workday">
          <h2>
            {review.operation === "start" ? "Review start" : "Review end"}
          </h2>
          <p>
            Branch {branchId}; Department{" "}
            {review.operation === "start"
              ? review.shift!.departmentId
              : review.day!.departmentId}
          </p>
          {review.operation === "start" ? (
            <p>
              Assignment {review.shift!.shiftAssignmentId}; Date{" "}
              {review.shift!.shiftDate}; Scheduled{" "}
              {review.shift!.scheduledStartAtUtc} to{" "}
              {review.shift!.scheduledEndAtUtc}
            </p>
          ) : (
            <p>
              Workday {review.day!.workdayId}; Date {review.day!.workDate};
              Started {review.day!.startedAtUtc}
            </p>
          )}
          <p>
            Actual time is determined by the server. This does not reassign work
            or close task intervals.
          </p>
          <button
            disabled={pending || locked || !dispatchEnabled}
            onClick={() => void confirm()}
          >
            {review.operation === "start"
              ? "Confirm scheduled start"
              : "Confirm workday end"}
          </button>
          <button disabled={pending} onClick={clearReview}>
            Cancel review
          </button>
        </section>
      )}
      {pending && <p role="status">Submitting once...</p>}
      {message && <p role="status">{message}</p>}
      <button
        disabled={pending}
        onClick={() => {
          clearReview();
          setRefresh((n) => n + 1);
        }}
      >
        Refresh snapshot
      </button>
      <button disabled={pending} onClick={revalidate}>
        Revalidate session
      </button>
      {locked && <p>Refresh does not unlock replay of an unknown mutation.</p>}
    </main>
  );
}
