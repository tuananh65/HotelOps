import type { RecordData } from "./domain";
import { recordExport } from "./services";
export type ReportSnapshot = {
  rows: RecordData[];
  snapshot: string;
  key: string;
  from: string;
  to: string;
  columns?: string[];
};
function download(data: BlobPart, type: string, filename: string) {
  const url = URL.createObjectURL(new Blob([data], { type }));
  const a = document.createElement("a");
  a.href = url;
  a.download = filename;
  a.click();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}
export async function exportReport(
  preview: ReportSnapshot,
  format: "CSV" | "XLSX" | "PDF",
) {
  if (preview.rows.length > 50000)
    throw Error("Export exceeds 50,000 rows; narrow your filters.");
  const columns = preview.columns || [
    "name",
    "branch",
    "department",
    "status",
    "createdAt",
  ];
  if (format === "CSV") {
    const escape = (v: any) =>
      '"' + String(v ?? "").replaceAll('"', '""') + '"';
    const csv =
      "\uFEFF" +
      [
        ["Snapshot", preview.snapshot],
        ["Period", preview.from, preview.to],
        columns,
        ...preview.rows.map((r) => columns.map((k) => r[k])),
      ]
        .map((row) =>
          row
            .map((v) =>
              escape(typeof v === "string" && /^[=+@\-]/.test(v) ? "'" + v : v),
            )
            .join(","),
        )
        .join("\r\n");
    download(csv, "text/csv;charset=utf-8", `hotelops-${preview.key}.csv`);
  } else if (format === "XLSX") {
    const ExcelJS = await import("exceljs");
    const workbook = new ExcelJS.default.Workbook();
    const sheet = workbook.addWorksheet("HotelOps report");
    sheet.addRow(["Duc Vuong HotelOps", preview.key]);
    sheet.addRow(["Snapshot", preview.snapshot]);
    sheet.addRow(["Period", preview.from, preview.to]);
    sheet.addRow(columns);
    for (const r of preview.rows)
      sheet.addRow(columns.map((k) => String(r[k] ?? "")));
    sheet.columns.forEach((c) => (c.width = 26));
    sheet.getRow(4).font = { bold: true, color: { argb: "FFAD8750" } };
    download(
      await workbook.xlsx.writeBuffer(),
      "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
      `hotelops-${preview.key}.xlsx`,
    );
  } else {
    const { jsPDF } = await import("jspdf");
    const pdf = new jsPDF({ orientation: "landscape" });
    pdf.setFont("times");
    pdf.setFontSize(20);
    pdf.text("Duc Vuong HotelOps · " + preview.key, 14, 18);
    pdf.setFontSize(9);
    pdf.text(
      `Snapshot: ${preview.snapshot} | Period: ${preview.from} - ${preview.to}`,
      14,
      28,
    );
    let y = 40;
    pdf.text(columns.join("  |  "), 14, y);
    y += 8;
    for (const r of preview.rows) {
      const lines = pdf.splitTextToSize(
        columns.map((k) => String(r[k] ?? "")).join("  |  "),
        268,
      );
      if (y + lines.length * 5 > 195) {
        pdf.addPage();
        y = 18;
      }
      pdf.text(lines, 14, y);
      y += lines.length * 5 + 4;
    }
    pdf.save(`hotelops-${preview.key}.pdf`);
  }
  recordExport(
    preview.key,
    format,
    preview.snapshot,
    preview.from,
    preview.to,
    preview.rows.length,
  );
}
