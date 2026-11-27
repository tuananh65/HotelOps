import { useEffect, useRef, useState } from "react";
import { action, api, ApiError, fields, message, sessionToken, timestamp, type Row } from "./api";
import { ConfirmationActions, State, useQuery } from "./ui";
import "./cleaning-completion-report.css";

type DraftPhoto = { key: string; file: File; url: string; attachmentId?: number };
export default function CleaningCompletionReport({ work, pending, busy, saved, cancel, canContinueAfterShift = false, maintenanceOutcome }: {
  maintenanceOutcome?: "complete" | "unrepairable"; work: Row; canContinueAfterShift?: boolean; pending: boolean; busy: (value: boolean) => void; saved: () => void; cancel: () => void;
}) {
  const [photos, setPhotos] = useState<DraftPhoto[]>([]);
  const [error, setError] = useState("");
  const [progress, setProgress] = useState("");
  const locked = useRef(false);
  const draft = useRef<DraftPhoto[]>([]);
  const operationKey = useRef(crypto.randomUUID());
  useEffect(() => () => { draft.current.forEach(photo => URL.revokeObjectURL(photo.url)); }, []);
  function update(next: DraftPhoto[]) { draft.current = next; setPhotos(next); }
  function select(files: FileList | null, replace?: string) {
    if (!files || locked.current) return;
    const selected = Array.from(files);
    if (selected.some(file => !["image/png", "image/jpeg"].includes(file.type) || file.size <= 0 || file.size > 10485760)) {
      setError("Chỉ chọn ảnh PNG hoặc JPEG, mỗi ảnh lớn hơn 0 và không quá 10 MB."); return;
    }
    const added = selected.map(file => ({ key: crypto.randomUUID(), file, url: URL.createObjectURL(file) }));
    const previous = draft.current;
    previous.filter(photo => photo.key === replace).forEach(photo => URL.revokeObjectURL(photo.url));
    update([...previous.filter(photo => photo.key !== replace), ...added]);
    operationKey.current = crypto.randomUUID(); setError("");
  }
  return <form className="action-form" data-cleaning-continuation={canContinueAfterShift ? "true" : undefined} onSubmit={async event => {
    event.preventDefault();
    if (locked.current || pending) return;
    if (!draft.current.length) { setError(maintenanceOutcome ? "Cần ít nhất một ảnh bằng chứng kết quả sửa chữa." : "Cần ít nhất một ảnh phòng sau khi dọn."); return; }
    const note = String(new FormData(event.currentTarget).get("reason") || "").trim();
    if (maintenanceOutcome === "unrepairable" && note.length < 10) { setError("Ghi lý do không thể sửa chữa ít nhất 10 ký tự."); return; }
    locked.current = true; busy(true); setError("");
    try {
      for (const [index, photo] of draft.current.entries()) {
        if (photo.attachmentId) continue;
        setProgress(`Đang tải ảnh ${index + 1}/${draft.current.length}…`);
        const body = new FormData(); body.set("branchId", String(work.branchId)); body.set("workId", String(work.workId)); body.set("file", photo.file);
        const uploaded = await api("evidence", body);
        const attachmentId = Number(uploaded.fields?.attachmentId);
        if (!Number.isSafeInteger(attachmentId) || attachmentId <= 0) throw new Error("Invalid attachment response");
        photo.attachmentId = attachmentId;
      }
      setProgress("Đang gửi báo cáo hoàn thành…");
      await api(`work/${work.workId}/actions/${maintenanceOutcome || "complete"}`, action(work.rowVersion, { operationKey: operationKey.current, reason: note || null, attachmentIds: draft.current.map(photo => photo.attachmentId) }));
      saved();
    } catch (failure) { setError(message(failure)); }
    finally { locked.current = false; busy(false); setProgress(""); }
  }}>
    <p>{maintenanceOutcome === "unrepairable" ? "Không thể sửa chữa: gửi ảnh và kết luận để trưởng đội xác nhận, sau đó chuyển quản lý vận hành xử lý thay thế." : maintenanceOutcome === "complete" ? "Sửa chữa thành công: gửi trực tiếp kết quả lên hệ thống, không chờ trưởng đội duyệt hoàn thành." : null}</p>
    <p>Chọn ảnh bằng chứng và xem trước trước khi gửi báo cáo hoàn thành. Ảnh chỉ được tải lên khi bấm Xác nhận. PNG hoặc JPEG, tối đa 10 MB mỗi ảnh.</p>
    <label>Ảnh hoàn thành (bắt buộc)<input type="file" accept="image/png,image/jpeg" capture={maintenanceOutcome ? "environment" : undefined} multiple disabled={pending} onChange={event => { select(event.target.files); event.target.value = ""; }}/></label>
    <div className="cleaning-photo-grid">{photos.map(photo => <figure key={photo.key}>
      <img src={photo.url} alt={`Ảnh đã chọn: ${photo.file.name}`} />
      <figcaption>Ảnh minh chứng đã chọn</figcaption>
      <div className="cleaning-photo-actions"><label>Thay ảnh<input type="file" accept="image/png,image/jpeg" disabled={pending} onChange={event => { select(event.target.files, photo.key); event.target.value = ""; }}/></label>
      <button type="button" disabled={pending} onClick={() => { URL.revokeObjectURL(photo.url); update(draft.current.filter(item => item.key !== photo.key)); operationKey.current = crypto.randomUUID(); }}>Xóa ảnh</button></div>
    </figure>)}</div>
    {maintenanceOutcome !== "complete" && <label>{maintenanceOutcome === "unrepairable" ? "Lý do không thể sửa chữa (bắt buộc)" : "Ghi chú hoàn thành (không bắt buộc)"}<textarea name="reason" required={maintenanceOutcome === "unrepairable"} minLength={maintenanceOutcome === "unrepairable" ? 10 : undefined} maxLength={2000} disabled={pending}/></label>}
    {progress && <p role="status">{progress}</p>}
    {error && <p role="alert">{error}</p>}
    <ConfirmationActions pending={pending} disabled={!photos.length} cancel={cancel}/>
  </form>;
}

