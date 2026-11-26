import { useState } from "react";
import { type Row } from "./api";

export default function ReceiptDraftHeader({ doc, pending, submit }: { doc: Row; pending: boolean; submit: (path: string, body: Row, method?: string) => Promise<void> }) {
  const [supplierName, setSupplierName] = useState(doc.supplierName || "");
  const [reason, setReason] = useState(doc.reason || "");
  return <form onSubmit={e => { e.preventDefault(); if (!pending) void submit(`documents/${doc.documentId}/receipt-draft`, { rowVersion: doc.rowVersion, supplierName, reason }, "PUT"); }}><label>Nhà cung cấp<input maxLength={200} value={supplierName} onChange={e => setSupplierName(e.target.value)} /></label><label>Ghi chú nhập kho<textarea maxLength={2000} value={reason} onChange={e => setReason(e.target.value)} /></label><button disabled={pending}>Lưu thông tin phiếu</button></form>;
}
