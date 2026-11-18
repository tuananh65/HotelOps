import { useEffect, useRef, useState } from "react";
import { Link, useLocation, useNavigate } from "react-router-dom";
import { useAuth } from "../../auth/AuthProvider";
import type { AuthError } from "../../auth/authStore";

function logoutMessage(error: AuthError): string {
  if (error.kind === "busy")
    return "Một thao tác xác thực khác đang chạy. Chưa xác nhận đăng xuất.";
  if (error.kind === "stale")
    return "Trạng thái xác thực đã đổi. Kiểm tra phiên đăng nhập; chưa xác nhận đăng xuất.";
  if (error.kind === "http" && error.status === 401)
    return "Phiên không hợp lệ. Chưa xác nhận đăng xuất trên máy chủ. Kiểm tra phiên.";
  if (
    error.kind === "http" &&
    error.status === 400 &&
    error.code === "CSRF_VALIDATION_FAILED"
  )
    return "Chưa hoàn tất kiểm tra bảo mật. Chưa xác nhận đăng xuất. Kiểm tra phiên.";
  return "Chưa xác nhận đăng xuất. Xác thực chưa khả dụng. Kiểm tra phiên trước khi thử lại.";
}

export default function ProfileScreen() {
  const { state, actions } = useAuth();
  const nav = useNavigate();
  const location = useLocation();
  const [busy, setBusy] = useState(false);
  const [identityHidden, setIdentityHidden] = useState(false);
  const [error, setError] = useState("");
  const submitting = useRef(false);
  const mounted = useRef(false);
  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
    };
  }, []);

  async function logout() {
    if (submitting.current || state.pending !== null) return;
    submitting.current = true;
    setBusy(true);
    setIdentityHidden(true);
    setError("");
    try {
      const result = await actions.logout();
      if (!mounted.current) return;
      // Accepted AuthApi only returns successful logout for HTTP 204.
      if (result.ok) nav("/login");
      else setError(logoutMessage(result.error));
    } catch {
      if (mounted.current)
        setError(
          "Chưa xác nhận đăng xuất. Xác thực chưa khả dụng. Kiểm tra phiên trước khi thử lại.",
        );
    } finally {
      submitting.current = false;
      if (mounted.current) setBusy(false);
    }
  }

  const pending = busy || state.pending !== null;
  const session =
    !identityHidden &&
    !pending &&
    (state.status === "AUTHENTICATED" || state.status === "RESTRICTED")
      ? state.session
      : null;
  // Local markup avoids mock/storage initialization through PageHeader/ui/i18n imports.
  // Provider may unmount this component during logout; integration must handle that separately.
  return (
    <>
      <header className="page-header">
        <h1>Hồ sơ cá nhân</h1>
      </header>
      <section className="panel form-panel">
        <span className="eyebrow">HỒ SƠ NHÂN VIÊN</span>
        {session ? (
          <>
            <h2>{session.user.name}</h2>
            {location.state?.passwordChanged === true && !session.mustChangePassword && (
              <p role="status">MSG08: Đã đổi mật khẩu thành công.</p>
            )}
            <dl className="metadata">
              <div>
                <dt>ID</dt>
                <dd>{session.user.id}</dd>
              </div>
              <div>
                <dt>Tên đăng nhập</dt>
                <dd>{session.user.username}</dd>
              </div>
              <div>
                <dt>Phiên hết hạn lúc</dt>
                <dd>
                  <time dateTime={session.expiresAt}>{session.expiresAt}</time>
                </dd>
              </div>
              <div>
                <dt>Cần đổi mật khẩu</dt>
                <dd>{session.mustChangePassword ? "Có" : "Không"}</dd>
              </div>
            </dl>
          </>
        ) : (
          <p role="status">
            {busy
              ? "Đang đăng xuất…"
              : "Chưa có hồ sơ đã xác minh. Kiểm tra phiên đăng nhập."}
          </p>
        )}
        {error && <p role="alert">{error}</p>}
        <div className="form-actions">
          {session && (
            <Link className="secondary" to="/change-password">
              Đổi mật khẩu
            </Link>
          )}
          {(session || identityHidden) && (
            <button
              className="primary"
              type="button"
              onClick={logout}
              disabled={pending}
              aria-busy={pending}
            >
              {pending ? "Đang đăng xuất…" : "Đăng xuất"}
            </button>
          )}
        </div>
      </section>
    </>
  );
}
