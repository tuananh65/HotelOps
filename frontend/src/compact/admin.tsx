import { AlertPopup } from "./alert-popup";
import PageTitle from "./page-title";
import { useEffect, useRef, useState } from "react";
import { api, fields, type Row, type Resource } from "./api";
import { useAuth } from "./auth";
import { Dialog, Pager, State, useQuery } from "./ui";
import AccountManagement from "./account-management";
import StatusLight from "./status-light";
import StatusSwitch from "./status-switch";
import {
  accountMessage,
  PasswordChecklist,
  passwordCriteria,
} from "./account-policy";
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
  const { can, profile } = useAuth();
  const isAdmin = profile.roles.some(role=>role.code==="SYS_ADMIN");
  const [accountCommand,setAccountCommand] = useState("");
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
      [u.fullName, u.username, u.employeeCode, u.email, u.phone].some((v) =>
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
      setError(accountMessage(e));
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
                <th>STT</th>
                <th>Nhân viên</th>
                <th>Tài khoản</th>
                <th>Số điện thoại</th>
                <th>Chi nhánh</th>
                <th>Chức vụ</th>
                {isAdmin && <th>Trạng thái</th>}
                <th>Thao tác</th>
              </tr>
            </thead>
            <tbody>
              {pageUsers.map((u, index) => (
                <tr key={u.userId}>
                  <td>{(currentPage - 1) * pageSize + index + 1}</td>
                  <td>
                    <div className="account-employee-name">
                      <strong>{u.fullName}</strong>
                      {!isAdmin && <StatusLight
                        active={u.accountStatus === "ACTIVE"}
                        label={
                          u.accountStatus === "ACTIVE"
                            ? "Hoạt động"
                            : u.accountStatus === "LOCKED"
                              ? "Đã khóa"
                              : "Không hoạt động"
                        }
                      />}
                    </div>
                    <small>{u.employeeCode}</small>
                  </td>
                  <td>
                    {u.username}
                    <small>{u.email || ""}</small>
                  </td>
                  <td>{u.phone || "—"}</td>
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
                  {isAdmin && <td><StatusSwitch active={u.accountStatus==="ACTIVE"} label={u.fullName} disabled={!can("USER_MANAGE")||u.userId===profile.user.fields.userId} onChange={()=>{setAccountCommand(u.accountStatus==="ACTIVE"?"deactivate":"activate");setSelected(u);setError("");}} /></td>}
                  <td>
                    <button
                      onClick={() => {
                        setAccountCommand("");
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
                  <td colSpan={isAdmin?8:7} className="empty">
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
          <CreateAccountForm
            users={users}
            pending={pending}
            submit={(body) => mutate("users", body)}
          />
          {error && (
            <AlertPopup>
              {error}
            </AlertPopup>
          )}
        </Dialog>
      )}
      {selected && (
        <AccountManagement
          user={selected}
          initialCommand={accountCommand}
          close={() => setSelected(null)}
          changed={() => bump((x) => x + 1)}
        />
      )}
    </>
  );
}

function CreateAccountForm({
  users,
  pending,
  submit,
}: {
  users: Row[];
  pending: boolean;
  submit: (body: Row) => Promise<void>;
}) {
  const { branch } = useAuth();
  const [revision, bump] = useState(0);
  const options = useQuery<{ branches: Row[]; departments: Row[] }>(
    "users/creation-options",
    revision,
  );
  const [branchId, setBranchId] = useState(String(branch || ""));
  const [departmentId, setDepartmentId] = useState("");
  const [username, setUsername] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [validation, setValidation] = useState("");
  const duplicateUsername =
    !!username.trim() &&
    users.some(
      (u) => String(u.username).toLowerCase() === username.trim().toLowerCase(),
    );
  const duplicateEmail =
    !!email.trim() &&
    users.some(
      (u) => String(u.email || "").toLowerCase() === email.trim().toLowerCase(),
    );
  const validBranch = options.data?.branches.some(
    (b) => String(b.branchId) === branchId,
  );
  const strongPassword = passwordCriteria(password).every((item) => item.met);
  return (
    <State {...options} retry={() => bump((value) => value + 1)}>
      <form
        onSubmit={(event) => {
          event.preventDefault();
          if (
            pending ||
            duplicateUsername ||
            duplicateEmail ||
            !strongPassword ||
            !validBranch
          )
            return;
          const data = new FormData(event.currentTarget);
          const fullName = String(data.get("fullName") || "").trim();
          if (!fullName || !username.trim()) {
            setValidation("Vui lòng nhập họ tên và tên đăng nhập.");
            return;
          }
          setValidation("");
          void submit({
            username: username.trim(),
            fullName,
            password,
            email: email.trim() || null,
            phone: String(data.get("phone") || "").trim() || null,
            branchId: Number(branchId),
            departmentId: departmentId ? Number(departmentId) : null,
          });
        }}
      >
        <p>Mã nhân viên được hệ thống tự sinh khi tạo tài khoản.</p>
        <label>
          Tên đăng nhập
          <input
            name="username"
            value={username}
            onChange={(event) => setUsername(event.target.value)}
            required
            maxLength={100}
            autoComplete="off"
            aria-invalid={duplicateUsername}
            aria-describedby={
              duplicateUsername ? "account-username-error" : undefined
            }
          />
        </label>
        {duplicateUsername && (
          <p id="account-username-error" role="alert">
            Tên đăng nhập đã được sử dụng.
          </p>
        )}
        <label>
          Họ tên
          <input name="fullName" required maxLength={200} autoComplete="off" />
        </label>
        <label>
          Email
          <input
            name="email"
            type="email"
            value={email}
            onChange={(event) => setEmail(event.target.value)}
            maxLength={254}
            aria-invalid={duplicateEmail}
            aria-describedby={
              duplicateEmail ? "account-email-error" : undefined
            }
          />
        </label>
        {duplicateEmail && (
          <p id="account-email-error" role="alert">
            Email đã được sử dụng bởi tài khoản khác.
          </p>
        )}
        <label>
          Số điện thoại
          <input name="phone" type="tel" maxLength={30} autoComplete="off" />
        </label>
        <label>
          Chi nhánh
          <select
            name="branchId"
            value={validBranch ? branchId : ""}
            required
            onChange={(event) => {
              setBranchId(event.target.value);
              setDepartmentId("");
            }}
          >
            <option value="">Chọn chi nhánh</option>
            {options.data?.branches.map((b) => (
              <option key={b.branchId} value={b.branchId}>
                {b.name}
              </option>
            ))}
          </select>
        </label>
        <label>
          Bộ phận
          <select
            name="departmentId"
            value={departmentId}
            disabled={!validBranch}
            onChange={(event) => setDepartmentId(event.target.value)}
          >
            <option value="">Chưa gán bộ phận</option>
            {options.data?.departments
              .filter((d) => String(d.branchId) === branchId)
              .map((d) => (
                <option key={d.departmentId} value={d.departmentId}>
                  {d.name}
                </option>
              ))}
          </select>
        </label>
        <label>
          Mật khẩu tạm thời
          <input
            name="password"
            type="password"
            value={password}
            onChange={(event) => setPassword(event.target.value)}
            required
            minLength={8}
            maxLength={128}
            autoComplete="new-password"
            aria-describedby="account-password-policy"
          />
        </label>
        <PasswordChecklist password={password} id="account-password-policy" />
        <p>
          Tài khoản mới phải đổi mật khẩu khi đăng nhập. Mật khẩu tạm có hiệu
          lực trong 24 giờ.
        </p>
        {validation && (
          <AlertPopup>
            {validation}
          </AlertPopup>
        )}
        <button
          className="primary"
          disabled={
            pending ||
            duplicateUsername ||
            duplicateEmail ||
            !strongPassword ||
            !validBranch
          }
        >
          {pending ? "Đang tạo…" : "Tạo tài khoản"}
        </button>
      </form>
    </State>
  );
}
