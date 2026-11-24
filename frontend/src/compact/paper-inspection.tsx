import { AlertPopup } from "./alert-popup";
import { useRef, useState } from "react";
import { api, message, type Row } from "./api";
import { useAuth } from "./auth";
import { Dialog } from "./ui";

export default function PaperInspection({ work, related, reviews, saved }: {
  work: Row; related: Row[]; reviews: Row[]; saved: () => void;
}) {
  const { can } = useAuth();
  const [open, setOpen] = useState(false), [result, setResult] = useState("PASS"),
    [pending, setPending] = useState(false), [error, setError] = useState("");
  const lock = useRef(false), key = useRef(crypto.randomUUID());
  const activeInspection = related.find(r => r.kind === "INSPECTION" && !["PASSED", "FAILED", "CANCELLED"].includes(r.status));
  const findings = reviews.flatMap(r => r.findings || []).filter(f => f.status === "OPEN");
  const reworks = related.filter(r => r.kind === "REWORK" && !["COMPLETED", "FAILED", "CANCELLED"].includes(r.status));
  const lastFailure = Math.max(0, ...reviews.filter(r => r.result === "FAIL").map(r => new Date(r.verifiedAt).getTime()));
  const ready = reworks.every(r => r.status === "PENDING_REINSPECTION") &&
    (work.status !== "REWORK_REQUIRED" || reworks.some(r => r.completedAt && new Date(r.completedAt).getTime() > lastFailure));
  if (!can("INSPECTION_PERFORM") || !ready) return null;
  return <>
    <button className="primary" onClick={() => { setOpen(true); setError(""); setResult("PASS"); key.current = crypto.randomUUID(); }}>Xác nhận hoàn thành</button>
    {open && <Dialog title="Ghi kết quả kiểm tra phòng" close={() => !pending && setOpen(false)}>
      <p>Kiểm tra thực tế theo checklist giấy, sau đó ghi kết quả trên hệ thống.</p>
      <form onSubmit={async e => {
        e.preventDefault(); if (lock.current) return;
        lock.current = true; setPending(true); setError("");
        const data = new FormData(e.currentTarget);
        try {
          await api(`work/${work.workId}/inspection-result`, {
            rowVersion: work.rowVersion, inspectionRowVersion: activeInspection?.rowVersion || null,
            operationKey: key.current, result, note: String(data.get("note") || "").trim() || null,
            resolveFindings: data.get("resolveFindings") === "on",
          });
          setOpen(false); saved();
        } catch (err) { setError(message(err)); }
        finally { lock.current = false; setPending(false); }
      }}>
        <label>Kết quả<select aria-label="Kết quả" value={result} disabled={pending} onChange={e => setResult(e.target.value)}>
          <option value="PASS">Đạt</option><option value="FAIL">Không đạt — cần làm lại</option>
        </select></label>
        {result === "PASS" && findings.length > 0 && <>
          <p>Các lỗi cần xác nhận đã khắc phục:</p>
          <ul>{findings.map((f, i) => <li key={i}>{f.description}</li>)}</ul>
          {can("DEFECT_RESOLVE") ? <label><input type="checkbox" name="resolveFindings" required disabled={pending} /> Tôi đã kiểm tra và xác nhận các lỗi trên đã được khắc phục.</label>
            : <p role="alert">Bạn chưa có quyền xác nhận khắc phục lỗi. Người có quyền cần xử lý lỗi trước khi xác nhận Đạt.</p>}
        </>}
        <label>Ghi chú{(result === "FAIL" || findings.length > 0) ? " (bắt buộc, ít nhất 10 ký tự)" : " (không bắt buộc)"}
          <textarea name="note" disabled={pending} maxLength={2000} minLength={result === "FAIL" || findings.length > 0 ? 10 : undefined} required={result === "FAIL" || findings.length > 0} />
        </label>
        {error && <AlertPopup>{error}</AlertPopup>}
        <button className="primary" disabled={pending || (result === "PASS" && findings.length > 0 && !can("DEFECT_RESOLVE"))}>{pending ? "Đang lưu…" : "Lưu kết quả"}</button>
      </form>
    </Dialog>}
  </>;
}
