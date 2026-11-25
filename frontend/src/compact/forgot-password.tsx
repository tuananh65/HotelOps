import { useState } from "react";
import { UserRound } from "lucide-react";
import { ApiError } from "./api";
import { accountMessage } from "./account-policy";
import { AlertPopup } from "./alert-popup";
import { Dialog } from "./ui";

export default function ForgotPassword({ close }: { close: () => void }) {
  const [username, setUsername] = useState("");
  const [pending, setPending] = useState(false);
  const [done, setDone] = useState(false);
  const [error, setError] = useState("");
  return <Dialog title="Quên mật khẩu" className="login-recovery-dialog" close={() => { if (!pending) close(); }}>
    {done ? <>
      <p role="status">Nếu tài khoản có email hợp lệ, hướng dẫn đặt lại mật khẩu sẽ được gửi đến email đã đăng ký.</p>
      <p>Kiểm tra cả thư rác. Nếu không nhận được email, hãy liên hệ quản trị khách sạn.</p>
      <button className="primary" onClick={close}>Quay lại đăng nhập</button>
    </> : <form onSubmit={async event => {
      event.preventDefault(); if (pending) return;
      setPending(true); setError("");
      try {
        const response = await fetch("/api/v2/invitations/forgot-password", {
          method: "POST", headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ username: username.trim() }),
        });
        const result = await response.json().catch(() => null);
        if (!response.ok) throw new ApiError(response.status, result?.title || `HTTP_${response.status}`);
        setDone(true);
      } catch (failure) { setError(accountMessage(failure)); }
      finally { setPending(false); }
    }}>
      {/* Owner 25/11 14:11: recovery accepts Username only, matching login. */}
      <p>Nhập tên đăng nhập để nhận liên kết đặt lại mật khẩu qua email đã đăng ký.</p>
      <label htmlFor="recovery-username">Tên đăng nhập
      <span className="login-input-wrap">
        <UserRound aria-hidden="true" size={20} />
        <input id="recovery-username" autoComplete="username" placeholder="Nhập tên đăng nhập" required maxLength={100} value={username} onChange={event => setUsername(event.target.value)} disabled={pending} />
      </span>
      </label>
      <small className="recovery-expiry">Liên kết có hiệu lực trong 30 phút.</small>
      {error && <AlertPopup>{error}</AlertPopup>}
      <div className="recovery-actions">
        <button type="button" className="danger" disabled={pending} onClick={close}>Hủy</button>
        <button className="primary" disabled={pending || !username.trim()}>{pending ? "Đang gửi…" : "Gửi liên kết đặt lại"}</button>
      </div>
    </form>}
  </Dialog>;
}
