import { useEffect, useRef, useState } from "react";
import { useAuth } from "./auth";
import { action, api, ApiError, dateKey, message, timestamp } from "./api";
import { AlertPopup } from "./alert-popup";
import { ConfirmPopup, Dialog, State, useQuery } from "./ui";

// Shift closure remains available after scheduled hours, outside the work calendar.
export default function ShiftClose() {
  const { branch } = useAuth();
  const [open, setOpen] = useState(false), [confirm, setConfirm] = useState(false);
  const [revision, refresh] = useState(0), [pending, setPending] = useState(false);
  const [error, setError] = useState("");
  const locked = useRef(false);
  const query = useQuery(open ? `workday?branchId=${branch}&date=${dateKey()}` : null, revision);
  const shift = query.data?.active;
  useEffect(() => { setOpen(false); setConfirm(false); setError(""); }, [branch]);
  useEffect(() => {
    if (!open) return;
    const timer = setInterval(() => refresh(n => n + 1), 15000);
    return () => clearInterval(timer);
  }, [open]);
  async function endShift() {
    if (locked.current || !shift?.canEnd) return;
    locked.current = true; setPending(true); setError("");
    try {
      await api(`workday/assignments/${shift.assignmentId}/end`, action(shift.rowVersion));
      setConfirm(false);
      window.dispatchEvent(new Event("compact:shift-required"));
    } catch (e) { setError(message(e)); }
    finally { locked.current = false; setPending(false); refresh(n => n + 1); }
  }
  return <>
    <button onClick={() => setOpen(true)}>Kết thúc ca</button>
    {open && <Dialog title="Kết thúc ca và hoàn trả vật tư" className="confirmation-popup" close={() => { if (!pending) { setOpen(false); setConfirm(false); } }}>
      <State {...query} retry={() => refresh(n => n + 1)}>
        {shift ? <>
          <p><strong>{shift.name}</strong> · Từ {timestamp(shift.startedAt)}</p>
          {shift.canEnd ? <button className="primary" disabled={pending} onClick={() => setConfirm(true)}>Xác nhận kết thúc ca</button>
            : <p role="status">{shift.endBlockedReason ? message(new ApiError(409, shift.endBlockedReason)) : "Hãy hoàn tất công việc đang thực hiện trước khi kết thúc ca."}</p>}
        </> : <p>Không có ca đang hoạt động cần kết thúc.</p>}
        {(query.data?.pendingReconciliations ?? []).map((item: {assignmentId:number; name:string}) => <p key={item.assignmentId} role="status">{item.name}: đang chờ tự động hoàn trả vật tư sau 5 phút. Bộ vật tư ca tiếp theo được cấp sau khi hoàn trả xong.</p>)}
      </State>
      {error && <AlertPopup tone="error">{error}</AlertPopup>}
    </Dialog>}
    {confirm && <ConfirmPopup title="Xác nhận kết thúc ca" pending={pending} disabled={!shift?.canEnd} cancel={() => setConfirm(false)} confirm={endShift}>
      <p>Ghi nhận giờ kết thúc thực tế. Vật tư còn giữ được hoàn trả sau 5 phút; yêu cầu bổ sung chưa cấp sẽ dừng. Chỉ nhận bộ ca tiếp theo sau khi hoàn trả xong.</p>
      {error && <AlertPopup tone="error">{error}</AlertPopup>}
    </ConfirmPopup>}
  </>;
}
