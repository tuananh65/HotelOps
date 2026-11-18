import { useEffect, useState } from "react";
import { useAuth } from "../auth/AuthProvider";

/** UI admission uses the existing server grant feed. POST still checks actor, scope and references. */
export function useUserCreationAccess() {
  const { state, actions } = useAuth();
  const owner =
    state.status === "AUTHENTICATED" ? state.authorityGeneration : null;
  const [retry, setRetry] = useState(0);
  const [result, setResult] = useState<{
    owner: typeof owner;
    status: "allowed" | "denied" | "error";
  } | null>(null);
  useEffect(() => {
    if (!owner) return;
    const abort = new AbortController();
    setResult(null);
    void Promise.resolve().then(async () => {
      if (abort.signal.aborted) return;
      try {
        const response = await fetch("/api/auth/navigation/operation-grants", {
          method: "GET",
          credentials: "same-origin",
          cache: "no-store",
          redirect: "error",
          signal: abort.signal,
        });
        if (response.status === 401) {
          if (!abort.signal.aborted) actions.invalidateSession(owner);
          return;
        }
        if (!response.ok) throw new Error();
        const value: unknown = await response.json();
        if (
          !value ||
          typeof value !== "object" ||
          !("grants" in value) ||
          !Array.isArray(value.grants) ||
          value.grants.some(
            (grant) =>
              !grant ||
              typeof grant.permission !== "string" ||
              !["GLOBAL", "BRANCH", "OWN"].includes(grant.scope) ||
              !(
                grant.branchId === null ||
                (Number.isSafeInteger(grant.branchId) && grant.branchId > 0)
              ),
          )
        )
          throw new Error();
        const grants = new Set(
          value.grants
            .filter(
              (grant) => grant.scope === "GLOBAL" && grant.branchId === null,
            )
            .map((grant) => grant.permission),
        );
        if (!abort.signal.aborted)
          setResult({
            owner,
            status:
              grants.has("USER_MANAGE") && grants.has("ROLE_MANAGE")
                ? "allowed"
                : "denied",
          });
      } catch {
        if (!abort.signal.aborted) setResult({ owner, status: "error" });
      }
    });
    return () => abort.abort();
  }, [owner, retry, actions]);
  return {
    status: result?.owner === owner ? (result?.status ?? "loading") : "loading",
    reload: () => setRetry((value) => value + 1),
  };
}
