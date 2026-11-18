import { displayCode } from "../i18n/vi";
import { useCallback, useState, type FormEvent } from "react";
import { Link } from "react-router-dom";
import type { UserDirectoryQuery } from "./contracts";
import { useUsersContext } from "./UsersRouteBoundary";
import { useUserRead, usersReadMessage } from "./useUserRead";

const filterId = (value: string): number | undefined => {
  const input = value.trim();
  if (!input) return undefined;
  if (!/^\d+$/.test(input)) return NaN;
  const numeric = Number(input);
  return Number.isSafeInteger(numeric) && numeric > 0 ? numeric : NaN;
};

export function UserDirectoryPage({
  accountManagementEnabled = false,
}: { accountManagementEnabled?: boolean } = {}) {
  const { api } = useUsersContext();
  const [selectedId, setSelectedId] = useState<number | null>(null);
  const [draft, setDraft] = useState({
    search: "",
    branchId: "",
    departmentId: "",
    roleId: "",
    accountStatus: "",
    pageSize: "20",
  });
  const [query, setQuery] = useState<UserDirectoryQuery>({
    page: 1,
    pageSize: 20,
  });
  const load = useCallback(
    (signal: AbortSignal) => api.listUsers(query, signal),
    [api, query],
  );
  const { result, reload } = useUserRead(JSON.stringify(query), load);
  const selected = result?.ok
    ? result.data.items.find((item) => item.id === selectedId)
    : undefined;
  function apply(event: FormEvent) {
    event.preventDefault();
    setQuery({
      search: draft.search || undefined,
      branchId: filterId(draft.branchId),
      departmentId: filterId(draft.departmentId),
      roleId: filterId(draft.roleId),
      accountStatus: draft.accountStatus
        ? (draft.accountStatus as "ACTIVE" | "INACTIVE")
        : undefined,
      page: 1,
      pageSize: Number(draft.pageSize),
    });
  }
  const appliedFilters = [
    query.search?.trim() ? "Tìm kiếm:" + query.search.trim() : null,
    query.branchId ? "Chi nhánh chính:" + query.branchId : null,
    query.departmentId ? "Bộ phận:" + query.departmentId : null,
    query.roleId ? "Vai trò:" + query.roleId : null,
    query.accountStatus ? "Trạng thái:" + query.accountStatus : null,
  ]
    .filter(Boolean)
    .join("; ");
  return (
    <main className="user-directory">
      <header>
        <h1>Danh mục người dùng</h1>
        {accountManagementEnabled && (
          <Link to="/users/new">Tạo tài khoản người dùng</Link>
        )}
        <Link to="/dashboard">Không gian làm việc</Link>
        <Link to="/profile">Hồ sơ cá nhân</Link>
      </header>
      <form onSubmit={apply} aria-label="Bộ lọc danh mục">
        <label>
          Tìm theo tên, tài khoản hoặc email
          <input
            value={draft.search}
            maxLength={200}
            onChange={(event) =>
              setDraft({ ...draft, search: event.target.value })
            }
          />
        </label>
        {(["branchId", "departmentId", "roleId"] as const).map((key) => (
          <label key={key}>
            {key === "branchId"
              ? "Mã chi nhánh chính"
              : key === "departmentId"
                ? "Mã bộ phận"
                : "Mã vai trò"}
            <input
              inputMode="numeric"
              value={draft[key]}
              onChange={(event) =>
                setDraft({ ...draft, [key]: event.target.value })
              }
            />
          </label>
        ))}
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
          Số dòng mỗi trang
          <select
            aria-label="Số dòng mỗi trang"
            value={draft.pageSize}
            onChange={(event) =>
              setDraft({ ...draft, pageSize: event.target.value })
            }
          >
            {[20, 50, 100].map((size) => (
              <option key={size} value={size}>
                {displayCode(size)}
              </option>
            ))}
          </select>
        </label>
        <button type="submit">Áp dụng bộ lọc</button>
        <button
          type="button"
          onClick={() => {
            setDraft({
              search: "",
              branchId: "",
              departmentId: "",
              roleId: "",
              accountStatus: "",
              pageSize: "20",
            });
            setQuery({ page: 1, pageSize: 20 });
          }}
        >
          Xóa bộ lọc
        </button>
      </form>
      <p aria-label="Bộ lọc đã áp dụng">
        Bộ lọc đã áp dụng: {appliedFilters || "Không có"}
      </p>
      {result === null ? (
        <p role="status">Đang tải tài khoản...</p>
      ) : !result.ok ? (
        <section>
          <p role="alert">{usersReadMessage(result.error)}</p>
          <button type="button" onClick={reload}>
            Tải lại danh mục
          </button>
        </section>
      ) : (
        <div
          className={`user-directory-workspace${selected ? " has-selection" : ""}`}
        >
          <section aria-label="Kết quả danh mục">
            <p>Tài khoản phù hợp: {result.data.totalCount}</p>
            <table>
              <caption>Tài khoản người dùng</caption>
              <thead>
                <tr>
                  {[
                    "Tài khoản người dùng",
                    "Chi nhánh / bộ phận",
                    "Vai trò",
                    "Trạng thái tài khoản",
                    "Chi tiết",
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
                        {item.employeeCode} · @{item.username} · #{item.id}
                      </small>
                      <small>{item.email}</small>
                    </td>
                    <td>
                      {item.primaryBranch?.name ?? "Chưa cung cấp"}
                      <small>{item.department?.name ?? "Chưa cung cấp"}</small>
                    </td>
                    <td>
                      {item.roles.length ? (
                        <ul>
                          {item.roles.map((role, index) => (
                            <li key={`${role.id}-${index}`}>
                              {role.code} — {role.name} (
                              {role.branch?.name ?? "Chưa cung cấp chi nhánh"})
                            </li>
                          ))}
                        </ul>
                      ) : (
                        "Không có dữ liệu trả về"
                      )}
                    </td>
                    <td>
                      <span
                        className={`user-account-status ${item.accountStatus === "ACTIVE" ? "active" : "inactive"}`}
                      >
                        {displayCode(item.accountStatus)}
                      </span>
                    </td>
                    <td>
                      <button
                        type="button"
                        aria-label={`Xem nhanh ${item.fullName}`}
                        aria-expanded={selectedId === item.id}
                        onClick={() =>
                          setSelectedId(selectedId === item.id ? null : item.id)
                        }
                      >
                        Xem nhanh
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
            {result.data.items.length === 0 && (
              <p role="status">MSG10: Không có hồ sơ phù hợp bộ lọc đã áp dụng.</p>
            )}
            <p>
              Trang {result.data.page} trên{" "}
              {Math.max(
                1,
                Math.ceil(result.data.totalCount / result.data.pageSize),
              )}
            </p>
            <button
              type="button"
              disabled={result.data.page <= 1}
              onClick={() => setQuery({ ...query, page: result.data.page - 1 })}
            >
              Trang trước
            </button>
            <button
              type="button"
              disabled={
                result.data.page * result.data.pageSize >=
                result.data.totalCount
              }
              onClick={() => setQuery({ ...query, page: result.data.page + 1 })}
            >
              Trang sau
            </button>
          </section>
          {selected && (
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
                <dd>{selected.primaryBranch?.name ?? "Chưa cung cấp"}</dd>
                <dt>Bộ phận</dt>
                <dd>{selected.department?.name ?? "Chưa cung cấp"}</dd>
                <dt>Trạng thái tài khoản</dt>
                <dd>{displayCode(selected.accountStatus)}</dd>
              </dl>
              <Link className="primary" to={`/users/${selected.id}`}>
                Xem hồ sơ đầy đủ
              </Link>
            </aside>
          )}
        </div>
      )}
    </main>
  );
}
