import { useEffect, useState } from "react";
import { useAuth } from "../../auth/AuthProvider";

type Grant = { permission: string; scope: string; branchId: number | null };
/** Presentation hints only. Each destination still authorizes its resource and action. */
export function useWorkspacePermissions(branch: string | number) {
  const { state, actions } = useAuth();
  const owner =
    state.status === "AUTHENTICATED" &&
    !state.pending &&
    !state.session?.mustChangePassword
      ? state.authorityGeneration
      : null;
  const [snapshot, setSnapshot] = useState<{
    owner: typeof owner;
    grants: Grant[];
  } | null>(null);
  const [error, setError] = useState(false);
  const [revision, setRevision] = useState(0);
  useEffect(() => {
    const abort = new AbortController();
    setSnapshot(null);
    setError(false);
    if (owner)
      void (async () => {
        try {
          const response = await fetch(
            "/api/auth/navigation/operation-grants",
            {
              credentials: "same-origin",
              cache: "no-store",
              redirect: "error",
              signal: abort.signal,
            },
          );
          if (response.status === 401) {
            if (!abort.signal.aborted) actions.invalidateSession(owner);
            return;
          }
          if (!response.ok) throw new Error();
          const data = await response.json();
          if (
            !Array.isArray(data.grants) ||
            data.grants.some(
              (g: Grant) =>
                !g ||
                typeof g.permission !== "string" ||
                !["GLOBAL", "BRANCH", "OWN"].includes(g.scope) ||
                (g.branchId !== null &&
                  (!Number.isSafeInteger(g.branchId) || g.branchId <= 0)),
            )
          )
            throw new Error();
          if (!abort.signal.aborted)
            setSnapshot({ owner, grants: data.grants });
        } catch {
          if (!abort.signal.aborted) setError(true);
        }
      })();
    return () => abort.abort();
  }, [owner, actions, revision]);
  const grants = owner && snapshot?.owner === owner ? snapshot.grants : null;
  return {
    ready: grants !== null,
    error,
    allows: (permission: string, own = false) =>
      !!grants?.some(
        (g) =>
          g.permission === permission &&
          (g.scope === "GLOBAL" ||
            (g.branchId === Number(branch) &&
              (g.scope === "BRANCH" || (own && g.scope === "OWN")))),
      ),
    reload: () => setRevision((v) => v + 1),
  };
}
