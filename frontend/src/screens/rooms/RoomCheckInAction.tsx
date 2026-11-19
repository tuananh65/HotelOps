import { useEffect, useRef, useState } from "react";
import { useWorkspacePermissions } from "../housekeeping/useWorkspacePermissions";
import { ApiError, getJson, send } from "../../api/dev1/client";

type Options = { canCheckIn: boolean; version: string; recordedStatus: string; readiness: string };
function decode(value: unknown): Options {
  const o = value as Options;
  if (!o || typeof o.canCheckIn !== "boolean" || !/^[A-F0-9]{64}$/.test(o.version)
    || typeof o.recordedStatus !== "string" || typeof o.readiness !== "string")
    throw new Error("Invalid check-in response");
  return o;
}
export function RoomCheckInAction({ branchId, roomId, roomNumber, onChanged }: {
  branchId: number; roomId: number; roomNumber: string; onChanged: () => void;
}) {
  const access = useWorkspacePermissions(branchId);
  const allowed = access.allows("ROOM_CHECKIN");
  const path = `/branches/${branchId}/rooms/${roomId}/check-in`;
  const [options, setOptions] = useState<{ path: string; value: Options } | null>(null);
  const [error, setError] = useState("");
  const [confirm, setConfirm] = useState(false);
  const [busy, setBusy] = useState(false);
  const [retry, setRetry] = useState(0);
  const flight = useRef(false);
  useEffect(() => {
    setOptions(null); setConfirm(false); setError("");
    if (!allowed) return;
    const controller = new AbortController();
    void getJson(path, controller.signal).then(decode).then(value => {
      if (!controller.signal.aborted) setOptions({ path, value });
    }).catch(() => {
      if (!controller.signal.aborted) setError("Chưa tải được điều kiện nhận khách. Vui lòng tải lại.");
    });
    return () => controller.abort();
  }, [path, allowed, retry]);
  if (!allowed) return null;
  const current = options?.path === path ? options.value : null;
  async function checkIn() {
    if (flight.current || !current?.canCheckIn) return;
    flight.current = true; setBusy(true); setError("");
    try {
      const csrf = await getJson("/auth/csrf") as { headerName: string; csrfToken: string };
      if (csrf.headerName !== "X-CSRF-TOKEN" || typeof csrf.csrfToken !== "string" || !csrf.csrfToken || /[\r\n]/.test(csrf.csrfToken))
        throw new Error("Invalid CSRF");
      const result = await send(path, undefined, { method: "POST", headers: {
        "If-Match": `"${current.version}"`, "X-CSRF-TOKEN": csrf.csrfToken,
      } });
      setOptions({ path, value: decode(result.body) }); setConfirm(false); onChanged();
    } catch (e) {
      setOptions(null); setConfirm(false);
      setError(e instanceof ApiError && [409, 412].includes(e.status)
        ? "Phòng đã thay đổi hoặc không còn đủ điều kiện nhận khách. Hãy tải lại."
        : e instanceof ApiError && e.status === 403 ? "Bạn không còn quyền nhận khách tại chi nhánh này."
        : "Chưa xác nhận được kết quả nhận khách. Hãy tải lại để kiểm tra trước khi thử tiếp.");
    } finally { flight.current = false; setBusy(false); }
  }
  return <section aria-label="Nhận khách">
    {current?.recordedStatus === "OCCUPIED" && <p role="status">Phòng đang có khách.</p>}
    {current?.canCheckIn && (!confirm
      ? <button type="button" className="ds-button ds-button--primary" onClick={() => setConfirm(true)}>Nhận khách</button>
      : <div className="ops-note">
        <p>Xác nhận khách nhận phòng {roomNumber}?</p>
        <div className="form-actions">
          <button type="button" className="ds-button ds-button--primary" disabled={busy} onClick={() => void checkIn()}>{busy ? "Đang ghi nhận…" : "Xác nhận nhận khách"}</button>
          <button type="button" className="ds-button" disabled={busy} onClick={() => setConfirm(false)}>Hủy</button>
        </div>
      </div>)}
    {error && <div role="alert"><p>{error}</p><button type="button" className="ds-button" onClick={() => { setRetry(n => n + 1); onChanged(); }}>Tải lại điều kiện nhận khách</button></div>}
  </section>;
}
