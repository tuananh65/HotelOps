import { useEffect, useRef, useState, type FormEvent } from "react";
import { Link } from "react-router-dom";
import { useAuth } from "../../auth/AuthProvider";
import type { AuthError } from "../../auth/authStore";

function loginMessage(error: AuthError): string {
  if (error.kind === "busy")
    return "Một thao tác xác thực khác đang chạy. Vui lòng chờ trước khi thử lại.";
  if (error.kind === "stale")
    return "Trạng thái xác thực đã đổi. Kiểm tra phiên đăng nhập trước khi thử lại.";
  if (error.kind === "http") {
    if (error.status === 401 && error.code === "INVALID_CREDENTIALS")
      return "Tên đăng nhập hoặc mật khẩu không đúng.";
    if (error.status === 423 && error.code === "ACCOUNT_LOCKED")
      return error.lockedUntil
        ? `Tài khoản tạm khóa đến ${new Date(error.lockedUntil).toLocaleString()} (giờ địa phương).`
        : "Tài khoản đã khóa. Liên hệ quản trị viên.";
    if (error.status === 403 && error.code === "TEMPORARY_PASSWORD_EXPIRED")
      return "Mật khẩu tạm đã hết hạn. Liên hệ quản trị viên.";
    if (error.status === 409 && error.code === "AUTHENTICATED_SESSION_EXISTS")
      return "Đã có phiên đăng nhập. Kiểm tra phiên trước khi thử lại.";
    if (error.status === 400 && error.code === "VALIDATION_FAILED")
      return "Kiểm tra tên đăng nhập và mật khẩu rồi gửi lại.";
    if (error.status === 400 && error.code === "CSRF_VALIDATION_FAILED")
      return "Chưa hoàn tất kiểm tra bảo mật. Kiểm tra phiên trước khi thử lại.";
  }
  return "Dịch vụ xác thực chưa khả dụng. Chưa xác nhận kết quả đăng nhập. Vui lòng thử lại sau.";
}

export default function SignInScreen() {
  const { state, actions } = useAuth();
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const [missing, setMissing] = useState({ username: false, password: false });
  const [busy, setBusy] = useState(false);
  const submitting = useRef(false);
  const mounted = useRef(false);
  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
    };
  }, []);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (submitting.current || state.pending !== null) return;
    const required = { username: !username.trim(), password: !password.trim() };
    setMissing(required);
    if (required.username || required.password) {
      setError("Cần tên đăng nhập và mật khẩu.");
      return;
    }
    submitting.current = true;
    setBusy(true);
    setError("");
    try {
      const result = await actions.login({
        username: username.trim(),
        password,
      });
      if (!mounted.current) return;
      // LoginRoute owns post-login GET revalidation and safe destination restoration.
      if (!result.ok) setError(loginMessage(result.error));
    } catch {
      // Never render raw exceptions or submitted values, including from a rejected adapter.
      if (mounted.current)
        setError(
          "Dịch vụ xác thực chưa khả dụng. Chưa xác nhận kết quả đăng nhập. Vui lòng thử lại sau.",
        );
    } finally {
      submitting.current = false;
      if (mounted.current) {
        setBusy(false);
        setPassword("");
      }
    }
  }

  const pending = busy || state.pending !== null;
  // Local markup avoids AuthLayout -> ui/services and i18n storage initialization.
  // Shared layout integration is deferred; this component requires an explicit AuthProvider.
  return (
    <div className="auth">
      <aside className="auth-art">
        <span className="eyebrow">KHÁCH SẠN ĐỨC VƯỢNG SÀI GÒN</span>
        <h1>
          Vận hành khách sạn.
          <br />
          Không gian làm việc kết nối.
        </h1>
        <small>BÙI VIỆN / CAO THẮNG</small>
      </aside>
      <main className="auth-panel">
        <Link to="/login" className="auth-wordmark">
          Hotel<span>Ops</span>
        </Link>
        <span className="eyebrow">CỔNG NHÂN VIÊN</span>
        <h1>Đăng nhập</h1>
        <p>Chào mừng bạn trở lại. Đăng nhập để bắt đầu làm việc.</p>
        <form noValidate onSubmit={submit} aria-busy={pending}>
          <div className="form-grid">
            <label htmlFor="login-username">
              Tên đăng nhập hoặc email
              <input
                id="login-username"
                name="username"
                autoComplete="username"
                aria-describedby={error ? "login-error" : undefined}
                aria-invalid={missing.username || undefined}
                maxLength={150}
                required
                value={username}
                onChange={(event) => { setUsername(event.target.value); setMissing(value => ({ ...value, username: false })); }}
                disabled={pending}
              />
              {missing.username && <span>Vui lòng nhập tên đăng nhập hoặc email.</span>}
            </label>
            <label htmlFor="login-password">
              Mật khẩu
              <input
                id="login-password"
                name="password"
                type="password"
                autoComplete="current-password"
                aria-describedby={error ? "login-error" : undefined}
                aria-invalid={missing.password || undefined}
                maxLength={256}
                required
                value={password}
                onChange={(event) => { setPassword(event.target.value); setMissing(value => ({ ...value, password: false })); }}
                disabled={pending}
              />
              {missing.password && <span>Vui lòng nhập mật khẩu.</span>}
            </label>
          </div>
          {error && (
            <p role="alert" id="login-error">
              {error}
            </p>
          )}
          <div className="form-actions">
            <button className="primary" type="submit" disabled={pending}>
              {pending ? "Đang đăng nhập…" : "Đăng nhập"}
            </button>
          </div>
        </form>
        <Link className="forgot" to="/forgot-password">
          Quên mật khẩu?
        </Link>
      </main>
    </div>
  );
}
