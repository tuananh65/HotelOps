import {
  CalendarDays,
  Users,
  ClipboardList,
  Clock,
  Inbox,
  PackageCheck,
} from "lucide-react";
import type { Row } from "./api";
import { metricClass } from "./metric-tone";

export default function BranchMetrics({ data }: { data: Row }) {
  const s = data.scheduling,
    o = data.operations;
  const metrics = [
    {
      name: "Nhân sự được phân ca",
      metric: "assignedStaff",
      value: s?.assignedStaff,
      icon: Users,
      hint: `Tuần bắt đầu ${s?.weekStart}`,
      href: "#workday",
    },
    {
      name: "Nhân sự đang trong ca",
      metric: "workingStaff",
      value: s?.workingStaff,
      icon: Clock,
      hint: "Ca đã bắt đầu, chưa kết thúc",
      href: "#workday",
    },
    {
      name: "Ca chưa đủ người",
      metric: "underfilledShifts",
      value: s?.underfilledShifts,
      icon: CalendarDays,
      hint: "Trong tuần hiện tại",
      href: "#workday",
    },
    {
      name: "Công việc chưa kết thúc",
      metric: "activeTasks",
      value: o?.activeTasks,
      icon: ClipboardList,
      href: "#work?kind=OPERATIONAL",
    },
    {
      name: "Công việc quá hạn",
      metric: "overdueTasks",
      value: o?.overdueTasks,
      icon: Clock,
      href: "#work?kind=OPERATIONAL",
    },
    {
      name: "Yêu cầu nội bộ chờ xử lý",
      metric: "pendingRequests",
      value: o?.pendingRequests,
      icon: Inbox,
      href: "#work?kind=INTERNAL_REQUEST",
    },
    {
      name: "Vật tư chờ phê duyệt",
      metric: "pendingMaterials",
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
        <article className={metricClass(m.metric)} key={m.name}>
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
