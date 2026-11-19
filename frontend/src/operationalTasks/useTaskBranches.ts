import { useEffect, useState } from "react";
import { useAuth } from "../auth/AuthProvider";

export type TaskBranch = { id: number; name: string; isPrimary: boolean; canCreate: boolean; canViewWorkload?: boolean; canManageSeries?: boolean };
export function useTaskBranches() {
  const { state, actions } = useAuth();
  const owner = state.status === "AUTHENTICATED" && !state.pending && !state.session?.mustChangePassword ? state.authorityGeneration : null;
  const [revision, reload] = useState(0);
  const [result, setResult] = useState<{ owner: typeof owner; items?: TaskBranch[]; error?: string } | null>(null);
  useEffect(() => {
    setResult(null);
    const abort = new AbortController();
    if (owner) void (async () => {
      try {
        const response = await fetch('/api/operational-tasks/branches', { signal: abort.signal, credentials: 'same-origin', cache: 'no-store', redirect: 'error' });
        if (response.status === 401) { actions.invalidateSession(owner); return; }
        if (!response.ok) throw new Error();
        const value: unknown = await response.json();
        if (!Array.isArray(value) || value.some(b => !b || !Number.isSafeInteger(b.id) || b.id <= 0 || typeof b.name !== 'string' || typeof b.isPrimary !== 'boolean' || typeof b.canCreate !== 'boolean' || (b.canViewWorkload !== undefined && typeof b.canViewWorkload !== 'boolean') || (b.canManageSeries !== undefined && typeof b.canManageSeries !== 'boolean'))) throw new Error();
        if (!abort.signal.aborted) setResult({ owner, items: value });
      } catch { if (!abort.signal.aborted) setResult({ owner, error: 'Chưa tải được chi nhánh của bạn. Vui lòng thử lại.' }); }
    })();
    return () => abort.abort();
  }, [owner, actions, revision]);
  return { items: owner && result?.owner === owner ? result.items : undefined, error: owner && result?.owner === owner ? result.error : undefined, reload: () => reload(n => n + 1) };
}
