import { useEffect, useRef, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import { AssetRegistrationForm, type AssetRegistrationInput } from "./AssetRegistrationForm";

export function AssetRegistrationPage() {
  const { state, actions } = useAuth(); const navigate = useNavigate();
  const owner = state.status === "AUTHENTICATED" && !state.pending ? state.authorityGeneration : null;
  const writer = useRef<AbortController | null>(null);
  const [busy, setBusy] = useState(false), [uncertain, setUncertain] = useState(false);
  useEffect(() => { setUncertain(false); setBusy(false); return () => writer.current?.abort(); }, [owner]);
  async function save(input: AssetRegistrationInput, current: () => boolean) {
    if (!owner || !current() || writer.current || uncertain) return;
    const abort = new AbortController(); writer.current = abort; setBusy(true);
    let dispatched = false;
    try {
      const bootstrap = await fetch("/api/auth/csrf", { credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal });
      if (bootstrap.status === 401) actions.invalidateSession(owner);
      const token = await bootstrap.json();
      if (!bootstrap.ok || token.headerName !== "X-CSRF-TOKEN" || typeof token.csrfToken !== "string" || !token.csrfToken) throw Error("CSRF bootstrap unavailable.");
      if (!current() || abort.signal.aborted) return;
      dispatched = true;
      const response = await fetch("/api/asset-registration", { method: "POST", credentials: "same-origin", cache: "no-store", redirect: "error", signal: abort.signal,
        headers: { "Content-Type": "application/json", "X-CSRF-TOKEN": token.csrfToken }, body: JSON.stringify(input) });
      if (response.status === 401) actions.invalidateSession(owner);
      const body = await response.json();
      if (!response.ok) { if (response.status < 500) dispatched = false; throw Error(body.detail ?? `Registration failed (${response.status}).`); }
      if (!Number.isSafeInteger(body.assetId) || body.assetId <= 0) throw Error("Invalid registration receipt. Check the asset list before retrying.");
      if (current() && !abort.signal.aborted) navigate(`/assets/${body.assetId}/placement`, { replace: true });
    } catch (e) { if (dispatched && current()) setUncertain(true); throw e; }
    finally { if (writer.current === abort) { writer.current = null; setBusy(false); } }
  }
  return <><Link to="/assets">Asset list</Link>{uncertain && <p role="alert">Result uncertain. Check the asset list before registering again; this request will not be retried automatically.</p>}
    <AssetRegistrationForm key={String(owner)} save={save} busy={busy || uncertain} /></>;
}
