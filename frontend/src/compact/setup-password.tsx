import { useState } from "react";
import { PasswordChecklist, accountMessage } from "./account-policy";
import { AlertPopup } from "./alert-popup";
import { ApiError } from "./api";

export default function SetupPassword() {
  const [token] = useState(() => new URLSearchParams(location.hash.split("?")[1]).get("token") || "");
  const [password, setPassword] = useState("");
  const [confirmation, setConfirmation] = useState("");
  const [pending, setPending] = useState(false);
  const [done, setDone] = useState(false);
  const [error, setError] = useState("");
  return <div className="login-page"><section className="login-card">
    <h1>Thiết lập mật khẩu</h1>
    {done ? <><p>Đã thiết lập mật khẩu. Bạn có thể đăng nhập bằng mật khẩu mới.</p><a href="#">Đăng nhập</a></> :
      <form onSubmit={async e => {
        e.preventDefault(); if (pending) return;
        if (password !== confirmation) { setError("Mật khẩu xác nhận chưa khớp."); return; }
        setPending(true); setError("");
        try {
          // Invitation setup is anonymous, even if another account has a session in this tab.
          const response = await fetch("/api/v2/invitations/setup-password", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ token, newPassword: password }) });
          const result = await response.json().catch(() => null);
          if (!response.ok) throw new ApiError(response.status, result?.title || "INVITATION_INVALID_OR_EXPIRED");
          setPassword(""); setConfirmation(""); setDone(true);
          history.replaceState(null, "", `${location.pathname}#setup-password`);
        } catch (error) { setError(accountMessage(error)); } finally { setPending(false); }
      }}>
        <p>Liên kết chỉ sử dụng một lần. Thời hạn sử dụng được ghi trong email.</p>
        <label>Mật khẩu mới<input type="password" autoComplete="new-password" required minLength={8} maxLength={128} value={password} onChange={e => setPassword(e.target.value)} /></label>
        <PasswordChecklist password={password} />
        <label>Nhập lại mật khẩu<input type="password" autoComplete="new-password" required value={confirmation} onChange={e => setConfirmation(e.target.value)} /></label>
        <button className="primary" disabled={pending || !/^[a-fA-F0-9]{64}$/.test(token)}>{pending ? "Đang xử lý…" : "Lưu mật khẩu"}</button>
        {!/^[a-fA-F0-9]{64}$/.test(token) && <p role="alert">Liên kết không hợp lệ. Liên hệ quản trị để được gửi lại lời mời.</p>}
      </form>}
    {error && <AlertPopup>{error}</AlertPopup>}
  </section></div>;
}