function EvidencePhoto({ file }: { file: Row }) {
  const [url, setUrl] = useState("");
  const [error, setError] = useState("");
  const [attempt, retry] = useState(0);
  useEffect(() => {
    const controller = new AbortController(); let objectUrl = "";
    setUrl(""); setError("");
    (async () => {
      try {
        const response = await fetch(`/api/v2/evidence/${file.attachmentId}`, { signal: controller.signal, headers: { Authorization: `Bearer ${sessionToken()}` } });
        if (!response.ok) { const failure = await response.json().catch(() => null); throw new ApiError(response.status, failure?.title || `HTTP_${response.status}`); }
        const blob = await response.blob();
        if (!controller.signal.aborted) { objectUrl = URL.createObjectURL(blob); setUrl(objectUrl); }
      } catch (failure) { if (!controller.signal.aborted) setError(message(failure)); }
    })();
    return () => { controller.abort(); if (objectUrl) URL.revokeObjectURL(objectUrl); };
  }, [file.attachmentId, attempt]);
  return <figure>{url ? <a href={url} target="_blank" rel="noreferrer"><img src={url} alt={file.originalName || "Ảnh bằng chứng hoàn thành"}/></a> : error ? <p role="alert">{error} <button type="button" onClick={() => retry(value => value + 1)}>Thử lại</button></p> : <p role="status">Đang tải ảnh…</p>}<figcaption>Ảnh minh chứng</figcaption></figure>;
}
export function CleaningReportHistory({ detail }: { detail: Row }) {
  const photos: Row[] = (detail.attachments || []).map((file: Row) => file.fields || file).filter((file: Row) => ["image/png", "image/jpeg"].includes(file.mimeType));
  const reports = fields(detail.events).filter(event => ["COMPLETE", "MAINTENANCE_UNREPAIRABLE"].includes(event.eventType)).map(event => {
    let snapshot: Row = {};
    try { snapshot = typeof event.snapshotJson === "string" ? JSON.parse(event.snapshotJson) : event.snapshotJson || {}; } catch { /* Legacy events may not contain a report snapshot. */ }
    const candidateIds = snapshot.attachmentIds || snapshot.AttachmentIds;
    const ids: number[] = Array.isArray(candidateIds) ? candidateIds : [];
    return { event, photos: photos.filter(photo => ids.includes(photo.attachmentId)) };
  });
  const grouped = new Set(reports.flatMap(report => report.photos.map(photo => photo.attachmentId)));
  const other = photos.filter(photo => !grouped.has(photo.attachmentId));
  return <section className="cleaning-report-history"><h3>Báo cáo ảnh hoàn thành</h3>
    {reports.filter(report => report.photos.length > 0).map(({ event, photos: images }) => <section key={event.workEventId}><h4>{event.eventType === "MAINTENANCE_UNREPAIRABLE" ? "Không thể sửa chữa" : "Lần gửi"} · {timestamp(event.occurredAt || event.createdAt)}</h4>{event.reason && <p>{event.reason}</p>}<div className="cleaning-photo-grid">{images.map(file => <EvidencePhoto key={file.attachmentId} file={file}/>)}</div></section>)}
    {!!other.length && <section><h4>Ảnh đính kèm chưa gắn báo cáo / ảnh lịch sử</h4><div className="cleaning-photo-grid">{other.map(file => <EvidencePhoto key={file.attachmentId} file={file}/>)}</div></section>}
    {!photos.length && <p className="hint">Chưa có ảnh báo cáo hoàn thành.</p>}
  </section>;
}
export function RelatedCleaningReport({ workId }: { workId: number }) {
  const [revision, refresh] = useState(0);
  const detail = useQuery(`work/${workId}`, revision);
  return <State {...detail} retry={() => refresh(value => value + 1)}>{detail.data && <section><h3>{detail.data.work.fields.title}</h3><CleaningReportHistory detail={detail.data}/></section>}</State>;
}
