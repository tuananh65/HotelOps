import { useState } from "react";
import { api, message, type Row } from "./api";
import { Dialog } from "./ui";
import { AlertPopup } from "./alert-popup";

export default function CleaningSupplyReconciliation({ work, saved }: { work: Row; saved: () => void }) {
  const [key, setKey] = useState("");
  const [reason, setReason] = useState("");
  const [pending, setPending] = useState(false);
  const [error, setError] = useState("");
  return <>
    <button onClick={() => { setKey(crypto.randomUUID()); setReason(""); setError(""); }}>Đối soát vật tư công việc</button>
    {key && <Dialog title="Đối soát vật tư công việc" close={() => { if (!pending) setKey(""); }}>
      <p>Giữ chỗ vật tư cho công việc đang dở. Nếu ca cũ chưa được cấp vật tư, hệ thống xuất đủ định mức hiện tại từ kho và lưu định mức cho ca. Không đủ tồn kho hoặc vật tư thì toàn bộ thao tác không được ghi nhận.</p>
      <p>Chỉ xác nhận sau khi nhân viên nhận đúng vật tư thực tế. Thao tác không ghi nhận thay việc sử dụng vật tư trước đây.</p>
      <form onSubmit={async e => {
        e.preventDefault(); setPending(true); setError("");
        try {
          await api(`work/${work.workId}/reconcile-supplies`, { rowVersion: work.rowVersion, operationKey: key, reason });
          setKey(""); saved();
        } catch (ex) { setError(message(ex)); } finally { setPending(false); }
      }}>
        <label>Lý do đối soát<textarea required minLength={10} maxLength={2000} value={reason} disabled={pending} onChange={e => { setReason(e.target.value); setKey(crypto.randomUUID()); }} /></label>
        {error && <AlertPopup>{error}</AlertPopup>}
        <button className="primary" disabled={pending}>Xác nhận đối soát</button>
      </form>
    </Dialog>}
  </>;
}
