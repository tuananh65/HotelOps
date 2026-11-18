import {
  CollapsibleFilterPanel,
  ActionDialog,
} from "../components/workspace-interactions";
import { useCallback, useState, type FormEvent } from "react";
import { Link } from "react-router-dom";
import { LoadingIndicator } from "../components/LoadingIndicator";
import { Eye, ExternalLink, Plus, RefreshCw, Search } from "lucide-react";
import {
  ActionMenu,
  AppButton,
  DataTable,
  LoadingSkeleton,
  Pagination,
  StatusBadge,
  ErrorState,
} from "../components/design-system";
import { displayCode } from "../i18n/vi";
import type { UserDirectoryQuery } from "./contracts";
import { useUsersContext } from "./UsersRouteBoundary";
import { useUserRead, usersReadMessage } from "./useUserRead";
import { useUserOptions } from "./useUserOptions";
import { useUserCreationAccess } from "./useUserCreationAccess";
import "./user-workspace.css";

const emptyFilters = {
  search: "",
  branchId: "",
  departmentId: "",
  roleId: "",
  accountStatus: "",
  pageSize: "20",
};
export function UserDirectoryPage({
  accountManagementEnabled = false,
}: { accountManagementEnabled?: boolean } = {}) {
  const { api } = useUsersContext();
  const creationAccess = useUserCreationAccess();
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [draft, setDraft] = useState(emptyFilters);
  const [query, setQuery] = useState<UserDirectoryQuery>({
    page: 1,
    pageSize: 20,
  });
  const [appliedFilters, setAppliedFilters] = useState("");
  const branches = useUserOptions("branches");
  const departments = useUserOptions("departments", draft.branchId);
  const roles = useUserOptions("roles");
  const load = useCallback(
    (signal: AbortSignal) => api.listUsers(query, signal),
    [api, query],
  );
  const { result, reload } = useUserRead(JSON.stringify(query), load);
  const selected = result?.ok
    ? result.data.items.find((item) => item.id === selectedId)
    : undefined;
  function restoreFilterDraft() {
    setDraft((current) => ({
      search: current.search,
      branchId: query.branchId?.toString() ?? "",
      departmentId: query.departmentId?.toString() ?? "",
      roleId: query.roleId?.toString() ?? "",
      accountStatus: query.accountStatus ?? "",
      pageSize: String(query.pageSize ?? 20),
    }));
  }
  function apply(event: FormEvent) {
    event.preventDefault();
    const nextQuery: UserDirectoryQuery = {
      search: draft.search.trim() || undefined,
      branchId: draft.branchId ? Number(draft.branchId) : undefined,
      departmentId: draft.departmentId ? Number(draft.departmentId) : undefined,
      roleId: draft.roleId ? Number(draft.roleId) : undefined,
      accountStatus: draft.accountStatus
        ? (draft.accountStatus as "ACTIVE" | "INACTIVE")
        : undefined,
      page: 1,
      pageSize: Number(draft.pageSize),
    };
    setSelectedId(null);
    setQuery(nextQuery);
    // Re-applying unchanged filters is also an explicit refresh.
    reload();
    setAppliedFilters(
      [
        draft.search.trim() && `Từ khóa: ${draft.search.trim()}`,
        branches.items.find((item) => String(item.id) === draft.branchId)?.name,
        departments.items.find((item) => String(item.id) === draft.departmentId)
          ?.name,
        roles.items.find((item) => String(item.id) === draft.roleId)?.name,
        draft.accountStatus &&
          (draft.accountStatus === "ACTIVE"
            ? "Đang hoạt động"
            : "Ngừng hoạt động"),
      ]
        .filter(Boolean)
        .join(" · "),
    );
  }
  return (
    <main className="user-directory user-workspace">
      <header className="user-page-heading">
        <div>
          <p className="user-eyebrow">Quản trị hệ thống</p>
          <h1>Quản lý người dùng</h1>
          <p>Tra cứu nhân viên, nơi làm việc và trạng thái tài khoản.</p>
        </div>
        {accountManagementEnabled && creationAccess.status === "allowed" && (
          <Link className="user-primary" to="/users/new">
            <Plus size={16} aria-hidden="true" /> Tạo tài khoản
          </Link>
        )}
      </header>
      {accountManagementEnabled && creationAccess.status === "error" && (
        <ErrorState
          message="Chưa kiểm tra được quyền tạo tài khoản. Bạn vẫn có thể tra cứu người dùng."
          retry={creationAccess.reload}
        />
      )}
      <form className="user-card" onSubmit={apply} aria-label="Bộ lọc danh mục">
        <div className="ds-quick-search">
          <label className="user-search">
            Tìm theo tên, tài khoản hoặc email
            <input
              type="search"
              placeholder="Nhập tên nhân viên, tên đăng nhập hoặc email"
              value={draft.search}
              maxLength={200}
              onChange={(event) =>
                setDraft({ ...draft, search: event.target.value })
              }
            />
          </label>
          <button type="submit">Tìm kiếm</button>
        </div>
        <CollapsibleFilterPanel
          activeCount={
            [
              query.branchId,
              query.departmentId,
              query.roleId,
              query.accountStatus,
            ].filter(Boolean).length
          }
          onOpen={restoreFilterDraft}
          onClose={restoreFilterDraft}
        >
          <div className="user-grid">
            <label>
              Trạng thái tài khoản
              <select
                aria-label="Trạng thái tài khoản"
                value={draft.accountStatus}
                onChange={(event) =>
                  setDraft({ ...draft, accountStatus: event.target.value })
                }
              >
                <option value="">Tất cả trạng thái</option>
                <option value="ACTIVE">Đang hoạt động</option>
                <option value="INACTIVE">Ngừng hoạt động</option>
              </select>
            </label>
            <label>
              Chi nhánh chính
              <select
                aria-label="Chi nhánh chính"
                value={draft.branchId}
                disabled={branches.loading || !!branches.error}
                onChange={(event) =>
                  setDraft({
                    ...draft,
                    branchId: event.target.value,
                    departmentId: "",
                  })
                }
              >
                <option value="">Tất cả chi nhánh</option>
                {branches.items.map((item) => (
                  <option key={item.id} value={item.id}>
                    {item.name}
                    {!item.isActive ? " (ngừng hoạt động)" : ""}
                  </option>
                ))}
              </select>
            </label>
            <label>
              Bộ phận
              <select
                aria-label="Bộ phận"
                value={draft.departmentId}
                disabled={
                  !draft.branchId || departments.loading || !!departments.error
                }
                onChange={(event) =>
                  setDraft({ ...draft, departmentId: event.target.value })
                }
              >
                <option value="">
                  {draft.branchId ? "Tất cả bộ phận" : "Chọn chi nhánh trước"}
                </option>
                {departments.items.map((item) => (
                  <option key={item.id} value={item.id}>
                    {item.name}
                    {!item.isActive ? " (ngừng hoạt động)" : ""}
                  </option>
                ))}
              </select>
            </label>
            <label>
              Vai trò
              <select
                aria-label="Vai trò"
                value={draft.roleId}
                disabled={roles.loading || !!roles.error}
                onChange={(event) =>
                  setDraft({ ...draft, roleId: event.target.value })
                }
              >
                <option value="">Tất cả vai trò</option>
                {roles.items.map((item) => (
                  <option key={item.id} value={item.id}>
                    {item.name}
                    {!item.isActive ? " (ngừng hoạt động)" : ""}
                  </option>
                ))}
              </select>
            </label>
          </div>
          <div className="user-actions">
            <AppButton type="submit" variant="primary" icon={Search}>
              Áp dụng bộ lọc
            </AppButton>
            <button
              type="button"
              onClick={() => {
                setDraft(emptyFilters);
                setQuery({ page: 1, pageSize: 20 });
                setAppliedFilters("");
                setSelectedId(null);
                reload();
              }}
            >
              Xóa bộ lọc
            </button>
            {(branches.loading || departments.loading || roles.loading) && (
              <LoadingIndicator label="Đang tải bộ lọc" compact />
            )}
          </div>
          {[
            { name: "chi nhánh", lookup: branches },
            { name: "bộ phận", lookup: departments },
            { name: "vai trò", lookup: roles },
          ].map(
            ({ name, lookup }) =>
              lookup.error && (
                <div key={name}>
                  <p className="user-hint" role="status">
                    Chưa tải được bộ lọc {name}. Bạn vẫn có thể tìm theo tên và
                    trạng thái.
                  </p>
                  <button type="button" onClick={lookup.reload}>
                    Tải lại {name}
                  </button>
                </div>
              ),
          )}
        </CollapsibleFilterPanel>
      </form>
      {appliedFilters && (
        <p className="user-hint" aria-label="Bộ lọc đã áp dụng">
          Đang lọc: {appliedFilters}
        </p>
      )}
      {result === null ? (
        <LoadingSkeleton label="Đang tải tài khoản" />
      ) : !result.ok ? (
        <section className="user-card">
          <p role="alert">{usersReadMessage(result.error)}</p>
          <button type="button" onClick={reload}>
            Tải lại danh sách
          </button>
        </section>
      ) : (
        <div className="user-directory-workspace">
          <section aria-label="Kết quả danh mục">
            <div className="user-results-heading">
              <h2>
                Danh sách người dùng{" "}
                <span className="user-hint">({result.data.totalCount})</span>
              </h2>
              <AppButton icon={RefreshCw} onClick={reload}>
                Làm mới
              </AppButton>
            </div>
            {result.data.items.length === 0 ? (
              <div className="user-empty" role="status">
                <strong>Không tìm thấy người dùng</strong>
                <p>Thử từ khóa khác hoặc xóa bộ lọc để xem lại danh sách.</p>
              </div>
            ) : (
              <DataTable label="Danh sách tài khoản" minWidth={850}>
                <thead>
                  <tr>
                    {[
                      "Nhân viên / tài khoản",
                      "Chi nhánh / bộ phận",
                      "Vai trò",
                      "Trạng thái",
                      "Thao tác",
                    ].map((label) => (
                      <th key={label} scope="col">
                        {label}
                      </th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {result.data.items.map((item) => (
                    <tr
                      key={item.id}
                      className={
                        selectedId === item.id ? "is-selected" : undefined
                      }
                    >
                      <td>
                        <Link
                          className="user-directory-name"
                          to={`/users/${item.id}`}
                        >
                          {item.fullName}
                        </Link>
                        <small>
                          {item.employeeCode} · @{item.username}
                        </small>
                        <small>{item.email}</small>
                      </td>
                      <td>
                        {item.primaryBranch?.name ?? "Chưa có chi nhánh"}
                        <small>
                          {item.department?.name ?? "Chưa có bộ phận"}
                        </small>
                      </td>
                      <td>
                        {item.roles.length ? (
                          <ul>
                            {item.roles.map((role, index) => (
                              <li key={`${role.id}-${index}`}>
                                {role.name}
                                <small>{role.branch?.name ?? ""}</small>
                              </li>
                            ))}
                          </ul>
                        ) : (
                          "Chưa có vai trò"
                        )}
                      </td>
                      <td>
                        <StatusBadge
                          tone={
                            item.accountStatus === "ACTIVE"
                              ? "success"
                              : "neutral"
                          }
                        >
                          {displayCode(item.accountStatus)}
                        </StatusBadge>
                      </td>
                      <td>
                        <ActionMenu
                          label={`Thao tác ${item.fullName}`}
                          items={[
                            {
                              label: "Xem nhanh",
                              icon: Eye,
                              onSelect: () => setSelectedId(item.id),
                            },
                            {
                              label: "Xem hồ sơ đầy đủ",
                              icon: ExternalLink,
                              to: `/users/${item.id}`,
                            },
                          ]}
                        />
                      </td>
                    </tr>
                  ))}
                </tbody>
              </DataTable>
            )}
            <Pagination
              page={result.data.page}
              pageSize={result.data.pageSize}
              total={result.data.totalCount}
              onPage={(page) => {
                setSelectedId(null);
                setQuery({ ...query, page });
              }}
              onPageSize={(pageSize) => {
                setDraft({ ...draft, pageSize: String(pageSize) });
                setSelectedId(null);
                setQuery({ ...query, page: 1, pageSize });
              }}
            />
          </section>
          {selected && (
            <ActionDialog
              open
              drawer
              title="Xem nhanh tài khoản"
              onClose={() => setSelectedId(null)}
            >
              <aside
                className="user-directory-inspector"
                aria-label="Xem nhanh tài khoản"
              >
                <div className="user-inspector-heading">
                  <h2>{selected.fullName}</h2>
                  <button
                    type="button"
                    aria-label="Đóng xem nhanh"
                    onClick={() => setSelectedId(null)}
                  >
                    ×
                  </button>
                </div>
                <p>
                  @{selected.username} · {selected.employeeCode}
                </p>
                <dl>
                  <dt>Email</dt>
                  <dd>{selected.email}</dd>
                  <dt>Chi nhánh chính</dt>
                  <dd>{selected.primaryBranch?.name ?? "Chưa có chi nhánh"}</dd>
                  <dt>Bộ phận</dt>
                  <dd>{selected.department?.name ?? "Chưa có bộ phận"}</dd>
                  <dt>Trạng thái tài khoản</dt>
                  <dd>{displayCode(selected.accountStatus)}</dd>
                </dl>
                <Link className="primary" to={`/users/${selected.id}`}>
                  Xem hồ sơ đầy đủ
                </Link>
              </aside>
            </ActionDialog>
          )}
        </div>
      )}
    </main>
  );
}
