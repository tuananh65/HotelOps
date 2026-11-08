import { useEffect, useLayoutEffect, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import type { PublicSession } from "../auth/authStore";
import { createUserProfileApi } from "./userProfileApi";
import { createUserBranchMembershipApi } from "./userBranchMembershipApi";
import type { UserProfileDto } from "./userProfileContracts";
import type { UserBranchMembershipDto } from "./userBranchMembershipContracts";

export const unknownMembershipMessage =
  "Branch membership addition outcome could not be confirmed. The membership may already have been created. Do not retry automatically. Ask an authorized administrator to verify the current membership before making another change.";
export function destinationBranchId(value: string): number | null {
  const number =
    value.length > 0 && !/[^0-9]/.test(value) ? Number(value) : NaN;
  return Number.isSafeInteger(number) && number > 0 ? number : null;
}
type Snapshot = { profile: UserProfileDto; etag: string; revision: number };
type Read = { owner: PublicSession; target: number; generation: number } & (
  | { kind: "loading" }
  | { kind: "error"; message: string }
  | { kind: "ready"; snapshot: Snapshot }
);
type Progress = {
  phase:
    | "idle"
    | "pending"
    | "confirmed"
    | "unknown"
    | "stale"
    | "rejected"
    | "not-dispatched";
  message?: string;
  membership?: UserBranchMembershipDto;
};

export function useAddUserBranch(target: number | null) {
  const { state, actions } = useAuth();
  const owner = state.status === "AUTHENTICATED" ? state.session : null;
  const current = useRef({ owner, target });
  current.current = { owner, target };
  const previous = useRef({ owner, target });
  const [profileApi] = useState(() => createUserProfileApi());
  const [membershipApi] = useState(() => createUserBranchMembershipApi());
  const [attempt, setAttempt] = useState(0);
  const [read, setRead] = useState<Read | null>(null);
  const [progress, setProgress] = useState<Progress>({ phase: "idle" });
  const phase = useRef<Progress["phase"]>("idle");
  const mounted = useRef(false),
    busy = useRef(false),
    paused = useRef(false),
    recovered = useRef(false);
  const readGeneration = useRef(0),
    mutationGeneration = useRef(0),
    revision = useRef(0);
  const controller = useRef<AbortController | null>(null);
  const usable = useRef<Snapshot | null>(null);
  function publish(next: Progress) {
    phase.current = next.phase;
    setProgress(next);
  }
  function recover(status?: number, reason?: string) {
    if (
      (status === 401 || reason === "PASSWORD_CHANGE_REQUIRED") &&
      !recovered.current
    ) {
      recovered.current = true;
      paused.current = true;
      void actions.revalidate(); // Bounded GET recovery; never replay a mutation.
    }
  }
  useLayoutEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
      ++mutationGeneration.current;
      controller.current?.abort();
    };
  }, []);
  useLayoutEffect(() => {
    if (previous.current.owner === owner && previous.current.target === target)
      return;
    const changedTarget = previous.current.target !== target;
    const pending = phase.current === "pending";
    ++mutationGeneration.current;
    controller.current?.abort();
    controller.current = null;
    busy.current = false;
    usable.current = null;
    setRead(null);
    if (changedTarget) {
      paused.current = false;
      recovered.current = false;
      publish({ phase: "idle" });
    } else if (pending) {
      paused.current = true;
      publish({ phase: "unknown" });
    } else if (phase.current === "confirmed") {
      paused.current = true;
      publish({ phase: "idle" });
    } else if (!["unknown", "rejected", "stale"].includes(phase.current))
      publish({ phase: "idle" });
    previous.current = { owner, target };
  }, [owner, target]);
  useEffect(() => {
    const generation = ++readGeneration.current,
      abort = new AbortController();
    if (!owner || target === null || paused.current) return () => abort.abort();
    setRead({ owner, target, generation, kind: "loading" });
    void Promise.resolve().then(async () => {
      if (abort.signal.aborted || generation !== readGeneration.current) return;
      const result = await profileApi.getProfile(target, abort.signal);
      if (
        !mounted.current ||
        abort.signal.aborted ||
        generation !== readGeneration.current ||
        current.current.owner !== owner ||
        current.current.target !== target
      )
        return;
      if (result.ok)
        setRead({
          owner,
          target,
          generation,
          kind: "ready",
          snapshot: {
            profile: result.data,
            etag: result.etag,
            revision: ++revision.current,
          },
        });
      else {
        setRead({
          owner,
          target,
          generation,
          kind: "error",
          message: failureMessage(result.error.status, result.error.reason),
        });
        recover(result.error.status, result.error.reason);
      }
    });
    return () => {
      ++readGeneration.current;
      abort.abort();
    };
  }, [owner, target, attempt, profileApi]);
  const visible =
    owner &&
    read?.owner === owner &&
    read.target === target &&
    read.generation === readGeneration.current
      ? read
      : null;
  const snapshot = visible?.kind === "ready" ? visible.snapshot : null;
  usable.current = snapshot;
  function reload() {
    if (!owner || target === null || busy.current) return;
    ++readGeneration.current;
    usable.current = null;
    setRead(null);
    paused.current = false;
    recovered.current = false;
    if (!["unknown", "confirmed"].includes(phase.current))
      publish({ phase: "idle" });
    setAttempt((n) => n + 1);
  }
  async function add(branchId: number) {
    if (
      !mounted.current ||
      !owner ||
      target === null ||
      !snapshot ||
      usable.current !== snapshot ||
      current.current.owner !== owner ||
      current.current.target !== target ||
      busy.current ||
      paused.current ||
      ["unknown", "stale", "confirmed"].includes(phase.current) ||
      !Number.isSafeInteger(branchId) ||
      branchId <= 0
    )
      return;
    busy.current = true;
    const generation = ++mutationGeneration.current,
      abort = new AbortController();
    controller.current = abort;
    publish({ phase: "pending" });
    try {
      const result = await membershipApi.addBranchMembership(
        target,
        snapshot.etag,
        branchId,
        abort.signal,
      );
      if (
        !mounted.current ||
        generation !== mutationGeneration.current ||
        current.current.owner !== owner ||
        current.current.target !== target
      )
        return;
      if (result.outcome === "CONFIRMED_ADDED") {
        usable.current = null;
        setRead(null);
        paused.current = true;
        publish({ phase: "confirmed", membership: result.data });
      } else if (result.outcome === "OUTCOME_UNKNOWN") {
        usable.current = null;
        setRead(null);
        paused.current = true;
        publish({ phase: "unknown" });
      } else {
        const e = result.error;
        if ([401, 403, 404, 412, 428].includes(e.status ?? 0)) {
          usable.current = null;
          setRead(null);
          paused.current = true;
        }
        publish({
          phase:
            e.status === 412 || e.status === 428
              ? "stale"
              : result.outcome === "NOT_DISPATCHED"
                ? "not-dispatched"
                : "rejected",
          message: failureMessage(e.status, e.reason),
        });
        recover(e.status, e.reason);
      }
    } catch {
      if (
        mounted.current &&
        generation === mutationGeneration.current &&
        current.current.owner === owner &&
        current.current.target === target
      ) {
        usable.current = null;
        setRead(null);
        paused.current = true;
        publish({ phase: "unknown" });
      }
    } finally {
      if (generation === mutationGeneration.current) {
        busy.current = false;
        controller.current = null;
      }
    }
  }
  return {
    snapshot,
    progress,
    loading: visible?.kind === "loading",
    error: visible?.kind === "error" ? visible.message : null,
    reload,
    add,
  };
}

