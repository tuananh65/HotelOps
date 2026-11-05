import { useEffect, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { ApiError, request } from "../../api/http";
import { id, queryPath } from "../../controllers/dev1/transportController";
import "../dev1/dev1.css";

interface Reference {
  id: number;
  code: string;
  name: string;
}
interface Role extends Reference {
  branch: Reference | null;
}
interface Account {
  id: number;
  employeeCode: string;
  username: string;
  fullName: string;
  email: string;
  phone?: string | null;
  accountStatus: string;
  primaryBranch: Reference | null;
  department: Reference | null;
  roles?: Role[];
}
interface Directory {
  items: Account[];
  page: number;
  pageSize: number;
  totalCount: number;
}
interface Access {
  assignedRoles: {
    assignmentId: number;
    name: string;
    code: string;
    branchId: number | null;
    assignmentActive: boolean;
    roleActive: boolean;
  }[];
  effectivePermissions: { code: string; name: string }[];
}
type Result = {
  key: string;
  directory?: Directory;
  account?: Account;
  error?: string;
};
function errorText(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.status === 403)
      return "Tài khoản hiện tại không có quyền quản lý người dùng.";
    if (error.status === 401)
      return "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.";
    if (error.status === 404) return "Không tìm thấy tài khoản này.";
    return error.message;
  }
  return "Không tải được dữ liệu. Vui lòng thử lại.";
}
const statusText = (status: string) =>
  status === "ACTIVE"
    ? "Hoạt động"
    : status === "INACTIVE"
      ? "Ngừng hoạt động"
      : status;

