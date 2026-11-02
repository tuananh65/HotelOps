import { useEffect, useReducer, useState } from "react";
import { currentSession, subscribe } from "../../services";
import { ApiError } from "../../api/http";

export type Dev1QueryState<T> =
  | { status: "loading"; data: null; error: null }
  | { status: "error"; data: null; error: Error }
  | { status: "ready" | "empty"; data: T; error: null };
/** Memoize load/isEmpty with useCallback. Include branch/resource in key.
 * No permission or mock data is inferred; mutations remain explicit controller calls. */
export function useDev1QueryController<T>(
  key: string,
  load: (signal: AbortSignal) => Promise<T>,
  isEmpty: (value: T) => boolean,
) {
  const [revision, reload] = useReducer((n: number) => n + 1, 0);
  const [result, setResult] = useState<{
    key: string;
    value: Dev1QueryState<T>;
  }>({
    key,
    value: { status: "loading", data: null, error: null },
  });
  useEffect(() => {
    const controller = new AbortController();
    let active = true;
    const original = currentSession();
    const fingerprint = JSON.stringify(original);
    const unsubscribe = subscribe(() => {
      if (JSON.stringify(currentSession()) !== fingerprint) {
        active = false;
        controller.abort();
        setResult({
          key,
          value: {
            status: "error",
            data: null,
            error: new ApiError(
              "Session changed. Sign in or reload.",
              401,
              "unauthenticated",
            ),
          },
        });
      }
    });
    setResult({ key, value: { status: "loading", data: null, error: null } });
    Promise.resolve()
      .then(() => load(controller.signal))
      .then((data) => {
        if (active)
          setResult({
            key,
            value: {
              status: isEmpty(data) ? "empty" : "ready",
              data,
              error: null,
            },
          });
      })
      .catch((error) => {
        if (active)
          setResult({
            key,
            value: {
              status: "error",
              data: null,
              error:
                error instanceof Error
                  ? error
                  : new Error("DEV1 request failed"),
            },
          });
      });
    return () => {
      active = false;
      controller.abort();
      unsubscribe();
    };
  }, [key, load, isEmpty, revision]);
  return {
    ...(result.key === key
      ? result.value
      : { status: "loading" as const, data: null, error: null }),
    reload,
  };
}
