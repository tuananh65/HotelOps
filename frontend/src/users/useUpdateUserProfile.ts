import { useEffect, useLayoutEffect, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import type { PublicSession } from "../auth/authStore";
import { createUserProfileApi } from "./userProfileApi";
import type {
  UserProfileDto,
  UserProfileError,
  UserProfilePatch,
} from "./userProfileContracts";

export const unknownProfileMessage =
  "Profile update outcome could not be confirmed. Changes may already have been saved. Do not retry this update automatically. Ask an authorized administrator to verify the current profile before making another change.";
type Snapshot = { profile: UserProfileDto; etag: string; revision: number };
type Read = { owner: PublicSession; target: number; attempt: number } & (
  | { kind: "loading" }
  | { kind: "error"; error: UserProfileError }
  | { kind: "ready"; snapshot: Snapshot }
);
type Progress = {
  phase:
    | "idle"
    | "saving"
    | "updated"
    | "stale"
    | "unknown"
    | "rejected"
    | "not-dispatched";
  error?: UserProfileError;
};

export function useUpdateUserProfile(target: number | null) {
  const { state, actions } = useAuth();
  const owner = state.status === "AUTHENTICATED" ? state.session : null;
  const current = useRef({ owner, target });
  current.current = { owner, target }; // Reject obsolete callbacks before effect cleanup.
  const [api] = useState(() => createUserProfileApi());
  const [attempt, setAttempt] = useState(0);
  const [read, setRead] = useState<Read | null>(null);
  const [progress, setProgress] = useState<Progress>({ phase: "idle" });
  const phase = useRef<Progress["phase"]>("idle");
  const mounted = useRef(false),
    busy = useRef(false),
    paused = useRef(false),
    recovery = useRef(false);
  const readGeneration = useRef(0),
    mutationGeneration = useRef(0),
    revision = useRef(0);
  const mutationController = useRef<AbortController | null>(null);
  const usableSnapshot = useRef<Snapshot | null>(null);
  const previous = useRef({ owner, target });

  function publish(next: Progress) {
    phase.current = next.phase;
    setProgress(next);
  }
  function recover(error: UserProfileError) {
    if (error.status !== 401 && error.reason !== "PASSWORD_CHANGE_REQUIRED")
      return;
    paused.current = true;
    if (recovery.current) return;
    recovery.current = true;
    void actions.revalidate(); // One session GET per explicit profile-read cycle; never replay PATCH.
  }

  useLayoutEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
      ++mutationGeneration.current;
      mutationController.current?.abort();
    };
  }, []);

  useLayoutEffect(() => {
    if (
      previous.current.owner !== owner ||
      previous.current.target !== target
    ) {
      const targetChanged = previous.current.target !== target;
      const wasSaving = phase.current === "saving";
      ++mutationGeneration.current;
      mutationController.current?.abort();
      mutationController.current = null;
      busy.current = false;
      setRead(null);
      if (targetChanged) {
        paused.current = false;
        recovery.current = false;
        publish({ phase: "idle" });
      } else if (wasSaving) {
        paused.current = true;
        publish({ phase: "unknown" });
      } else if (!["unknown", "rejected"].includes(phase.current)) {
        publish({ phase: "idle" });
      }
      previous.current = { owner, target };
    }
  }, [owner, target]);

  useEffect(() => {
    const generation = ++readGeneration.current;
    const controller = new AbortController();
    if (!owner || target === null || paused.current)
      return () => controller.abort();
    const identity = { owner, target, attempt };
    setRead({ ...identity, kind: "loading" });
    void Promise.resolve().then(async () => {
      if (controller.signal.aborted || generation !== readGeneration.current)
        return;
      const result = await api.getProfile(target, controller.signal);
      if (
        !mounted.current ||
        controller.signal.aborted ||
        generation !== readGeneration.current ||
        current.current.owner !== owner ||
        current.current.target !== target
      )
        return;
      if (result.ok) {
        if (phase.current === "unknown") paused.current = true;
        setRead({
          ...identity,
          kind: "ready",
          snapshot: {
            profile: result.data,
            etag: result.etag,
            revision: ++revision.current,
          },
        });
      } else {
        setRead({ ...identity, kind: "error", error: result.error });
        recover(result.error);
      }
    });
    return () => {
      ++readGeneration.current;
      controller.abort();
    };
  }, [api, owner, target, attempt]);

  const visible =
    owner &&
    read?.owner === owner &&
    read.target === target &&
    read.attempt === attempt
      ? read
      : null;
  const snapshot = visible?.kind === "ready" ? visible.snapshot : null;
  usableSnapshot.current = snapshot;

  function reload() {
    if (!owner || target === null || busy.current) return;
    usableSnapshot.current = null;
    ++readGeneration.current;
    paused.current = false;
    recovery.current = false;
    // Reconciliation reads do not certify an earlier unknown mutation or unlock Save.
    if (phase.current !== "unknown") publish({ phase: "idle" });
    setRead(null);
    setAttempt((value) => value + 1);
  }

  async function save(patch: UserProfilePatch) {
    if (
      !mounted.current ||
      !owner ||
      target === null ||
      current.current.owner !== owner ||
      current.current.target !== target ||
      !snapshot ||
      usableSnapshot.current !== snapshot ||
      busy.current ||
      paused.current ||
      ["stale", "unknown"].includes(phase.current)
    )
      return;
    busy.current = true;
    const generation = ++mutationGeneration.current;
    const controller = new AbortController();
    mutationController.current = controller;
    publish({ phase: "saving" });
    try {
      const result = await api.updateProfile(
        target,
        snapshot.etag,
        patch,
        controller.signal,
      );
      if (
        !mounted.current ||
        controller.signal.aborted ||
        generation !== mutationGeneration.current ||
        current.current.owner !== owner ||
        current.current.target !== target
      )
        return;
      if (result.outcome === "CONFIRMED_UPDATED") {
        usableSnapshot.current = null;
        setRead({
          owner,
          target,
          attempt,
          kind: "ready",
          snapshot: {
            profile: result.data,
            etag: result.etag,
            revision: ++revision.current,
          },
        });
        publish({ phase: "updated" });
      } else if (result.outcome === "OUTCOME_UNKNOWN") {
        setRead(null);
        paused.current = true;
        publish({ phase: "unknown" });
      } else {
        const error = result.error;
        if ([401, 403, 404, 412, 428].includes(error.status ?? 0)) {
          setRead(null);
          paused.current = true;
        }
        publish({
          phase:
            error.status === 412 || error.status === 428
              ? "stale"
              : result.outcome === "REJECTED"
                ? "rejected"
                : "not-dispatched",
          error,
        });
        recover(error);
      }
    } catch {
      if (
        mounted.current &&
        generation === mutationGeneration.current &&
        current.current.owner === owner &&
        current.current.target === target
      ) {
        setRead(null);
        paused.current = true;
        publish({ phase: "unknown" });
      }
    } finally {
      if (generation === mutationGeneration.current) {
        busy.current = false;
        mutationController.current = null;
      }
    }
  }
  return {
    snapshot,
    progress,
    error: visible?.kind === "error" ? visible.error : null,
    loading: visible?.kind === "loading",
    paused: paused.current,
    reload,
    save,
  };
}

export function profileFailureMessage(error: UserProfileError): string {
  switch (error.reason) {
    case "FORBIDDEN":
      return "Access denied. This response does not confirm whether the account exists.";
    case "NOT_FOUND":
      return "The server could not find this user account.";
    case "SESSION_INVALID":
      return "Your session must be checked. Reload the profile after session recovery.";
    case "PASSWORD_CHANGE_REQUIRED":
      return "Change your password before updating a user profile.";
    case "EMAIL_IN_USE":
      return "Email is already in use. Choose another email.";
    case "LAST_DURABLE_ADMIN":
      return "This change would prevent safe administrator access. Contact an authorized administrator.";
    case "STALE_VERSION":
    case "IF_MATCH_REQUIRED":
      return "The profile version must be refreshed. Click Reload profile, review the current data and decide whether to save a new change.";
    case "CSRF_VALIDATION_FAILED":
      return "The security check was rejected. No update has been retried.";
    case "VALIDATION_FAILED":
    case "INVALID_REQUEST":
      return "Check the full name, email and phone fields. The server validates email format.";
    default:
      return "The profile request could not be completed safely. No update has been retried.";
  }
}
