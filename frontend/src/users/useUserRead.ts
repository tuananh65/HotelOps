import { useEffect, useRef, useState } from "react";
import type { PublicSession } from "../auth/authStore";
import type { UsersApiError, UsersApiResult } from "./contracts";
import { useUsersContext } from "./UsersRouteBoundary";

interface CompletedRead<T> {
  readonly owner: PublicSession;
  readonly key: string;
  readonly attempt: number;
  readonly result: UsersApiResult<T>;
}

export function useUserRead<T>(
  key: string,
  load: (signal: AbortSignal) => Promise<UsersApiResult<T>>,
) {
  const { session, sessionFailure } = useUsersContext();
  const generation = useRef(0);
  const [attempt, setAttempt] = useState(0);
  const [completed, setCompleted] = useState<CompletedRead<T> | null>(null);
  useEffect(() => {
    const current = ++generation.current;
    const controller = new AbortController();
    setCompleted(null);
    // StrictMode's discarded setup is cancelled before it can issue a GET.
    void Promise.resolve().then(async () => {
      if (controller.signal.aborted || current !== generation.current) return;
      let result: UsersApiResult<T>;
      try {
        result = await load(controller.signal);
      } catch {
        result = { ok: false, error: { kind: "network" } };
      }
      if (controller.signal.aborted || current !== generation.current) return;
      if (!result.ok) sessionFailure(result.error);
      setCompleted({ owner: session, key, attempt, result });
    });
    return () => {
      ++generation.current;
      controller.abort();
    };
  }, [session, key, attempt, load, sessionFailure]);

  // Mask old data during render, before effect cleanup, including same-user identity replacement.
  const result =
    completed?.owner === session &&
    completed.key === key &&
    completed.attempt === attempt
      ? completed.result
      : null;
  return { result, reload: () => setAttempt((value) => value + 1) };
}

export function usersReadMessage(error: UsersApiError): string {
  if (error.kind === "invalid-request")
    return "Please check the filters and numeric IDs.";
  if (error.kind === "cancelled")
    return "The read was cancelled. You can try again.";
  if (error.kind === "invalid-response")
    return "The server response could not be validated.";
  if (error.kind === "http") {
    if (error.status === 400) return "Please check the requested filters.";
    if (error.status === 401)
      return "Your session must be checked before loading account data.";
    if (error.status === 403)
      return "Access denied. This response does not confirm whether the account exists.";
    if (error.status === 404) return "The requested account was not found.";
  }
  return "Account data is currently unavailable. You can try again.";
}
