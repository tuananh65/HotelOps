import { ClipboardList, Wrench, AlertTriangle } from "lucide-react";
import { metricClass } from "./metric-tone";
import { timestamp, type Row } from "./api";
import AssetConditionBar from "./asset-condition-bar";

const names: Record<string, string> = {
  CLEANING: "Dọn phòng",
  INSPECTION: "Kiểm tra phòng",
  REWORK: "Làm lại",
  MAINTENANCE: "Bảo trì",
  INCIDENT: "Sự cố",
  INTERNAL_REQUEST: "Yêu cầu nội bộ",
};
export default function OperationsMetrics({ data }: { data: Row }) {
  const metrics: Row[] = data.monitoring || [];
  return (
    <>
      <div className="branch-metrics">
        {metrics.map((m) => {
          const Icon =
            m.kind === "INCIDENT"
              ? AlertTriangle
              : m.kind === "MAINTENANCE"
                ? Wrench
                : ClipboardList;
          return (
            <article className={metricClass(m.overdue > 0 ? "overdue" : m.kind)} key={m.kind}>
              <h3>
                <Icon size={20} aria-hidden="true" /> {names[m.kind]}
              </h3>
              <strong className="branch-metric-value">{m.active}</strong>
              <p>Đang xử lý · {m.overdue} quá hạn</p>
              <a href={`#work?kind=${m.kind}`}>Xem danh sách →</a>
            </article>
          );
        })}
      </div>
    </>
  );
}

export function OperationsMetricDetails({ data }: { data: Row }) {
  const attention: Row[] = [...(data.attention || [])].sort((a, b) =>
    String(a.dueAt).localeCompare(String(b.dueAt)),
  );
  return (
    <>
      <div className="chart-grid operations-metrics-details">
        {data.assets != null && (
          <AssetConditionBar rows={data.assets} href="#assets" />
        )}
        {attention.length > 0 && (
          <section className="panel">
            <h2>
              <AlertTriangle size={20} aria-hidden="true" /> Cần chú ý
            </h2>
            <p className="hint">
              Tối đa 5 công việc quá hạn lâu nhất mỗi nhóm.
            </p>
            <div className="table-scroll">
              <table className="mobile-records">
                <thead>
                  <tr>
                    <th>Công việc</th>
                    <th>Nhóm</th>
                    <th>Hạn xử lý</th>
                    <th>Thao tác</th>
                  </tr>
                </thead>
                <tbody>
                  {attention.map((w) => (
                    <tr key={w.workId}>
                      <td data-label="Công việc">
                        {w.workNo} · {w.title}
                      </td>
                      <td data-label="Nhóm">{names[w.kind]}</td>
                      <td data-label="Hạn xử lý">{timestamp(w.dueAt)}</td>
                      <td data-label="Thao tác">
                        <a
                          className="button-link"
                          href={`#work?kind=${w.kind}&id=${w.workId}`}
                        >
                          Chi tiết
                        </a>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </section>
        )}
      </div>
    </>
  );
}
