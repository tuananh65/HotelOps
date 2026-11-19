import { SearchableSelect } from "../components/SearchableSelect";
import { createContext, useEffect, useState, type ReactNode } from "react";
import { Link, Navigate, useParams, useSearchParams } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import { createHttpClient, isObject } from "../auth/http";
import { operations } from "./catalog.generated";
import { LoadingIndicator } from "../components/LoadingIndicator";

type Grant = { branchId: number | null; permission: string; scope: string; branchName?: string | null };
const request = createHttpClient();
const base: Record<string, string> = {
  incidents: "INCIDENT_VIEW",
  maintenance: "MAINTENANCE_VIEW",
  "material-requests": "MR_VIEW",
  replenishments: "REPLENISHMENT_VIEW",
};
export const OperationBranchesContext = createContext<{
  ids: number[];
  global: boolean;
  paths?: string[];
  names?: Record<number, string>;
}>({ ids: [], global: false });
export function permittedOperations(
  grants: Grant[],
  paths: string[],
  branch?: string | null,
  resourceDetail = false,
) {
  const branches = branch
    ? [Number(branch)]
    : [...new Set(grants.map((g) => g.branchId))];
  const allows = (codes: string[]) =>
    codes.length > 0 &&
    branches.some((b) =>
      codes.every((code) =>
        grants.some(
          (g) =>
            g.permission === code && (g.scope === "GLOBAL" || g.branchId === b),
        ),
      ),
    );
  const actions = operations
    .filter((op) => {
      const path =
        op.id === "A004A-021"
          ? "/incidents/new"
          : op.id === "A004A-043"
            ? "/material-requests/new"
            : `/operations/${op.module}`;
      return (
        (paths.includes(path) ||
          // A reporter may read their own source without access to the maintenance queue.
          // The detail API checks current ownership and branch before disclosure.
          (resourceDetail && op.id === "A004A-023")) &&
        allows([
          ...op.permissions,
          ...(base[op.module] ? [base[op.module]] : []),
        ])
      );
    })
    .map((op) => op.id);
  const modules = Object.keys(base).filter((module) =>
    actions.some((id) =>
      operations.some((op) => op.id === id && op.module === module),
    ),
  );
  const extras = ["INCIDENT_MANAGE", "PM_MANAGE"].filter((code) =>
    allows([code, code === "PM_MANAGE" ? "MAINTENANCE_VIEW" : "INCIDENT_VIEW"]),
  );
  return { actions, modules, extras };
}

