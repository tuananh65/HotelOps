import { useEffect, useMemo, useRef, useState } from "react";
import { Link, Navigate, useParams } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import { id } from "./contracts";
import { createRolePermissionConfigurationApi, type ConfigurationResult, type ConfiguredGrant } from "./rolePermissionConfigurationApi";
let generation = 0;
export function RolePermissionEditor() {
  const { roleId = "" } = useParams();
  const { state, actions } = useAuth();
  const binding = useMemo(() => ++generation, [roleId, state.session, state.status, state.pending]);
  if (state.status === "RESTRICTED" || state.session?.mustChangePassword) return <Navigate to="/change-password" replace />;
  if (state.status !== "AUTHENTICATED" || !state.session || state.pending !== null)
    return <section>Confirmed session required. <button onClick={() => void actions.revalidate()}>Revalidate session</button></section>;
  if (!/^[1-9][0-9]*$/.test(roleId) || !id(Number(roleId))) return <p role="alert">Invalid role ID.</p>;
  return <Editor key={binding} roleId={Number(roleId)} revalidate={() => void actions.revalidate()} />;
}
function Editor({ roleId, revalidate }: { roleId: number; revalidate: () => void }) {
  const [api] = useState(() => createRolePermissionConfigurationApi());
  const [result, setResult] = useState<ConfigurationResult | null>(null);
  const [desired, setDesired] = useState<ConfiguredGrant[]>([]);
  const [review, setReview] = useState(false);
  const [pending, setPending] = useState(false);
  const [locked, setLocked] = useState(false);
  const [message, setMessage] = useState("");
  const mounted = useRef(false), attempted = useRef(false), writing = useRef<AbortController | null>(null);
  useEffect(() => {
    mounted.current = true;
    const controller = new AbortController();
    void api.read(roleId, controller.signal).then(value => {
      if (controller.signal.aborted || !mounted.current) return;
      setResult(value);
      if (value.ok) setDesired(value.data.grants);
    });
    return () => { mounted.current = false; controller.abort(); writing.current?.abort(); };
  }, [api, roleId]);
  async function save() {
    if (!review || !result?.ok || !result.data.role.isActive || attempted.current || !mounted.current) return;
    attempted.current = true; setPending(true);
    writing.current = new AbortController();
    const saved = await api.save(roleId, desired, result.etag, writing.current.signal, () => mounted.current);
    if (!mounted.current) return;
    setPending(false); setLocked(true); setReview(false);
    if (saved.ok) {
      setResult(saved);
      setMessage("Permissions saved. Affected users must sign in again; scope and workflow authorization still apply.");
    } else setMessage(saved.unknown ? "Outcome unknown. Do not replay this save; review the server record with an authorized administrator."
      : saved.status === 412 ? "The role or grants changed. This selection was not saved. Reopen the editor to review current data."
      : saved.status === 409 ? "Change rejected: role inactive or last administrator protection."
      : "Save rejected or access unavailable. Revalidate your session before continuing.");
  }
  if (!result) return <p role="status">Loading role permissions...</p>;
  if (!result.ok) return <section role="alert">Permission configuration unavailable or access denied. <button onClick={revalidate}>Revalidate session</button></section>;
  const { role, catalog } = result.data;
  const valid = desired.every(g => ["ALL", "BRANCH", "OWN"].includes(g.dataScope));
  return <main>
    <h1>Configure role permissions</h1>
    <Link to={`/roles/${roleId}`}>Back to role</Link>
    <h2>{role.roleCode} — {role.roleName}</h2>
    <p>Scope stays subject to the user's branch assignment, resource ownership and workflow restrictions. ALL does not remove a branch-bound assignment's scope.</p>
    {!role.isActive && <p role="alert">Inactive roles cannot be configured.</p>}
    {!valid && <p role="alert">Legacy DEPARTMENT scope is not supported. Explicitly remove or replace it before saving.</p>}
    <fieldset disabled={!role.isActive || pending || locked}>
      <legend>Permissions by module</legend>
      {[...new Set(catalog.map(p => p.moduleCode))].map(module => <section key={module}>
        <h3>{module}</h3>
        {catalog.filter(p => p.moduleCode === module).map(p => <label key={p.permissionId} style={{ display: "block" }}>
          {p.permissionCode} — {p.permissionName}{" "}
          <select aria-label={`${p.permissionCode} scope`} value={desired.find(g => g.permissionId === p.permissionId)?.dataScope ?? ""}
            onChange={event => {
              const scope = event.target.value;
              setDesired(previous => [...previous.filter(g => g.permissionId !== p.permissionId), ...(scope ? [{ permissionId: p.permissionId, dataScope: scope }] : [])]);
              setReview(false);
            }}>
            <option value="">Not granted</option><option value="OWN">Own records</option><option value="BRANCH">Assigned branch</option><option value="ALL">All within assignment scope</option>
            <option value="DEPARTMENT" disabled>Legacy DEPARTMENT — unsupported</option>
          </select>
        </label>)}
      </section>)}
      <button disabled={!valid} onClick={() => setReview(true)}>Review changes</button>
    </fieldset>
    {review && !locked && <section aria-label="Permission change confirmation">
      <p>Save {desired.length} grants, replacing the current {result.data.grants.length}? Cleared permissions are removed. Affected sessions will end.</p>
      <ul>{desired.map(g => <li key={g.permissionId}>{catalog.find(p => p.permissionId === g.permissionId)?.permissionCode}: {g.dataScope}</li>)}</ul>
      <button disabled={pending || !valid} onClick={() => void save()}>Confirm permissions</button>
      <button disabled={pending} onClick={() => setReview(false)}>Cancel</button>
    </section>}
    {pending && <p role="status">Saving once...</p>}
    {message && <p role="status">{message}</p>}
    {locked && <button onClick={revalidate}>Revalidate session</button>}
  </main>;
}
