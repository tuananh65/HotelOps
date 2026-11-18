import { useEffect, useState, type ReactNode } from "react";
import { Link, Navigate, useParams, useSearchParams } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import { createHttpClient, isObject } from "../auth/http";
import { operations } from "./catalog.generated";
import { LoadingIndicator } from "../components/LoadingIndicator";

type Grant = { branchId: number | null; permission: string; scope: string };
const request = createHttpClient();
const base: Record<string, string> = { incidents: "INCIDENT_VIEW", maintenance: "MAINTENANCE_VIEW", "material-requests": "MR_VIEW", replenishments: "REPLENISHMENT_VIEW" };
export function permittedOperations(grants: Grant[], paths: string[], branch?: string | null) {
  const branches = branch ? [Number(branch)] : [...new Set(grants.map(g => g.branchId))];
  const allows = (codes: string[]) => codes.length > 0 && branches.some(b => codes.every(code => grants.some(g =>
    g.permission === code && (g.scope === "GLOBAL" || g.branchId === b))));
  const actions = operations.filter(op => {
    const path = op.id === "A004A-021" ? "/incidents/new" : op.id === "A004A-043" ? "/material-requests/new" : `/operations/${op.module}`;
    return paths.includes(path) && allows([...op.permissions, ...(base[op.module] ? [base[op.module]] : [])]);
  }).map(op => op.id);
  const modules = Object.keys(base).filter(module => actions.some(id => operations.some(op => op.id === id && op.module === module)));
  const extras = ["INCIDENT_MANAGE", "PM_MANAGE"].filter(code => allows([code, code === "PM_MANAGE" ? "MAINTENANCE_VIEW" : "INCIDENT_VIEW"]));
  return { actions, modules, extras };
}

export function AuthorizedOperations({ children }: { children: (access: ReturnType<typeof permittedOperations>) => ReactNode }) {
  const { state, actions } = useAuth();
  const owner = state.status === "AUTHENTICATED" && !state.pending ? state.authorityGeneration : null;
  const [query] = useSearchParams();
  const { module = "incidents", id } = useParams();
  const [snapshot, setSnapshot] = useState<{ owner: typeof owner; grants: Grant[]; paths: string[] }>();
  const [failed, setFailed] = useState(false);
  const [revision, setRevision] = useState(0);
  useEffect(() => {
    if (!owner) return;
    const controller = new AbortController();
    let running = false;
    const refresh = () => {
      if (running || controller.signal.aborted) return;
      running = true;
    void Promise.all([
      request({ method: "GET", url: "/api/auth/navigation/operation-grants", expectedStatus: 200, signal: controller.signal,
        decode: raw => isObject(raw) && Array.isArray(raw.grants) && raw.grants.every(g => isObject(g) && typeof g.permission === "string"
          && ["GLOBAL", "BRANCH", "OWN"].includes(String(g.scope)) && (g.branchId === null || Number.isSafeInteger(g.branchId))) ? raw.grants as Grant[] : undefined }),
      request({ method: "GET", url: "/api/auth/navigation", expectedStatus: 200, signal: controller.signal,
        decode: raw => isObject(raw) && Array.isArray(raw.paths) && raw.paths.every(p => typeof p === "string") ? raw.paths as string[] : undefined }),
    ]).then(([grants, nav]) => {
      if (controller.signal.aborted) return;
      if (grants.ok && nav.ok) { setSnapshot({ owner, grants: grants.data, paths: nav.data }); setFailed(false); }
      else {
        setFailed(true);
        if ([grants, nav].some(r => !r.ok && r.error.kind === "http" && [401, 403].includes(r.error.status))) {
          setSnapshot(undefined);
          if ([grants, nav].some(r => !r.ok && r.error.kind === "http" && r.error.status === 401)) actions.invalidateSession(owner);
        }
      }
    }).finally(() => { running = false; });
    };
    refresh();
    window.addEventListener("focus", refresh);
    const timer = window.setInterval(refresh, 30000);
    return () => { controller.abort(); window.removeEventListener("focus", refresh); window.clearInterval(timer); };
  }, [owner, revision, actions]);
  if (!snapshot || snapshot.owner !== owner) return failed
    ? <p role="status">Không xác nhận được quyền hiện tại. <button onClick={() => setRevision(v => v + 1)}>Thử lại</button></p>
    : <LoadingIndicator label="Đang kiểm tra quyền" />;
  const access = permittedOperations(snapshot.grants, snapshot.paths, query.get("branchId"));
  const desired = query.get("action") ?? operations.find(op => op.module === module && op.id === ({incidents: id ? "A004A-023" : "A004A-022", maintenance: id ? "A004A-036" : "A004A-035", "material-requests": id ? "A004A-038" : "A004A-037", replenishments: id ? "A004A-053" : "A004A-052"} as Record<string,string>)[module])?.id;
  if (!access.modules.includes(module) || !desired || !access.actions.includes(desired)) return <p role="alert">Bạn không có quyền sử dụng chức năng này. <Link to="/home">Về trang chủ</Link></p>;
  if (module === "incidents" && desired === "A004A-022" && !query.has("branchId")) {
    const branches = [...new Set(snapshot.grants.filter(g => g.permission === "INCIDENT_VIEW" && g.branchId !== null).map(g => g.branchId!))];
    const destination = (branch: number) => { const next = new URLSearchParams(query); next.set("branchId", String(branch)); return `/operations/incidents?${next}`; };
    if (branches.length === 1) return <Navigate replace to={destination(branches[0])} />;
    return <section><p>Chọn chi nhánh để xem sự cố.</p>{branches.map(branch => <p key={branch}><Link to={destination(branch)}>Chi nhánh {branch}</Link></p>)}{!branches.length && <p>Chưa xác định được chi nhánh xem sự cố của tài khoản.</p>}</section>;
  }
  return <>
    {failed && <p role="status">Không xác nhận được quyền hiện tại. Nội dung đang nhập được giữ tạm; chức năng sẽ mở lại khi kiểm tra thành công. <button onClick={() => setRevision(v => v + 1)}>Thử lại</button></p>}
    <div hidden={failed} inert={failed}>{children(access)}</div>
  </>;
}