function failureMessage(status?: number, reason?: string): string {
  if (status === 403)
    return "Access denied. This response does not confirm whether the account exists.";
  if (status === 401)
    return "Your session must be checked. Reload profile after session recovery. No membership request has been retried.";
  if (status === 404) return "The server could not find this user account.";
  if (status === 412 || status === 428)
    return "The version must be refreshed. Reload profile, review the target and destination again, then confirm a new attempt.";
  switch (reason) {
    case "TARGET_NOT_ACTIVE":
      return "The server rejected the request because the target account is not active.";
    case "MEMBERSHIP_EXISTS":
      return "A membership already exists, possibly a previously revoked membership. This flow cannot regrant it.";
    case "EXISTING_ROLE_PATH_CONFLICT":
      return "An existing destination role path prevents adding this membership. This flow cannot repair roles.";
    case "OPERATIONS_MANAGER_SCOPE_CONFLICT":
      return "The Operations Manager scope constraint prevents adding this membership.";
    case "BRANCH_UNAVAILABLE":
      return "The destination branch is unavailable. Check the Branch ID.";
    case "CSRF_VALIDATION_FAILED":
      return "The security check was rejected. No membership request has been retried.";
    case "HTTPS_REQUIRED":
      return "A secure HTTPS connection is required.";
    case "REQUEST_TOO_LARGE":
      return "The request exceeds the permitted size.";
  }
  if (status === 415)
    return "The request format was rejected. No membership request has been retried.";
  if (status === 400)
    return "Check the destination and request version. No membership request has been retried.";
  return "The request could not be completed safely. No membership request has been retried.";
}
