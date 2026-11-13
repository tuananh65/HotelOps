import { useLayoutEffect, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import type { PublicSession } from "../auth/authStore";
import { createHotelApi } from "./hotelApi";
import type {
  Context,
  Filters,
  HotelError,
  MutationKind,
  MutationRequests,
  ReadKind,
  ReadTypes,
} from "./contracts";
export interface HotelSnapshot {
  owner: PublicSession;
  key: string;
  revision: number;
  data: ReadTypes[ReadKind];
  etag: string | null;
}
export function hotelErrorMessage(error: HotelError) {
  if (error.reason === "PASSWORD_CHANGE_REQUIRED")
    return "Your session must be checked before continuing.";
  if (error.reason === "ACTIVE_ROOMS_EXIST")
    return (
      "This room type still has active rooms. Resolve the dependency before deactivation." +
      (error.activeRooms?.length
        ? " Affected rooms: " +
          error.activeRooms
            .map((v) => `${v.roomNumber} (#${v.id})`)
            .join(", ") +
          "."
        : "")
    );
  if (error.reason === "ROOM_TYPE_SCHEMA_REQUIRED")
    return "The backend room-type schema prerequisite is not available. Contact the integration owner.";
  switch (error.status) {
    case 400:
      return "The server rejected the input or security check. Review the fields before a new attempt.";
    case 401:
      return "Your session needs to be checked. No request has been replayed.";
    case 403:
      return "Access denied. This response does not confirm whether the requested record exists.";
    case 404:
      return "The requested record could not be found.";
    case 409:
      return "The server rejected a duplicate, inactive record or dependency conflict. No change has been confirmed.";
    case 412:
    case 428:
      return "The version must be refreshed. Load the record again, review the current values, then confirm a new attempt.";
    case 503:
      return "The service is unavailable. No demo data has been substituted.";
  }
  return error.kind === "invalid-request"
    ? "Check the branch, record ID and required fields."
    : "The response could not be confirmed safely. No raw server error is displayed.";
}
export const unknownHotelOutcome =
  "The change could not be confirmed. It may already have been saved. Do not retry automatically. Ask an authorized administrator to verify the record before making another change.";
export function useHotelFlow(key: string) {
  const { state, actions } = useAuth();
  const owner = state.status === "AUTHENTICATED" ? state.session : null;
  const current = useRef({ owner, key });
  current.current = { owner, key };
  const prior = useRef({ owner, key });
  const [api] = useState(() => createHotelApi());
  const [snapshot, setSnapshot] = useState<HotelSnapshot | null>(null);
  const [phase, setPhase] = useState<
    | "idle"
    | "reading"
    | "pending"
    | "confirmed"
    | "unknown"
    | "stale"
    | "denied"
  >("idle");
  const phaseRef = useRef(phase);
  const [message, setMessage] = useState("");
  const [receipt, setReceipt] = useState<unknown>(null);
  const mounted = useRef(false),
    busy = useRef(false),
    recovered = useRef(false),
    unknown = useRef(false);
  const generation = useRef(0),
    revision = useRef(0);
  const readAbort = useRef<AbortController | null>(null),
    writeAbort = useRef<AbortController | null>(null);
  const usable = useRef<HotelSnapshot | null>(null);
  function publish(next: typeof phase) {
    phaseRef.current = next;
    setPhase(next);
  }
  useLayoutEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
      ++generation.current;
      readAbort.current?.abort();
      writeAbort.current?.abort();
    };
  }, []);
  useLayoutEffect(() => {
    if (prior.current.owner === owner && prior.current.key === key) return;
    if (busy.current) unknown.current = true;
    ++generation.current;
    readAbort.current?.abort();
    writeAbort.current?.abort();
    busy.current = false;
    usable.current = null;
    setSnapshot(null);
    setReceipt(null);
    setMessage("");
    recovered.current = false;
    publish(unknown.current ? "unknown" : "idle");
    prior.current = { owner, key };
  }, [owner, key]);
  const visible =
    owner && snapshot?.owner === owner && snapshot.key === key
      ? snapshot
      : null;
  usable.current = visible;
  const valid = (n: number) =>
    mounted.current &&
    n === generation.current &&
    current.current.owner === owner &&
    current.current.key === key;
  function recover(e: HotelError) {
    if (
      (e.status === 401 || e.reason === "PASSWORD_CHANGE_REQUIRED") &&
      !recovered.current
    ) {
      recovered.current = true;
      void actions.revalidate();
    }
  }
  async function load(kind: ReadKind, context: Context, filters: Filters) {
    if (!owner || busy.current || phaseRef.current === "confirmed") return;
    readAbort.current?.abort();
    const abort = new AbortController(),
      n = ++generation.current;
    readAbort.current = abort;
    usable.current = null;
    setSnapshot(null);
    setMessage("");
    publish("reading");
    const result = await api.read(kind, context, filters, abort.signal);
    if (!valid(n)) return;
    if (result.ok) {
      setSnapshot({
        owner,
        key,
        revision: ++revision.current,
        data: result.data,
        etag: result.etag,
      });
      publish(unknown.current ? "unknown" : "idle");
    } else {
      setMessage(hotelErrorMessage(result.error));
      publish(
        unknown.current
          ? "unknown"
          : result.error.status === 403
            ? "denied"
            : "idle",
      );
      recover(result.error);
    }
  }
  async function save<K extends MutationKind>(
    kind: K,
    context: Context,
    request: MutationRequests[K],
    reviewed: HotelSnapshot | null,
  ) {
    const needsSnapshot = !kind.startsWith("create");
    if (
      !mounted.current ||
      !owner ||
      current.current.owner !== owner ||
      current.current.key !== key ||
      busy.current ||
      unknown.current ||
      ["confirmed", "stale", "denied"].includes(phaseRef.current) ||
      (needsSnapshot &&
        (!reviewed ||
          usable.current !== reviewed ||
          reviewed.owner !== owner ||
          reviewed.key !== key ||
          !reviewed.etag))
    )
      return;
    readAbort.current?.abort();
    const abort = new AbortController(),
      n = ++generation.current;
    writeAbort.current = abort;
    busy.current = true;
    publish("pending");
    setMessage("");
    const result = await api.mutate(
      kind,
      context,
      request,
      reviewed?.etag,
      abort.signal,
    );
    if (!valid(n)) return;
    busy.current = false;
    writeAbort.current = null;
    if (result.outcome === "CONFIRMED") {
      usable.current = null;
      setSnapshot(null);
      setReceipt(result.data);
      publish("confirmed");
    } else if (result.outcome === "OUTCOME_UNKNOWN") {
      unknown.current = true;
      usable.current = null;
      setSnapshot(null);
      publish("unknown");
      recover(result.error);
    } else {
      const e = result.error;
      if ([401, 403, 404, 412, 428].includes(e.status ?? 0)) {
        usable.current = null;
        setSnapshot(null);
      }
      setMessage(
        (result.outcome === "NOT_DISPATCHED" ? "No mutation was sent. " : "") +
          hotelErrorMessage(e),
      );
      publish(
        e.status === 403
          ? "denied"
          : e.status === 412 || e.status === 428
            ? "stale"
            : "idle",
      );
      recover(e);
    }
  }
  return {
    snapshot: visible,
    phase,
    message,
    receipt: owner ? receipt : null,
    load,
    save,
    unknown: unknown.current,
  };
}
