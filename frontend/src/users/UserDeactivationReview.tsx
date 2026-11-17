import { useEffect, useState } from "react";
import { useAuth } from "../auth/AuthProvider";

type Work = { kind: string; id: number; branchId: number };
export type UserDeactivationPreview = {
  userId: number; version: string; accountStatus: string; workItems: Work[];
  hasRestrictedWork: boolean; hasMore: boolean;
  lastDurableAdministrator: boolean; coverageIncomplete: boolean; hasOpenResponsibilities: boolean;
};
const positive = (v: unknown): v is number => Number.isSafeInteger(v) && (v as number) > 0;
export function decodeUserDeactivationPreview(value: unknown, userId: number): UserDeactivationPreview | null {
  if (!value || typeof value !== "object") return null;
  const p = value as UserDeactivationPreview;
  if (p.userId !== userId || !/^[0-9A-F]{16}$/.test(p.version)
    || !["ACTIVE", "INACTIVE"].includes(p.accountStatus)
    || !Array.isArray(p.workItems) || p.workItems.length > 200
    || !p.workItems.every(w => w && typeof w.kind === "string" && positive(w.id) && positive(w.branchId))
    || [p.hasRestrictedWork, p.hasMore, p.lastDurableAdministrator, p.coverageIncomplete, p.hasOpenResponsibilities].some(v => typeof v !== "boolean")) return null;
  return p;
}

export function UserDeactivationReview({ userId, autoLoad = false }: { userId: number; autoLoad?: boolean }) {
  const { state } = useAuth();
  const [result, setResult] = useState<{ session: typeof state.session; userId: number; data: UserDeactivationPreview | null; error: string }>();
  const [reload, setReload] = useState(autoLoad ? 1 : 0);
  useEffect(() => {
    if (reload === 0 || state.status !== "AUTHENTICATED" || state.pending !== null || !state.session || !positive(userId)) return;
    const controller = new AbortController();
    let active = true;
    const session = state.session;
    setResult(undefined);
    void (async () => {
      try {
        const response = await fetch(`/api/users/${userId}/deactivation-preview`, {
          method: "GET", credentials: "same-origin", cache: "no-store", redirect: "error", signal: controller.signal,
        });
        const data = response.ok ? decodeUserDeactivationPreview(await response.json(), userId) : null;
        if (active) setResult({ session, userId, data, error: data ? "" : response.status === 403
          ? "You cannot review deactivation dependencies for this account."
          : "The dependency review is unavailable. No account change has been made." });
      } catch {
        if (active) setResult({ session, userId, data: null, error: "Unable to load the dependency review. No account change has been made." });
      }
    })();
    return () => { active = false; controller.abort(); };
  }, [userId, state.session, state.status, state.pending, reload]);
  if (state.status !== "AUTHENTICATED" || state.pending !== null || !state.session) return null;
  const current = result?.session === state.session && result.userId === userId ? result : undefined;
  return <section aria-label="Account deactivation dependencies">
    <h2>Work to resolve before deactivation</h2>
    {reload === 0 ? <p>Review assigned work before requesting an account status change.</p>
      : !current ? <p role="status">Loading dependencies…</p> : current.data ? <>
      {current.data.lastDurableAdministrator && <p role="alert">This account is the last durable Administrator and cannot be deactivated.</p>}
      {current.data.workItems.length > 0 ? <>
        <p>Ask the responsible lead to reassign or hand over these items before deactivation.</p>
        <ul>{current.data.workItems.map(w => <li key={`${w.kind}:${w.id}`}>
          {w.kind.replaceAll("_", " ")} #{w.id} — Branch {w.branchId}
        </li>)}</ul>
      </> : <p>No open work is listed within your current management scope.</p>}
      {current.data.hasRestrictedWork && <p>Additional responsibilities exist outside your management scope. Contact an authorized Administrator.</p>}
      {current.data.hasMore && <p>The first 200 items are shown. Resolve these items and refresh to continue.</p>}
      {current.data.hasOpenResponsibilities && current.data.workItems.length === 0 && <p>Other account responsibilities must be resolved before deactivation.</p>}
      {current.data.coverageIncomplete && <p>Deactivation remains unavailable until all dependency checks can be completed.</p>}
      <p>This review does not grant access to the underlying work records.</p>
    </> : <p role="alert">{current.error}</p>}
    <button type="button" onClick={() => setReload(n => n + 1)}>{reload === 0 ? "Review dependencies" : "Refresh dependencies"}</button>
  </section>;
}
