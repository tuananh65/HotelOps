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
  if (!safeId(branchId)) return <p role="alert">Choose a valid branch ID.</p>;
  return (
    <Bound
      key={key(binding)}
      {...{ branchId, dispatchEnabled, current }}
      revalidate={() => void actions.revalidate()}
    />
  );
}
function Bound({
  branchId,
  dispatchEnabled,
  current,
  revalidate,
}: {
  branchId: number;
  dispatchEnabled: boolean;
  current: () => boolean;
  revalidate: () => void;
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
    [locked, setLocked] = useState(false);
  const mounted = useRef(false),
    busy = useRef(false),
    uncertain = useRef(false),
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
    setShifts(null);
    setDay(undefined);
    setSelection("");
    setShiftError("");
    setDayError("");
    void read.ownShifts(branchId, abort.signal).then((r) => {
      if (!active || !current()) return;
      if (r.ok) setShifts(r.data);
      else setShiftError(`Assigned shifts unavailable: ${r.error}.`);
    });
    void read.current(branchId, abort.signal).then((r) => {
      if (!active || !current()) return;
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
  async function mutate(operation: "start" | "end") {
    if (
      !dispatchEnabled ||
      busy.current ||
      uncertain.current ||
      !mounted.current ||
      !current() ||
      (operation === "start" ? !startAllowed : !endAllowed)
    )
      return;
    busy.current = true;
    setPending(true);
    setMessage("");
    controller.current = new AbortController();
    const result =
      operation === "start"
        ? await write.start(
            branchId,
            selected!.shiftAssignmentId,
            () => mounted.current && current(),
            controller.current.signal,
          )
        : await write.end(
            branchId,
            day!.workdayId,
            day!.shiftAssignmentId!,
            `"${day!.rowVersion}"`,
            () => mounted.current && current(),
            controller.current.signal,
          );
    if (!mounted.current || !current()) return;
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
              onChange={(e) => setSelection(e.target.value)}
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
        onClick={() => void mutate("start")}
      >
        Start scheduled workday
      </button>
      <button
        disabled={!dispatchEnabled || !endAllowed || pending || locked}
        onClick={() => void mutate("end")}
      >
        End workday
      </button>
      {pending && <p role="status">Submitting once...</p>}
      {message && <p role="status">{message}</p>}
      <button disabled={pending} onClick={() => setRefresh((n) => n + 1)}>
        Refresh snapshot
      </button>
      <button disabled={pending} onClick={revalidate}>
        Revalidate session
      </button>
      {locked && <p>Refresh does not unlock replay of an unknown mutation.</p>}
    </main>
  );
}
