import { useState } from "react";
import { api, action, fields, message, timestamp, type Row } from "./api";
import { useAuth } from "./auth";
import { Dialog, State, useQuery } from "./ui";
import BranchContext from "./branch-context";
import ResetPasswordForm from "./reset-password-form";

const membershipNames: Record<string, string> = {
  BRANCH_ACCESS: "Truy cập chi nhánh",
  EMPLOYMENT: "Công tác",
  DEPARTMENT_AUTHORITY: "Phụ trách bộ phận",
};

export default function AccountManagement({
  user,
  close,
  changed,
}: {
  user: Row;
  close: () => void;
  changed: () => void;
}) {
  const { can, branch, profile } = useAuth();
  const [revision, bump] = useState(0),
    [command, setCommand] = useState(""),
    [revoking, setRevoking] = useState<Row | null>(null);
  const [pending, setPending] = useState(false),
    [error, setError] = useState(""),
    [success, setSuccess] = useState("");
  const detail = useQuery(`users/${user.userId}`, revision);
  const roles = useQuery(
    can("ROLE_MANAGE") && command === "role"
      ? "catalog/roles?pageSize=100"
      : null,
  );
  const departments = useQuery(
    can("MASTER_DATA_MANAGE") && command === "membership"
      ? `catalog/departments?branchId=${branch}&pageSize=100`
      : null,
  );
  const account: Row = detail.data?.user.fields || user;
  const self = account.userId === profile.user.fields.userId;
  function choose(value: string, row: Row | null = null) {
    setCommand(value);
    setRevoking(row);
    setError("");
    setSuccess("");
  }
  async function submit(form: HTMLFormElement) {
    if (pending) return;
    const data = new FormData(form);
    let path = `users/${user.userId}`,
      method = "POST",
      body: Row;
    switch (command) {
      case "edit":
        method = "PUT";
        body = {
          rowVersion: account.rowVersion,
          fullName: data.get("fullName"),
          email: data.get("email") || null,
          phone: data.get("phone") || null,
        };
        break;
      case "reset":
        path += "/reset-password";
        body = {
          rowVersion: account.rowVersion,
          adminPassword: data.get("adminPassword"),
          gmailAddress: data.get("gmailAddress"),
          gmailAppPassword: data.get("gmailAppPassword"),
          reason: "Quản trị viên xác nhận gửi mật khẩu tạm qua Gmail.",
        };
        break;
      case "membership":
        path += "/memberships";
        body = {
          branchId: branch,
          kind: data.get("kind"),
          departmentId: data.get("departmentId")
            ? Number(data.get("departmentId"))
            : null,
        };
        break;
      case "role":
        path += "/roles";
        body = { branchId: branch, roleId: Number(data.get("roleId")) };
        break;
      case "revoke-role":
        path = `users/role-assignments/${revoking!.userRoleId}/revoke`;
        body = { rowVersion: revoking!.rowVersion };
        break;
      case "revoke-membership":
        path = `users/memberships/${revoking!.membershipId}/revoke`;
        body = { rowVersion: revoking!.rowVersion };
        break;
      default:
        path += `/${command}`;
        body = action(account.rowVersion, { reason: data.get("reason") });
    }
    setPending(true);
    setError("");
    setSuccess("");
    try {
      const response = await api(path, body, method);
      setSuccess(
        command === "reset"
          ? `Đã gửi mật khẩu tạm đến ${response.recipient}. Hết hạn sau 24 giờ; người dùng phải đổi mật khẩu khi đăng nhập.`
          : "Đã cập nhật tài khoản.",
      );
      setCommand("");
      setRevoking(null);
      bump((x) => x + 1);
      changed();
    } catch (e) {
      setError(message(e));
    } finally {
      for (const name of ["adminPassword", "gmailAppPassword"]) {
        const input = form.elements.namedItem(name) as HTMLInputElement | null;
        if (input) input.value = "";
      }
      setPending(false);
    }
  }
  return (
    <Dialog
      title={account.fullName}
      close={() => {
        if (!pending) close();
      }}
    >
      <State {...detail} retry={() => bump((x) => x + 1)}>
        <div className="account-detail-grid">
          <div>
            <small>Tài khoản</small>
            <strong>{account.username}</strong>
            <small>{account.employeeCode}</small>
          </div>
          <div>
            <small>Email</small>
            <span>{account.email || "Chưa có email"}</span>
          </div>
          <div>
            <small>Điện thoại</small>
            <span>{account.phone || "Chưa có số điện thoại"}</span>
          </div>
          <div>
            <small>Đăng nhập gần nhất</small>
            <span>{timestamp(account.lastLoginAt)}</span>
          </div>
        </div>
        {success && (
          <p className="account-success" role="status">
            {success}
          </p>
        )}
        <div className="account-management-actions">
          {can("USER_MANAGE") && (
            <>
              <button
                disabled={pending}
                className="account-edit"
                onClick={() => choose("edit")}
              >
                Chỉnh sửa thông tin
              </button>
              <button disabled={pending} onClick={() => choose("membership")}>
                Thêm phạm vi
              </button>
              {!self && (
                <button
                  disabled={pending}
                  onClick={() =>
                    choose(
                      account.accountStatus === "ACTIVE"
                        ? "deactivate"
                        : "activate",
                    )
                  }
                >
                  {account.accountStatus === "ACTIVE"
                    ? "Vô hiệu hóa"
                    : "Kích hoạt"}
                </button>
              )}
              {!self && account.accountStatus === "ACTIVE" && (
                <button
                  disabled={pending}
                  className="primary"
                  onClick={() => choose("reset")}
                >
                  Đặt lại mật khẩu
                </button>
              )}
            </>
          )}
          {can("ROLE_MANAGE") && (
            <button disabled={pending} onClick={() => choose("role")}>
              Gán vai trò
            </button>
          )}
        </div>
        {command === "reset" && (
          <ResetPasswordForm recipient={account.email}
            sender={/@(gmail|googlemail)\.com$/i.test(profile.user.fields.email || "") ? profile.user.fields.email : ""}
            pending={pending} error={error} submit={submit} cancel={() => choose("")} />
        )}
        {command && command !== "reset" && (
          <form
            key={command}
            className="account-management-form"
            onSubmit={(e) => {
              e.preventDefault();
              void submit(e.currentTarget);
            }}
          >
            <fieldset disabled={pending}>
              {command === "edit" && (
                <>
                  <h3>Chỉnh sửa thông tin</h3>
                  <label>
                    Họ tên
                    <input
                      name="fullName"
                      required
                      maxLength={200}
                      defaultValue={account.fullName}
                    />
                  </label>
                  <label>
                    Email
                    <input
                      name="email"
                      type="email"
                      maxLength={254}
                      defaultValue={account.email || ""}
                    />
                  </label>
                  <label>
                    Số điện thoại
                    <input
                      name="phone"
                      type="tel"
                      maxLength={30}
                      defaultValue={account.phone || ""}
                    />
                  </label>
                </>
              )}
              {["membership", "role"].includes(command) && <BranchContext />}
              {command === "membership" && (
                <>
                  <h3>Thêm phạm vi</h3>
                  <label>
                    Loại phạm vi
                    <select name="kind">
                      <option value="BRANCH_ACCESS">Truy cập chi nhánh</option>
                      <option value="EMPLOYMENT">Công tác</option>
                      <option value="DEPARTMENT_AUTHORITY">
                        Phụ trách bộ phận
                      </option>
                    </select>
                  </label>
                  <label>
                    Bộ phận
                    <select name="departmentId" defaultValue="">
                      <option value="">Không chọn</option>
                      {fields(departments.data?.items).map((d) => (
                        <option key={d.departmentId} value={d.departmentId}>
                          {d.name}
                        </option>
                      ))}
                    </select>
                  </label>
                  {departments.error && (
                    <p className="alert">{message(departments.error)}</p>
                  )}
                </>
              )}
              {command === "role" && (
                <>
                  <h3>Gán vai trò</h3>
                  <label>
                    Vai trò
                    <select name="roleId" required defaultValue="">
                      <option value="">Chọn vai trò</option>
                      {fields(roles.data?.items)
                        .filter((r) => r.isActive && r.code !== "SYS_ADMIN")
                        .map((r) => (
                          <option key={r.roleId} value={r.roleId}>
                            {r.name}
                          </option>
                        ))}
                    </select>
                  </label>
                  {roles.error && (
                    <p className="alert">{message(roles.error)}</p>
                  )}
                </>
              )}
              {["activate", "deactivate"].includes(command) && (
                <>
                  <h3>
                    {command === "activate"
                      ? "Kích hoạt tài khoản"
                      : "Vô hiệu hóa tài khoản"}
                  </h3>
                  <label>
                    Lý do
                    <textarea
                      name="reason"
                      required
                      minLength={10}
                      maxLength={2000}
                    />
                  </label>
                </>
              )}
              {command.startsWith("revoke-") && (
                <>
                  <h3>Xác nhận thu hồi</h3>
                  <p>
                    Thu hồi {revoking?.name || membershipNames[revoking?.kind]}{" "}
                    tại {revoking?.branchName || "toàn hệ thống"} của{" "}
                    {account.fullName}?
                  </p>
                </>
              )}
              {error && (
                <p className="alert" role="alert">
                  {error}
                </p>
              )}
              <div className="toolbar">
                <button
                  className="primary"
                  disabled={
                    pending ||
                    (command === "role" && (roles.loading || !!roles.error))
                  }
                >
                  {pending
                    ? "Đang xử lý…"
                    : "Xác nhận"}
                </button>
                <button type="button" onClick={() => choose("")}>
                  Hủy
                </button>
              </div>
            </fieldset>
          </form>
        )}
        <section className="account-grants" aria-label="Vai trò đã cấp">
          <h3>Vai trò đã cấp</h3>
          {(detail.data?.roles || []).map((r: Row) => (
            <div className="account-grant" key={r.userRoleId}>
              <div>
                <strong>{r.name}</strong>
                <small>
                  {r.branchName || "Toàn hệ thống"}
                  {r.expiresAt ? ` · Đến ${timestamp(r.expiresAt)}` : ""}
                </small>
              </div>
              {can("ROLE_MANAGE") && (
                <button
                  disabled={pending}
                  aria-label={`Thu hồi vai trò ${r.name}`}
                  onClick={() => choose("revoke-role", r)}
                >
                  Thu hồi
                </button>
              )}
            </div>
          ))}
          {!detail.data?.roles.length && <p>Chưa có vai trò còn hiệu lực.</p>}
        </section>
        <section className="account-grants" aria-label="Phạm vi đã cấp">
          <h3>Phạm vi đã cấp</h3>
          {(detail.data?.memberships || []).map((m: Row) => (
            <div className="account-grant" key={m.membershipId}>
              <div>
                <strong>{membershipNames[m.kind] || "Phạm vi công tác"}</strong>
                <small>
                  {m.branchName}
                  {m.departmentName ? ` · ${m.departmentName}` : ""}
                  {m.isPrimary ? " · Chính" : ""}
                </small>
              </div>
              {can("USER_MANAGE") && (
                <button
                  disabled={pending}
                  aria-label={`Thu hồi ${membershipNames[m.kind]} tại ${m.branchName}`}
                  onClick={() => choose("revoke-membership", m)}
                >
                  Thu hồi
                </button>
              )}
            </div>
          ))}
          {!detail.data?.memberships.length && (
            <p>Chưa có phạm vi còn hiệu lực.</p>
          )}
        </section>
      </State>
    </Dialog>
  );
}
