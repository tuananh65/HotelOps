import type { Row } from "./api";
import { label } from "./ui";
import StatChart from "./stat-chart";
import { useAuth } from "./auth";

export default function OperationalCharts({ data }: { data: Row }) {
  const { profile } = useAuth();
  const ownerOnly =
    profile.roles.length > 0 && profile.roles.every((r) => r.code === "OWNER");
  const link = (route: string) => (ownerOnly ? undefined : route);
  const rooms: Row[] | null = data.rooms;
  const work: Row[] | null = data.work;
  const stock: Row[] | null = data.stock;
  const readiness = new Map<string, number>();
  rooms?.forEach((r) =>
    readiness.set(r.readiness, (readiness.get(r.readiness) || 0) + r.count),
  );
  const occupied =
    rooms?.filter((r) => r.isOccupied).reduce((n, r) => n + r.count, 0) || 0;
  const total = rooms?.reduce((n, r) => n + r.count, 0) || 0;
  return (
    <div className="chart-grid">
      {rooms !== null && (
        <>
          <StatChart
            title="Tình trạng phòng"
            rows={Array.from(readiness, ([name, value]) => ({
              name: label(name),
              value,
            }))}
            href={link("#rooms")}
          />
          <StatChart
            title="Phòng có khách"
            type="donut"
            rows={[
              { name: "Có khách", value: occupied },
              { name: "Không có khách", value: total - occupied },
            ]}
            href={link("#rooms")}
          />
        </>
      )}
      {work !== null && (
        <StatChart
          title="Công việc vận hành"
          rows={(work || []).map((r) => ({
            name: label(r.status),
            value: r.count,
          }))}
          href={link("#work?kind=OPERATIONAL")}
        />
      )}
      {stock !== null && (
        <StatChart
          title="Số dòng tồn kho theo số dư"
          rows={[
            {
              name: "Số dư dương",
              value: (stock || []).filter((r) => r.baseQty > 0).length,
            },
            {
              name: "Số dư bằng 0",
              value: (stock || []).filter((r) => r.baseQty === 0).length,
            },
            {
              name: "Số dư âm",
              value: (stock || []).filter((r) => r.baseQty < 0).length,
            },
          ]}
          href={link("#stock")}
          link="Kiểm tra tồn kho"
        />
      )}
      {rooms === null && work === null && stock === null && (
        <p className="empty">
          Chưa có dữ liệu thống kê thuộc phạm vi được cấp.
        </p>
      )}
    </div>
  );
}