export function AuthorizedOperations({
  children,
}: {
  children: (access: ReturnType<typeof permittedOperations>) => ReactNode;
}) {
  const { state, actions } = useAuth();
  const owner =
    state.status === "AUTHENTICATED" && !state.pending
      ? state.authorityGeneration
      : null;
  const [query] = useSearchParams();
  const { module = "incidents", id } = useParams();
  const [snapshot, setSnapshot] = useState<{
    owner: typeof owner;
    grants: Grant[];
    paths: string[];
  }>();
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
        request({
          method: "GET",
          url: "/api/auth/navigation/operation-grants",
          expectedStatus: 200,
          signal: controller.signal,
          decode: (raw) =>
            isObject(raw) &&
            Array.isArray(raw.grants) &&
            raw.grants.every(
              (g) =>
                isObject(g) &&
                typeof g.permission === "string" &&
                ["GLOBAL", "BRANCH", "OWN"].includes(String(g.scope)) &&
                (g.branchId === null || Number.isSafeInteger(g.branchId)),
            )
              ? (raw.grants as Grant[])
              : undefined,
        }),
        request({
          method: "GET",
          url: "/api/auth/navigation",
          expectedStatus: 200,
          signal: controller.signal,
          decode: (raw) =>
            isObject(raw) &&
            Array.isArray(raw.paths) &&
            raw.paths.every((p) => typeof p === "string")
              ? (raw.paths as string[])
              : undefined,
        }),
      ])
        .then(([grants, nav]) => {
          if (controller.signal.aborted) return;
          if (grants.ok && nav.ok) {
            setSnapshot({ owner, grants: grants.data, paths: nav.data });
            setFailed(false);
          } else {
            setFailed(true);
            if (
              [grants, nav].some(
                (r) =>
                  !r.ok &&
                  r.error.kind === "http" &&
                  [401, 403].includes(r.error.status),
              )
            ) {
              setSnapshot(undefined);
              if (
                [grants, nav].some(
                  (r) =>
                    !r.ok && r.error.kind === "http" && r.error.status === 401,
                )
              )
                actions.invalidateSession(owner);
            }
          }
        })
        .finally(() => {
          running = false;
        });
    };
    refresh();
    window.addEventListener("focus", refresh);
    const timer = window.setInterval(refresh, 30000);
    return () => {
      controller.abort();
      window.removeEventListener("focus", refresh);
      window.clearInterval(timer);
    };
  }, [owner, revision, actions]);
  if (!snapshot || snapshot.owner !== owner)
    return failed ? (
      <p role="status">
        Không xác nhận được quyền hiện tại.{" "}
        <button onClick={() => setRevision((v) => v + 1)}>Thử lại</button>
      </p>
    ) : (
      <LoadingIndicator label="Đang kiểm tra quyền" />
    );
  const access = permittedOperations(
    snapshot.grants,
    snapshot.paths,
    query.get("branchId"),
    !!id && /^[1-9]\d*$/.test(id) && Number.isSafeInteger(Number(id)),
  );
  const desired =
    query.get("action") ??
    operations.find(
      (op) =>
        op.module === module &&
        op.id ===
          (
            {
              incidents: id ? "A004A-023" : "A004A-022",
              maintenance: id ? "A004A-036" : "A004A-035",
              "material-requests": id ? "A004A-038" : "A004A-037",
              replenishments: id ? "A004A-053" : "A004A-052",
            } as Record<string, string>
          )[module],
    )?.id;
  if (
    !access.modules.includes(module) ||
    !desired ||
    !access.actions.includes(desired)
  )
    return (
      <p role="alert">
        Bạn không có quyền sử dụng chức năng này.{" "}
        <Link to="/home">Về trang chủ</Link>
      </p>
    );
  const branchAccess = {
    names: Object.fromEntries(snapshot.grants.filter(g => g.branchId !== null && g.branchName).map(g => [g.branchId!, g.branchName!])),
    ids: [
      ...new Set(
        snapshot.grants
          .filter((g) => g.permission === base[module] && g.branchId !== null)
          .map((g) => g.branchId!),
      ),
    ],
    global: snapshot.grants.some(
      (g) => g.permission === base[module] && g.scope === "GLOBAL",
    ),
  };
  if (
    ["A004A-022", "A004A-035", "A004A-037"].includes(desired) &&
    !query.has("branchId")
  ) {
    const branches = branchAccess.ids;
    const destination = (branch: number) => {
      const next = new URLSearchParams(query);
      next.set("branchId", String(branch));
      return `/operations/${module}?${next}`;
    };
    if (branches.length === 1)
      return <Navigate replace to={destination(branches[0])} />;
    return (
      <BranchPrompt
        names={branchAccess.names}
        ids={branches}
        global={branchAccess.global}
        destination={destination}
      />
    );
  }
  return (
    <>
      {failed && (
        <p role="status">
          Không xác nhận được quyền hiện tại. Nội dung đang nhập được giữ tạm;
          chức năng sẽ mở lại khi kiểm tra thành công.{" "}
          <button onClick={() => setRevision((v) => v + 1)}>Thử lại</button>
        </p>
      )}
      <OperationBranchesContext.Provider
        value={{ ...branchAccess, paths: snapshot.paths }}
      >
        <div hidden={failed} inert={failed}>
          {children(access)}
        </div>
      </OperationBranchesContext.Provider>
    </>
  );
}

function BranchPrompt({
  names,
  ids,
  global,
  destination,
}: {
  names: Record<number, string>;
  ids: number[];
  global: boolean;
  destination: (id: number) => string;
}) {
  const [selected, setSelected] = useState("");
  const valid =
    /^[1-9]\d*$/.test(selected) && Number.isSafeInteger(Number(selected));
  return (
    <section className="ops-read-card">
      <h1>Chọn chi nhánh</h1>
      <p>Chọn phạm vi để tải danh sách công việc và yêu cầu.</p>
      <label>
        Chi nhánh
        <SearchableSelect
          aria-label="Chi nhánh"
          value={selected}
          onChange={(e) => setSelected(e.target.value)}
        >
          <option value="">Chọn chi nhánh</option>
          {ids.map((id) => (
            <option key={id} value={id}>
              {names[id] ?? "Chưa có tên chi nhánh"}
            </option>
          ))}
        </SearchableSelect>
      </label>
      {global && (
        <label>
          Mã chi nhánh khác
          <input
            type="number"
            min="1"
            value={selected}
            onChange={(e) => setSelected(e.target.value)}
          />
        </label>
      )}
      {valid && <Link to={destination(Number(selected))}>Xem dữ liệu</Link>}
      {!ids.length && !global && (
        <p role="status">Chưa có chi nhánh được phép xem.</p>
      )}
    </section>
  );
}
