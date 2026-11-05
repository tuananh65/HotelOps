import { Download } from "lucide-react";
import { useState } from "react";
import { StatusChart } from "../../components/StatusChart";
import { t, useLanguage } from "../../i18n";
import {
  generateReport,
  reportTypes as reportDefinitions,
} from "../../report-engine";
import type { ReportSnapshot } from "../../report-export";
import { exportReport } from "../../report-export";
import * as service from "../../services";
import { DataTable, PageHeader, Summary, date, notify, useDB } from "../../ui";
export default function OperationalReportsScreen() {
  useLanguage();
  useDB();
  const [key, setKey] = useState("rooms");
  const [from, setFrom] = useState(
    new Date(Date.now() - 30 * 86400000).toISOString().slice(0, 10),
  );
  const [to, setTo] = useState(new Date().toISOString().slice(0, 10));
  const [preview, setPreview] = useState<ReportSnapshot>(() =>
    generateReport("rooms", from, to),
  );
  const reportTypes = reportDefinitions;
  const [format, setFormat] = useState<"CSV" | "XLSX" | "PDF">("CSV");
  const exportCsv = async () => {
    if (!preview) return;
    try {
      await exportReport(preview, format);
      notify(format + " exported using the preview snapshot.");
    } catch (e) {
      notify((e as Error).message);
    }
  };
  return (
    <>
      <PageHeader id={78}>
        {preview && (
          <button className="primary" onClick={exportCsv}>
            <Download size={16} />
            {t("Export")} {format}
          </button>
        )}
      </PageHeader>
      <section className="panel report-controls">
        <div className="filter-bar">
          <label>
            {t("Format")}
            <select
              aria-label={t("Format")}
              value={format}
              onChange={(e) =>
                setFormat(e.target.value as "CSV" | "XLSX" | "PDF")
              }
            >
              <option>{t("CSV")}</option>
              <option>{t("XLSX")}</option>
              <option>{t("PDF")}</option>
            </select>
          </label>
          <label>
            {t("Report type")}
            <select
              aria-label={t("Report type")}
              value={key}
              onChange={(e) => setKey(e.target.value)}
            >
              {reportTypes.map((k) => (
                <option key={k.key} value={k.key}>
                  {t(k.name)}
                </option>
              ))}
            </select>
          </label>
          <label>
            {t("From")}
            <input
              type="date"
              value={from}
              onChange={(e) => setFrom(e.target.value)}
            />
          </label>
          <label>
            {t("To")}
            <input
              type="date"
              value={to}
              onChange={(e) => setTo(e.target.value)}
            />
          </label>
          <button
            className="primary"
            onClick={() => {
              if (from > to) {
                notify("End date must not precede start date.");
                return;
              }
              try {
                setPreview(generateReport(key, from, to));
              } catch (e) {
                notify((e as Error).message);
              }
            }}
          >
            {t("Generate preview")}
          </button>
        </div>
      </section>
      {preview && (
        <>
          <Summary
            items={[
              { label: "Records in snapshot", value: preview.rows.length },
              { label: "Report type", value: preview.key },
              { label: "Period", value: preview.from + " — " + preview.to },
            ]}
          />
          <StatusChart rows={preview.rows} title="Report distribution" />
          <DataTable
            rows={preview.rows}
            columns={preview.columns?.map((key) => ({
              key,
              label: key.replace(/([A-Z])/g, " $1"),
            }))}
          />
        </>
      )}
    </>
  );
}