export default function AdminAccountsScreen({
  accountId,
}: {
  accountId?: string;
}) {
  const [query, setQuery] = useSearchParams();
  const [search, setSearch] = useState(query.get("search") ?? "");
  const [status, setStatus] = useState(query.get("accountStatus") ?? "");
  const parsedPage = Number(query.get("page") ?? 1);
  const page =
    Number.isSafeInteger(parsedPage) && parsedPage > 0 ? parsedPage : 1;
  const [result, setResult] = useState<Result>();
  const [reload, setReload] = useState(0);
  const key = `${accountId ?? "list"}:${query.toString()}:${reload}`;
  useEffect(() => {
    setSearch(query.get("search") ?? "");
    setStatus(query.get("accountStatus") ?? "");
  }, [query]);
  useEffect(() => {
    const controller = new AbortController();
    setResult(undefined);
    (async () => {
      if (accountId) {
        const account = await request<Account>(
          `/api/users/${id(Number(accountId))}`,
          { signal: controller.signal },
        );
        if (!controller.signal.aborted) setResult({ key, account });
      } else {
        const directory = await request<Directory>(
          queryPath("/api/users", {
            search: query.get("search"),
            accountStatus: query.get("accountStatus"),
            page,
            pageSize: 20,
          }),
          { signal: controller.signal },
        );
        // IDs must be losslessly representable before generating account links.
        directory.items.forEach((account) => id(account.id));
        if (!controller.signal.aborted) setResult({ key, directory });
      }
    })().catch((error) => {
      if (!controller.signal.aborted)
        setResult({ key, error: errorText(error) });
    });
    return () => controller.abort();
  }, [key]);
  const current = result?.key === key ? result : undefined;
  const directory = current?.directory;
  const account = current?.account;
  const changePage = (next: number) => {
    const updated = new URLSearchParams(query);
    updated.set("page", String(next));
    setQuery(updated);
  };
  return (
    <section className="dev1-page">
      <nav className="dev1-nav" aria-label="Quản lý tài khoản">
        <Link to="/users">Danh sách tài khoản</Link>
        <Link to="/profile">Hồ sơ của tôi</Link>
      </nav>
      <h1>{accountId ? "Chi tiết tài khoản" : "Quản lý tài khoản"}</h1>
      {!accountId && (
        <form
          className="dev1-context"
          onSubmit={(event) => {
            event.preventDefault();
            const next = new URLSearchParams();
            if (search.trim()) next.set("search", search.trim());
            if (status) next.set("accountStatus", status);
            setQuery(next);
          }}
        >
          <label>
            Tìm tài khoản
            <input
              value={search}
              maxLength={200}
              placeholder="Họ tên, tên đăng nhập hoặc email"
              onChange={(event) => setSearch(event.target.value)}
            />
          </label>
          <label>
            Trạng thái
            <select
              value={status}
              onChange={(event) => setStatus(event.target.value)}
            >
              <option value="">Tất cả</option>
              <option value="ACTIVE">Hoạt động</option>
              <option value="INACTIVE">Ngừng hoạt động</option>
            </select>
          </label>
          <button className="primary" type="submit">
            Tìm kiếm
          </button>
          <button
            type="button"
            onClick={() => {
              setSearch("");
              setStatus("");
              setQuery({});
            }}
          >
            Xóa bộ lọc
          </button>
        </form>
      )}
      {!current && <p role="status">Đang tải tài khoản…</p>}
      {current?.error && (
        <div role="alert">
          <p>{current.error}</p>
          <button onClick={() => setReload((value) => value + 1)}>
            Thử lại
          </button>
        </div>
      )}
      {directory && (
        <>
          <p>
            Tổng {directory.totalCount} tài khoản · Trang {directory.page}
          </p>
          {directory.items.length === 0 ? (
            <p role="status">Không có tài khoản phù hợp với bộ lọc.</p>
          ) : (
            <div style={{ overflowX: "auto" }}>
              <table className="dev1-table">
                <thead>
                  <tr>
                    <th>Nhân viên</th>
                    <th>Tên đăng nhập</th>
                    <th>Email</th>
                    <th>Chi nhánh / phòng ban</th>
                    <th>Vai trò</th>
                    <th>Trạng thái</th>
                    <th>Chi tiết</th>
                  </tr>
                </thead>
                <tbody>
                  {directory.items.map((user) => (
                    <tr key={user.id}>
                      <td>
                        {user.fullName}
                        <br />
                        <small>{user.employeeCode}</small>
                      </td>
                      <td>{user.username}</td>
                      <td>{user.email}</td>
                      <td>
                        {user.primaryBranch?.name ?? "—"}
                        <br />
                        {user.department?.name ?? "—"}
                      </td>
                      <td>
                        {user.roles?.map((role) => role.name).join(", ") || "—"}
                      </td>
                      <td>{statusText(user.accountStatus)}</td>
                      <td>
                        <Link to={`/users/${id(user.id)}`}>Xem chi tiết</Link>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
          <div className="dev1-actions">
            <button
              disabled={directory.page <= 1}
              onClick={() => changePage(directory.page - 1)}
            >
              Trang trước
            </button>
            <span>
              Trang {directory.page} /{" "}
              {Math.max(
                1,
                Math.ceil(directory.totalCount / directory.pageSize),
              )}
            </span>
            <button
              disabled={
                directory.page * directory.pageSize >= directory.totalCount
              }
              onClick={() => changePage(directory.page + 1)}
            >
              Trang sau
            </button>
          </div>
        </>
      )}
      {account && <AccountDetail account={account} />}
    </section>
  );
}
function AccountDetail({ account }: { account: Account }) {
  const [access, setAccess] = useState<Access>();
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);
  // Access review is a separate permission-protected API, loaded only on request.
  const [requested, setRequested] = useState(false);
  useEffect(() => {
    if (!requested) return;
    const controller = new AbortController();
    setLoading(true);
    request<Access>(`/api/users/${id(account.id)}/effective-access`, {
      signal: controller.signal,
    })
      .then((value) => {
        if (!controller.signal.aborted) setAccess(value);
      })
      .catch((error) => {
        if (!controller.signal.aborted) setError(errorText(error));
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });
    return () => controller.abort();
  }, [account.id, requested]);
  return (
    <>
      <dl className="dev1-details">
        {Object.entries({
          "Mã nhân viên": account.employeeCode,
          "Tên đăng nhập": account.username,
          "Họ tên": account.fullName,
          Email: account.email,
          "Điện thoại": account.phone ?? "—",
          "Trạng thái": statusText(account.accountStatus),
          "Chi nhánh": account.primaryBranch?.name ?? "—",
          "Phòng ban": account.department?.name ?? "—",
        }).map(([label, value]) => (
          <div key={label}>
            <dt>{label}</dt>
            <dd>{value}</dd>
          </div>
        ))}
      </dl>
      <button
        disabled={loading || requested}
        onClick={() => setRequested(true)}
      >
        Xem vai trò và quyền
      </button>
      {loading && <p role="status">Đang tải quyền…</p>}
      {error && <p role="alert">{error}</p>}
      {access && (
        <>
          <h2>Vai trò được gán</h2>
          <ul>
            {access.assignedRoles.map((role) => (
              <li key={role.assignmentId}>
                {role.name} ({role.code}) ·{" "}
                {role.branchId === null
                  ? "Toàn hệ thống"
                  : `Chi nhánh ${role.branchId}`}{" "}
                ·{" "}
                {role.assignmentActive && role.roleActive
                  ? "Hoạt động"
                  : "Ngừng hoạt động"}
              </li>
            ))}
          </ul>
          <h2>Quyền hiệu lực</h2>
          {access.effectivePermissions.length ? (
            <ul>
              {access.effectivePermissions.map((permission) => (
                <li key={permission.code}>
                  {permission.name} ({permission.code})
                </li>
              ))}
            </ul>
          ) : (
            <p>Không có quyền hiệu lực.</p>
          )}
        </>
      )}
    </>
  );
}
