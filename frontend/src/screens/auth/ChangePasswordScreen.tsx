import { userMessage } from "../../i18n/userMessage";
import { useEffect, useRef, useState, type FormEvent } from "react";
import { useNavigate } from "react-router-dom";
import { useAuth } from "../../auth/AuthProvider";
import type { AuthError } from "../../auth/authStore";

function unmetPasswordItems(current: string, next: string): string[] {
  // Match server UTF-16 char categories; advisory feedback never grants success.
  const units = next.split("");
  const items: string[] = [];
  if (next.length < 8 || next.length > 256) items.push("Dùng 8–256 ký tự.");
  if (!units.some(c => /\p{Lu}/u.test(c))) items.push("Thêm một chữ cái viết hoa.");
  if (!units.some(c => /\p{Ll}/u.test(c))) items.push("Thêm một chữ cái viết thường.");
  if (!units.some(c => /\p{Nd}/u.test(c))) items.push("Thêm một chữ số.");
  if (!units.some(c => /[\p{P}\p{S}]/u.test(c))) items.push("Thêm một ký tự đặc biệt.");
  if (units.some(c => /\p{Cc}/u.test(c))) items.push("Loại bỏ ký tự điều khiển.");
  if (current === next) items.push("Chọn mật khẩu khác mật khẩu hiện tại.");
  return items;
}

function changePasswordMessage(error: AuthError): string {
  if (error.kind === "busy")
    return "Một thao tác xác thực khác đang chạy. Vui lòng chờ trước khi thử lại.";
  if (error.kind === "stale")
    return "Trạng thái xác thực đã đổi. Chưa xác nhận đổi mật khẩu. Vui lòng kiểm tra phiên đăng nhập.";
  if (error.kind === "http") {
    if (error.status === 400 && error.code === "CURRENT_PASSWORD_INVALID")
      return "Mật khẩu hiện tại không đúng.";
    if (error.status === 400 && error.code === "PASSWORD_POLICY_VIOLATION")
      return "Mật khẩu mới chưa đạt chính sách: 8–256 ký tự gồm chữ hoa, chữ thường, số và ký tự đặc biệt; không có ký tự điều khiển và phải khác mật khẩu hiện tại.";
    if (error.status === 400 && error.code === "VALIDATION_FAILED")
      return "Kiểm tra các ô mật khẩu và xác nhận mật khẩu.";
    if (error.status === 400 && error.code === "CSRF_VALIDATION_FAILED")
      return "Chưa hoàn tất kiểm tra bảo mật. Chưa xác nhận đổi mật khẩu. Kiểm tra phiên đăng nhập.";
    if (error.status === 401)
      return "Phiên không còn hợp lệ. Đăng nhập lại trước khi đổi mật khẩu.";
    if (error.status === 409 && error.code === "AUTH_STATE_CHANGED")
      return "Trạng thái xác thực đã đổi. Chưa xác nhận đổi mật khẩu. Vui lòng kiểm tra phiên đăng nhập.";
  }
  return "Chưa xác nhận đổi mật khẩu. Dịch vụ xác thực chưa khả dụng. Kiểm tra phiên trước khi thử lại.";
}

export default function ChangePasswordScreen() {
  const { state, actions } = useAuth();
  const navigate = useNavigate();
  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmNewPassword, setConfirmNewPassword] = useState("");
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  const submitting = useRef(false);
  const mounted = useRef(false);
  const confirmed =
    state.session !== null &&
    (state.status === "AUTHENTICATED" || state.status === "RESTRICTED");
  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
    };
  }, []);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (submitting.current || state.pending !== null || !confirmed) return;
    if (
      !currentPassword.length ||
      !newPassword.length ||
      !confirmNewPassword.length
    ) {
      setError("Vui lòng nhập đầy đủ các ô mật khẩu.");
      return;
    }
    if (newPassword !== confirmNewPassword) {
      setError("Mật khẩu mới và xác nhận phải giống nhau.");
      return;
    }
    submitting.current = true;
    setBusy(true);
    setError("");
    try {
      const result = await actions.changePassword({
        currentPassword,
        newPassword,
        confirmNewPassword,
      });
      if (!mounted.current) return;
      if (result.ok) navigate("/profile", { state: { passwordChanged: true } });
      else {
        const details = result.error.kind === "http" && result.error.status === 400
          && result.error.code === "PASSWORD_POLICY_VIOLATION"
          ? unmetPasswordItems(currentPassword, newPassword).join(" ") : "";
        setError([changePasswordMessage(result.error), details].filter(Boolean).join(" "));
      }
    } catch {
      if (mounted.current)
        setError(
          "Chưa xác nhận đổi mật khẩu. Dịch vụ xác thực chưa khả dụng. Kiểm tra phiên trước khi thử lại.",
        );
    } finally {
      submitting.current = false;
      if (mounted.current) {
        setBusy(false);
        setCurrentPassword("");
        setNewPassword("");
        setConfirmNewPassword("");
      }
    }
  }

  const pending = busy || state.pending !== null;
  const disabled = pending || !confirmed;
  // Local markup avoids the shared Form/AuthLayout/i18n mock-storage initialization.
  // Provider mutation unmount/recovery is an integration dependency, not solved here.
  return (
    <div className="auth">
      <aside className="auth-art">
        <h1>
          Vận hành khách sạn.
          <br />
          Không gian làm việc kết nối.
        </h1>
      </aside>
      <main className="auth-panel">
        <h1>Đổi mật khẩu</h1>
        <p>Dùng 8–256 ký tự gồm chữ hoa, chữ thường, số và ký tự đặc biệt. Mật khẩu phải khác mật khẩu hiện tại, không có ký tự điều khiển. Máy chủ kiểm tra các yêu cầu này.</p>
        {!confirmed && (
          <p role="status">
            Cần xác minh phiên đăng nhập trước khi đổi mật khẩu.
          </p>
        )}
        <form noValidate onSubmit={submit} aria-busy={pending}>
          <div className="form-grid">
            <label htmlFor="change-current">
              Mật khẩu hiện tại
              <input
                id="change-current"
                name="currentPassword"
                type="password"
                autoComplete="current-password"
                required
                aria-describedby={error ? "change-password-error" : undefined}
                value={currentPassword}
                onChange={(event) => setCurrentPassword(event.target.value)}
                disabled={disabled}
              />
            </label>
            <label htmlFor="change-new">
              Mật khẩu mới
              <input
                id="change-new"
                name="newPassword"
                type="password"
                autoComplete="new-password"
                required
                aria-describedby={error ? "change-password-error" : undefined}
                value={newPassword}
                onChange={(event) => setNewPassword(event.target.value)}
                disabled={disabled}
              />
            </label>
            <label htmlFor="change-confirm">
              Xác nhận mật khẩu mới
              <input
                id="change-confirm"
                name="confirmNewPassword"
                type="password"
                autoComplete="new-password"
                required
                aria-describedby={error ? "change-password-error" : undefined}
                value={confirmNewPassword}
                onChange={(event) => setConfirmNewPassword(event.target.value)}
                disabled={disabled}
              />
            </label>
          </div>
          {error && (
            <p role="alert" id="change-password-error">
              {userMessage(error)}
            </p>
          )}
          <div className="form-actions">
            <button className="primary" type="submit" disabled={disabled}>
              {pending ? "Đang đổi mật khẩu…" : "Đổi mật khẩu"}
            </button>
          </div>
        </form>
      </main>
    </div>
  );
}
