import { useState } from "react";
import { api, message, timestamp, type Row } from "./api";
import { useAuth } from "./auth";
import { State, useQuery } from "./ui";
import { AlertPopup } from "./alert-popup";

export default function AccountInvitations({ user, changed }: { user: Row; changed: () => void }) {
  const { can } = useAuth();
  const [revision, setRevision] = useState(0), [pending, setPending] = useState(false), [error, setError] = useState("");
  const query = useQuery(`invitations/users/${user.userId}`, revision);
  const rows: Row[] = query.data?.items || [];
  if (query.data && !rows.length) return null;
  return <section className="form-panel"><h3>Lời mời tài khoản</h3><State {...query}>
    {query.data?.configurationError && <p role="status">Chưa thể gửi email: {query.data.configurationError}. Lời mời được giữ trong hàng đợi.</p>}
    {rows.map(row => <p key={row.invitationId}>
      {row.usedAt ? "Đã thiết lập mật khẩu" : row.revokedAt ? "Đã thu hồi" : row.deliveryStatus === "SENT" ? "Đã gửi email" : row.deliveryStatus === "FAILED" ? "Gửi thất bại, đang chờ thử lại" : "Chờ gửi email"}
      {" · "}{timestamp(row.createdAt)}{" · "}{row.attempts} lần thử
      {row.lastError && <small>{row.lastError}</small>}
    </p>)}
    {can("USER_MANAGE") && rows.length > 0 && !rows[0].usedAt && <button disabled={pending} onClick={async () => {
      setPending(true); setError("");
      try { await api(`invitations/users/${user.userId}/resend`, { userRowVersion: user.rowVersion }); setRevision(x => x + 1); changed(); }
      catch (error) { setError(message(error)); } finally { setPending(false); }
    }}>Gửi lại lời mời</button>}
  </State>{error && <AlertPopup>{error}</AlertPopup>}</section>;
}
