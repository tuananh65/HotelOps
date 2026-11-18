import { displayCode } from "../i18n/vi";
import { useEffect, useMemo, useRef, useState } from "react";
import { Link, Navigate, useParams } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import { createRoleCatalogApi, type CatalogResult } from "./roleCatalogApi";
import {
  type RoleDto,
  type DetailDto,
  type PermissionDto,
  type Page,
} from "./contracts";
const keys = new WeakMap<object, number>();
let next = 0;
function key(x: object) {
  if (!keys.has(x)) keys.set(x, ++next);
  return keys.get(x)!;
}
export function RoleCatalogRoute({
  mode,
  creationEnabled = false,
}: {
  mode: "list" | "detail" | "matrix" | "catalog";
  creationEnabled?: boolean;
}) {
  const { state, actions } = useAuth();
  const { roleId = "" } = useParams();
  const binding = useMemo(
    () => ({}),
    [state.session, state.status, state.pending, mode, roleId],
  );
  const latest = useRef(binding);
  latest.current = binding;
  const current = useMemo(() => () => latest.current === binding, [binding]);
  if (state.status === "RESTRICTED" || state.session?.mustChangePassword)
    return <Navigate replace to="/change-password" />;
  if (state.status === "ANONYMOUS" || state.status === "EXPIRED_INVALID")
    return <Navigate replace to="/login" />;
  if (
    state.status !== "AUTHENTICATED" ||
    !state.session ||
    state.pending !== null
  )
    return (
      <section>
        <p>Chưa xác nhận phiên đăng nhập.</p>
        <button onClick={() => void actions.revalidate()}>
          Xác minh lại phiên
        </button>
      </section>
    );
  if (mode !== "list" && mode !== "catalog" && !/^[1-9][0-9]*$/.test(roleId))
    return <p role="alert">Mã vai trò không hợp lệ.</p>;
  return (
    <Catalog
      key={key(binding)}
      {...{ mode, roleId: Number(roleId), current, creationEnabled }}
      revalidate={() => void actions.revalidate()}
    />
  );
}
function ErrorView({
  error,
  revalidate,
}: {
  error: { status?: number; kind: string };
  revalidate: () => void;
}) {
  return (
    <section role="alert">
      <p>
        {error.status === 401
          ? "Phiên đăng nhập không hợp lệ. Xác minh lại trước khi tiếp tục."
          : error.status === 403
            ? "Máy chủ từ chối quyền truy cập."
            : error.status === 404
              ? "Không tìm thấy vai trò."
              : error.status === 503
                ? "Danh mục chưa khả dụng."
                : "Không tải được danh mục."}
      </p>
      {error.status === 401 && (
        <button onClick={revalidate}>Xác minh lại phiên</button>
      )}
    </section>
  );
}
function Catalog({
  mode,
  creationEnabled,
  roleId,
  current,
  revalidate,
}: {
  mode: "list" | "detail" | "matrix" | "catalog";
  creationEnabled: boolean;
  roleId: number;
  current: () => boolean;
  revalidate: () => void;
}) {
  const [api] = useState(() => createRoleCatalogApi());
  const [p, setP] = useState(1),
    [draft, setDraft] = useState(""),
    [search, setSearch] = useState(""),
    [active, setActive] = useState("");
  const [result, setResult] = useState<{
      binding: object;
      value: CatalogResult<Page<RoleDto> | DetailDto | Page<PermissionDto>>;
    } | null>(null),
    [composition, setComposition] = useState<{
      binding: object;
      value: CatalogResult<{ roleId: number; grants: DetailDto["grants"] }>;
    } | null>(null);
  const request = useMemo(() => ({}), [p, search, active, mode, roleId]);
  const latest = useRef(request);
  latest.current = request;
  useEffect(() => {
    const abort = new AbortController();
    let mounted = true;
    const accept = () => mounted && current() && latest.current === request;
    const read =
      mode === "list"
        ? api.roles(p, search, active, abort.signal)
        : mode === "detail"
          ? api.detail(roleId, abort.signal)
          : api.permissions(p, abort.signal);
    void read.then((r) => {
      if (accept()) setResult({ binding: request, value: r });
    });
    if (mode === "matrix")
      void api.grants(roleId, abort.signal).then((r) => {
        if (accept()) setComposition({ binding: request, value: r });
      });
    return () => {
      mounted = false;
      abort.abort();
    };
  }, [api, p, search, active, mode, roleId, current, request]);
  const r = result?.binding === request ? result.value : null,
    g = composition?.binding === request ? composition.value : null;
  const pagination = (total: number) => (
    <nav aria-label="Phân trang">
      <button disabled={p === 1} onClick={() => setP((n) => n - 1)}>
        Trước
      </button>
      <span>Trang {p}</span>
      <button disabled={p * 25 >= total} onClick={() => setP((n) => n + 1)}>
        Tiếp
      </button>
    </nav>
  );
  return (
    <main>
      <h1>
        {mode === "list"
          ? "Danh mục vai trò"
          : mode === "detail"
            ? "Chi tiết vai trò"
            : mode === "catalog"
              ? "Danh mục quyền"
              : "Ma trận quyền"}
      </h1>
      <nav>
        <Link to="/profile">Hồ sơ cá nhân</Link>
        <Link to="/roles">Vai trò</Link>
        <Link to="/permissions">Danh mục quyền</Link>
        {creationEnabled && r?.ok && <Link to="/roles/new">Tạo vai trò tùy chỉnh</Link>}
      </nav>
      <p>
        Xem vai trò và quyền cần được máy chủ cho phép hiện tại.
      </p>
      {mode === "list" && (
        <form
          onSubmit={(e) => {
            e.preventDefault();
            setP(1);
            setSearch(draft);
          }}
        >
          <label>
            Tìm vai trò
            <input
              maxLength={100}
              value={draft}
              onChange={(e) => setDraft(e.target.value)}
            />
          </label>
          <label>
            Trạng thái vai trò
            <select
              aria-label="Trạng thái vai trò"
              value={active}
              onChange={(e) => {
                setP(1);
                setActive(e.target.value);
              }}
            >
              <option value="">Tất cả</option>
              <option value="true">Đang hoạt động</option>
              <option value="false">Ngừng hoạt động</option>
            </select>
          </label>
          <button>Tìm kiếm</button>
        </form>
      )}
      {!r ? (
        <p role="status">Đang tải danh mục...</p>
      ) : !r.ok ? (
        <ErrorView error={r} revalidate={revalidate} />
      ) : mode === "list" ? (
        (() => {
          const d = r.data as Page<RoleDto>;
          return (
            <>
              <p>
                Người dùng được gán: tài khoản đang hoạt động, đang làm việc, chưa xóa và còn phân công hiệu lực; không phải số người có quyền truy cập thực tế.
              </p>
              {!d.items.length && <p>Không tìm thấy vai trò.</p>}
              <ul>
                {d.items.map((x) => (
                  <li key={x.roleId}>
                    <Link to={`/roles/${x.roleId}`}>
                      {displayCode(x.roleCode)} — {x.roleName}
                    </Link>{" "}
                    {x.isActive ? "Đang hoạt động" : "Ngừng hoạt động"};{" "}
                    {x.isSystemRole ? "Hệ thống" : "Tùy chỉnh"}; Người dùng{" "}
                    {x.assignedUserCount}; Quyền được cấp {x.permissionGrantCount}
                  </li>
                ))}
              </ul>
              {pagination(d.total)}
            </>
          );
        })()
      ) : mode === "detail" ? (
        (() => {
          const d = r.data as DetailDto;
          return (
            <>
              <h2>
                {displayCode(d.role.roleCode)} — {d.role.roleName}
              </h2>
              <p>{d.role.description}</p>
              <p>
                {d.role.isActive ? "Đang hoạt động" : "Ngừng hoạt động"};{" "}
                {d.role.isSystemRole
                  ? "Vai trò hệ thống — được bảo vệ"
                  : "Vai trò tùy chỉnh"}
                .
              </p>
              <Link to={`/roles/${roleId}/permissions`}>Ma trận quyền</Link>
              {creationEnabled && d.role.isActive && <Link to={`/roles/${roleId}/permissions/edit`}>Cấu hình quyền</Link>}
              {creationEnabled && !d.role.isSystemRole && <Link to={`/roles/${roleId}/edit`}>Chỉnh sửa thông tin vai trò</Link>}
              {creationEnabled && !d.role.isSystemRole && d.role.isActive && <Link to={`/roles/${roleId}/deactivate`}>Vô hiệu hóa vai trò</Link>}
              <ul>
                {d.grants.map((x) => (
                  <li key={x.rolePermissionId}>
                    {x.permission.permissionCode}: {displayCode(x.dataScope)}
                  </li>
                ))}
              </ul>
              {!d.grants.length && <p>Chưa có quyền được cấp.</p>}
            </>
          );
        })()
      ) : mode === "catalog" ? (
        (() => {
          const d = r.data as Page<PermissionDto>;
          return (
            <>
              {!d.items.length && <p>Không tìm thấy quyền.</p>}
              {[...new Set(d.items.map((x) => x.moduleCode))].map((module) => (
                <section key={module}>
                  <h2>{module}</h2>
                  {d.items
                    .filter((x) => x.moduleCode === module)
                    .map((x) => (
                      <p key={x.permissionId}>
                        {x.permissionCode} — {x.permissionName};{" "}
                        {x.resourceCode}/{x.actionCode}
                      </p>
                    ))}
                </section>
              ))}
              {pagination(d.total)}
            </>
          );
        })()
      ) : !g ? (
        <p role="status">Đang tải quyền được cấp...</p>
      ) : !g.ok ? (
        <ErrorView error={g} revalidate={revalidate} />
      ) : (
        (() => {
          const d = r.data as Page<PermissionDto>;
          return (
            <>
              <p>
                Danh mục và quyền là các bản đọc riêng; không cung cấp phiên bản tổng hợp hoặc quyền ghi.
              </p>
              {creationEnabled && <Link to={`/roles/${roleId}/permissions/edit`}>Cấu hình quyền</Link>}
              {!d.items.length && <p>Không tìm thấy quyền.</p>}
              {[...new Set(d.items.map((x) => x.moduleCode))].map((module) => (
                <section key={module}>
                  <h2>{module}</h2>
                  {d.items
                    .filter((x) => x.moduleCode === module)
                    .map((x) => {
                      const found = g.data.grants.filter(
                        (a) => a.permission.permissionId === x.permissionId,
                      );
                      return (
                        <p key={x.permissionId}>
                          {x.permissionCode} — {x.permissionName}:{" "}
                          {found.length
                            ? `Đã cấp (${found.map((a) => a.dataScope).join(", ")})`
                            : "Chưa cấp quyền"}
                        </p>
                      );
                    })}
                </section>
              ))}
              {pagination(d.total)}
            </>
          );
        })()
      )}
    </main>
  );
}
