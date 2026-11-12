import { useLayoutEffect, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import { createCreateUserApi } from "./createUserApi";
import type {
  CreateUserFailure,
  CreateUserRequest,
  CreateUserResponse,
} from "./createUserContracts";

export const unknownCreationMessage =
  "Account creation outcome could not be confirmed. The account may already exist. Do not submit this request again. Ask an authorized administrator to verify the account and arrange a credential reset if needed.";

type Progress =
  | { phase: "idle" | "pending" | "created" | "unknown" }
  | {
      phase: "not-dispatched" | "rejected";
      error: CreateUserFailure;
    };

/** Only nonsecret progress survives provider fallback. Successful secrets go directly to the mounted handoff. */
export function useCreateUser() {
  const { state, actions } = useAuth();
  const owner = state.status === "AUTHENTICATED" ? state.session : null;
  const currentOwner = useRef(owner);
  currentOwner.current = owner; // Mask stale callbacks before effect cleanup.
  const [api] = useState(() => createCreateUserApi());
  const [progress, setProgress] = useState<Progress>({ phase: "idle" });
  const phase = useRef<Progress["phase"]>("idle");
  const busy = useRef(false);
  const mounted = useRef(false);
  const generation = useRef(0);
  const controller = useRef<AbortController | null>(null);
  const attemptOwner = useRef(owner);

  function publish(next: Progress) {
    phase.current = next.phase;
    setProgress(next);
  }

  useLayoutEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
      ++generation.current;
      controller.current?.abort();
    };
  }, []);

  useLayoutEffect(() => {
    if (controller.current && attemptOwner.current !== owner) {
      ++generation.current;
      controller.current.abort();
      controller.current = null;
      busy.current = false;
      // The API may already have sent POST. Aborting does not prove rollback.
      if (phase.current === "pending") publish({ phase: "unknown" });
    }
  }, [owner]);

  async function submit(
    input: CreateUserRequest,
    receive: (data: CreateUserResponse) => void,
  ) {
    if (
      !mounted.current ||
      !owner ||
      currentOwner.current !== owner ||
      busy.current ||
      ["unknown", "created"].includes(phase.current)
    )
      return;
    busy.current = true; // Synchronous, including two events before React renders.
    const attempt = ++generation.current;
    const abort = new AbortController();
    controller.current = abort;
    attemptOwner.current = owner;
    publish({ phase: "pending" });
    try {
      const result = await api.createUser(input, abort.signal);
      if (
        !mounted.current ||
        attempt !== generation.current ||
        currentOwner.current !== owner ||
        abort.signal.aborted
      )
        return;
      switch (result.outcome) {
        case "CONFIRMED_CREATED":
          receive(result.data);
          publish({ phase: "created" });
          break;
        case "OUTCOME_UNKNOWN":
          publish({ phase: "unknown" });
          // A received401 invalidates session authority even if its body was unreadable.
          // Keep the ambiguous creation locked; session recovery never replays POST.
          if (result.error.status === 401) void actions.revalidate();
          break;
        case "NOT_DISPATCHED":
          publish({ phase: "not-dispatched", error: result.error });
          if (result.error.status === 401) void actions.revalidate();
          break;
        case "REJECTED":
          publish({ phase: "rejected", error: result.error });
          if (
            result.error.status === 401 ||
            result.error.reason === "PASSWORD_CHANGE_REQUIRED"
          )
            void actions.revalidate();
          break;
      }
    } catch {
      if (
        mounted.current &&
        attempt === generation.current &&
        currentOwner.current === owner
      )
        publish({ phase: "unknown" });
    } finally {
      if (attempt === generation.current) {
        busy.current = false;
        controller.current = null;
      }
    }
  }
  return { progress, submit };
}

export function createUserFailureMessage(error: CreateUserFailure): string {
  switch (error.reason) {
    case "INVALID_REQUEST":
    case "VALIDATION_FAILED":
      return "Check the account fields and the branch, department and role relationship.";
    case "CSRF_VALIDATION_FAILED":
      return "The security check was rejected. No request has been retried automatically.";
    case "SESSION_INVALID":
      return "Your session must be confirmed before creating an account.";
    case "FORBIDDEN":
      return "Access denied. Creating an account requires server authorization.";
    case "PASSWORD_CHANGE_REQUIRED":
      return "Change your password before creating an account.";
    case "DUPLICATE":
      return error.duplicateFields?.length
        ? `Already in use: ${error.duplicateFields.join(", ")}.`
        : "An employee code, username or email is already in use.";
    case "LAST_DURABLE_ADMIN":
      return "This account would prevent safe administrator access. Contact an authorized administrator.";
    case "UNSUPPORTED_MEDIA_TYPE":
      return "The server rejected the request format.";
    case "CANCELLED":
      return "The attempt was cancelled before account creation was sent.";
    default:
      return "The attempt could not start account creation. No account creation request was sent.";
  }
}
