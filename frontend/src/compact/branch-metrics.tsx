import {
  CalendarDays,
  Users,
  ClipboardList,
  Clock,
  Inbox,
  PackageCheck,
} from "lucide-react";
import type { Row } from "./api";

export default function BranchMetrics({ data }: { data: Row }) {
  const s = data.scheduling,
    o = data.operations;
  const metrics = [
    {
      name: "Nhân sự được phân ca",
      value: s?.assignedStaff,
      icon: Users,
      hint: `Tuần bắt đầu ${s?.weekStart}`,
      href: "#workday",
    },
    {
      name: "Nhân sự đang trong ca",
      value: s?.workingStaff,
      icon: Clock,
      hint: "Ca đã bắt đầu, chưa kết thúc",
      href: "#workday",
    },
    {
      name: "Ca chưa đủ người",
      value: s?.underfilledShifts,
      icon: CalendarDays,
      hint: "Trong tuần hiện tại",
      href: "#workday",
    },
    {
      name: "Công việc chưa kết thúc",
      value: o?.activeTasks,
      icon: ClipboardList,
      href: "#work?kind=OPERATIONAL",
    },
    {
      name: "Công việc quá hạn",
      value: o?.overdueTasks,
      icon: Clock,
      href: "#work?kind=OPERATIONAL",
    },
    {
      name: "Yêu cầu nội bộ chờ xử lý",
      value: o?.pendingRequests,
      icon: Inbox,
      href: "#work?kind=INTERNAL_REQUEST",
    },
    {
      name: "Vật tư chờ phê duyệt",
      value: o?.pendingMaterials,
      icon: PackageCheck,
      hint: "Trong chi nhánh; khả năng duyệt tùy từng yêu cầu",
      href: "#documents?kind=MATERIAL_REQUEST",
    },
  ].filter((m) => m.value !== null && m.value !== undefined);
  if (!metrics.length) return null;
  return (
    <div className="branch-metrics">
      {metrics.map((m) => (
        <article className="panel" key={m.name}>
          <m.icon size={24} aria-hidden="true" />
          <h2>{m.name}</h2>
          <strong className="branch-metric-value">{m.value}</strong>
          {m.hint && <p className="hint">{m.hint}</p>}
          <a href={m.href}>Xem chi tiết →</a>
        </article>
      ))}
    </div>
  );
}
