import PageTitle from "./page-title";
import { useEffect, useRef, useState } from "react";
import { api, fields, message, type Row, type Resource } from "./api";
import { useAuth } from "./auth";
import { Dialog, Pager, State, useQuery } from "./ui";
import AccountManagement from "./account-management";
import StatusLight from "./status-light";
import AccountFilters, {
  emptyAccountFilter,
  matchesAccountFilter,
} from "./account-filters";

function directoryLabels(rows: Row[] | undefined, key: string) {
  return [
    ...new Set<string>((rows ?? []).map((row) => row[key]).filter(Boolean)),
  ];
}

export default function Admin() {
  const { can } = useAuth();
  const [page, setPage] = useState(1),
    [pageSize, setPageSize] = useState(10);
  const tableScroll = useRef<HTMLDivElement>(null);
  const [revision, bump] = useState(0),
    [search, setSearch] = useState(""),
    [filter, setFilter] = useState(emptyAccountFilter),
    [creating, setCreating] = useState(false),
    [selected, setSelected] = useState<Row | null>(null),
    [error, setError] = useState(""),
    [pending, setPending] = useState(false);
  const query = useQuery<Resource[]>(
    can("USER_VIEW") ? "users" : null,
    revision,
  );
  const users = fields(query.data);
  const normalize = (value: unknown) =>
    String(value ?? "")
      .normalize("NFD")
      .replace(/[\u0300-\u036f]/g, "")
      .replace(/đ/gi, "d")
      .toLocaleLowerCase()
      .trim();
  const filtered = users.filter(
    (u) =>
      matchesAccountFilter(u, filter) &&
      [u.fullName, u.username, u.employeeCode, u.email].some((v) =>
        normalize(v).includes(normalize(search)),
      ),
  );
  const pages = Math.max(1, Math.ceil(filtered.length / pageSize));
  const currentPage = Math.min(page, pages);
  const pageUsers = filtered.slice(
    (currentPage - 1) * pageSize,
    currentPage * pageSize,
  );
  useEffect(() => {
    tableScroll.current?.scrollTo({ top: 0 });
  }, [currentPage, pageSize, search, filter]);
  async function mutate(path: string, body: Row) {
    if (pending) return;
    setPending(true);
    setError("");
    try {
      await api(path, body);
      setCreating(false);
      setSelected(null);
      bump((x) => x + 1);
    } catch (e) {
      setError(message(e));
    } finally {
      setPending(false);
    }
  }
  return (
    <>
      <div className="page-heading">
        <PageTitle>
          <h1>Nhân sự & truy cập</h1>
          <p>Cấp quyền theo vai trò và phạm vi chi nhánh</p>
        </PageTitle>
      </div>
      <State {...query}>
        <AccountFilters
          users={users}
          search={search}
          setSearch={(value) => {
            setSearch(value);
            setPage(1);
          }}
          filter={filter}
          setFilter={(value) => {
            setFilter(value);
            setPage(1);
          }}
          count={filtered.length}
          create={
            can("USER_MANAGE")
              ? () => {
                  setCreating(true);
                  setError("");
                }
              : undefined
          }
        />
        <div className="table-scroll" ref={tableScroll}>
          <table className="account-directory">
            <thead>
              <tr>
                <th>Nhân viên</th>
                <th>Tài khoản</th>
                <th>Chi nhánh</th>
                <th>Chức vụ</th>
                <th>Thao tác</th>
              </tr>
            </thead>
            <tbody>
              {pageUsers.map((u) => (
                <tr key={u.userId}>
                  <td>
                    <div className="account-employee-name">
                      <strong>{u.fullName}</strong>
                      <StatusLight
                        active={u.accountStatus === "ACTIVE"}
                        label={
                          u.accountStatus === "ACTIVE"
                            ? "Hoạt động"
                            : u.accountStatus === "LOCKED"
                              ? "Đã khóa"
                              : "Không hoạt động"
                        }
                      />
                    </div>
                    <small>{u.employeeCode}</small>
                  </td>
                  <td>
                    {u.username}
                    <small>{u.email || ""}</small>
                  </td>
                  <td>
                    {directoryLabels(u.branches ?? u.employments, "branchName")
                      .length ? (
                      directoryLabels(
                        u.branches ?? u.employments,
                        "branchName",
                      ).map((name) => <div key={name}>{name}</div>)
                    ) : (
                      <span className="muted">Chưa gán chi nhánh</span>
                    )}
                  </td>
                  <td>
                    {directoryLabels(u.roles, "name").length ? (
                      directoryLabels(u.roles, "name").map((name) => (
                        <div key={name}>{name}</div>
                      ))
                    ) : (
                      <span className="muted">Chưa gán chức vụ</span>
                    )}
                  </td>
                  <td>
                    <button
                      onClick={() => {
                        setSelected(u);
                        setError("");
                      }}
                    >
                      Quản lý
                    </button>
                  </td>
                </tr>
              ))}
              {!filtered.length && (
                <tr>
                  <td colSpan={5} className="empty">
                    Không tìm thấy tài khoản phù hợp. Thử tên khác hoặc xóa bộ
                    lọc.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
        <nav className="account-pagination" aria-label="Phân trang tài khoản">
          <label>
            Số dòng mỗi trang
            <select
              value={pageSize}
              onChange={(e) => {
                setPageSize(Number(e.target.value));
                setPage(1);
              }}
            >
              {[10, 20, 50].map((size) => (
                <option key={size} value={size}>
                  {size}
                </option>
              ))}
            </select>
          </label>
          <span className="account-page-range" aria-live="polite">
            {filtered.length ? (currentPage - 1) * pageSize + 1 : 0}–
            {Math.min(currentPage * pageSize, filtered.length)} /{" "}
            {filtered.length} tài khoản · {pages} trang
          </span>
          <Pager
            page={currentPage}
            total={filtered.length}
            pageSize={pageSize}
            onPage={setPage}
          />
        </nav>
      </State>
      {creating && (
        <Dialog
          title="Tạo tài khoản"
          close={() => {
            if (!pending) setCreating(false);
          }}
        >
          <form
            onSubmit={(e) => {
              e.preventDefault();
              mutate(
                "users",
                Object.fromEntries(new FormData(e.currentTarget)),
              );
            }}
          >
            {[
              ["employeeCode", "Mã nhân viên"],
              ["username", "Tên đăng nhập"],
              ["fullName", "Họ tên"],
              ["email", "Email"],
              ["password", "Mật khẩu tạm thời"],
            ].map(([k, n]) => (
              <label key={k}>
                {n}
                <input
                  name={k}
                  type={
                    k === "password"
                      ? "password"
                      : k === "email"
                        ? "email"
                        : "text"
                  }
                  required={k !== "email"}
                  minLength={k === "password" ? 12 : 1}
                  autoComplete={k === "password" ? "new-password" : "off"}
                />
              </label>
            ))}
            <p>Tài khoản mới phải đổi mật khẩu khi đăng nhập.</p>
            <button className="primary" disabled={pending}>
              Tạo tài khoản
            </button>
          </form>
          {error && (
            <p className="alert" role="alert">
              {error}
            </p>
          )}
        </Dialog>
      )}
      {selected && (
        <AccountManagement
          user={selected}
          close={() => setSelected(null)}
          changed={() => bump((x) => x + 1)}
        />
      )}
    </>
  );
}
