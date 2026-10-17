import type { RecordData } from "./domain";
import { request } from "./api/http";
import { endpoints } from "./api/endpoints";
export interface ReportSnapshot {
  snapshotId: string;
  rows: RecordData[];
  snapshot: string;
  key: string;
  from: string;
  to: string;
  columns: string[];
  totalCount: number;
  missingSources?: string[];
}
/** The server exports precisely the preview snapshot and writes the export audit event. */
export async function exportReport(
  preview: ReportSnapshot,
  format: "CSV" | "XLSX" | "PDF",
) {
  if (!preview.snapshotId)
    throw Error("The API must return a report snapshotId before exporting.");
  if (preview.totalCount > 50000)
    throw Error(
      "The report has more than 50,000 rows. Please narrow the filters.",
    );
  const blob = await request<Blob>(
    `${endpoints.reports}/snapshots/${encodeURIComponent(preview.snapshotId)}/export`,
    { method: "POST", body: { format }, responseType: "blob" },
  );
  if (!blob?.size || /json|text\/html/.test(blob.type))
    throw Error("The API did not return a report file.");
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement("a");
  anchor.href = url;
  anchor.download = `hotelops-${preview.key}.${format.toLowerCase()}`;
  anchor.click();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}
