import type { Row } from "./api";

export function reportCsv(rows: Row[]) {
  const columns = [...new Set(rows.flatMap(Object.keys))];
  const cell = (value: unknown) => {
    let text = value == null ? "" : String(value);
    if (typeof value === "string" && /^\s*[=+\-@]/.test(text))
      text = "'" + text;
    return `"${text.replaceAll('"', '""')}"`;
  };
  return (
    "\uFEFF" +
    [
      columns.map(cell).join(","),
      ...rows.map((row) => columns.map((key) => cell(row[key])).join(",")),
    ].join("\r\n")
  );
}
export function downloadReportCsv(rows: Row[], name: string) {
  const url = URL.createObjectURL(
    new Blob([reportCsv(rows)], { type: "text/csv;charset=utf-8" }),
  );
  const link = document.createElement("a");
  link.href = url;
  link.download = `${name}.csv`;
  link.click();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}
