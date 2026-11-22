import { ClipboardList, Wrench, AlertTriangle } from "lucide-react";
import { timestamp, type Row } from "./api";
import { label } from "./ui";
import StatChart from "./stat-chart";

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
  const attention: Row[] = [...(data.attention || [])].sort((a, b) =>
    String(a.dueAt).localeCompare(String(b.dueAt)),
  );
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
            <article className="panel" key={m.kind}>
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
      <div className="chart-grid operations-metrics-details">
        {data.assets != null && (
          <StatChart
            title="Tình trạng tài sản đang sử dụng"
            rows={data.assets.map((a: Row) => ({
              name:
                (
                  {
                    GOOD: "Tốt",
                    FAIR: "Bình thường",
                    POOR: "Kém",
                    FAULTY: "Hỏng",
                    UNDER_REPAIR: "Đang sửa chữa",
                    LOST: "Thất lạc",
                  } as Record<string, string>
                )[a.condition] || label(a.condition),
              value: a.count,
            }))}
            href="#assets"
          />
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
              <table>
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
                      <td>
                        {w.workNo} · {w.title}
                      </td>
                      <td>{names[w.kind]}</td>
                      <td>{timestamp(w.dueAt)}</td>
                      <td>
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
