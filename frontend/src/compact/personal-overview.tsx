import PageTitle from "./page-title";
import AttendanceSummary from "./attendance-summary";
import { metricClass } from "./metric-tone";
import PageToolbar from "./page-toolbar";
import { useState } from "react";
import {
  ClipboardList,
  Sparkles,
  ClipboardCheck,
  Wrench,
  TriangleAlert,
  MessageSquare,
  RotateCcw,
} from "lucide-react";
import MetricHeading from "./metric-heading";
import { useAuth } from "./auth";
import { fields, type Page } from "./api";
import { Badge, Empty, State, useQuery } from "./ui";
import { kinds, terminal } from "./workflow";
function WorkCard({ kind, name }: { kind: string; name: string }) {
  const { branch } = useAuth();
  const [revision, bump] = useState(0);
  const query = useQuery<Page>(
    `work?branchId=${branch}&kind=${kind}&mine=true&pageSize=100`,
    revision,
  );
  return (
    <article className={metricClass(kind)}>
      <MetricHeading
        icon={
          (
            {
              CLEANING: Sparkles,
              INSPECTION: ClipboardCheck,
              MAINTENANCE: Wrench,
              INCIDENT: TriangleAlert,
              INTERNAL_REQUEST: MessageSquare,
              REWORK: RotateCcw,
            } as Record<string, typeof ClipboardList>
          )[kind] || ClipboardList
        }
      >
        {name} của tôi
      </MetricHeading>
      <State {...query} retry={() => bump((n) => n + 1)}>
        <strong className="metric">{query.data?.total || 0}</strong>
        <p>Công việc do bạn tạo, được giao hoặc đang chờ nhận bàn giao</p>
        {fields(query.data?.items)
          .filter((r) => !terminal.includes(r.status))
          .slice(0, 3)
          .map((r) => (
            <a
              className="overview-task"
              key={r.workId}
              href={`#work?kind=${kind}&id=${r.workId}`}
            >
              <span>{r.title}</span>
              <Badge value={r.status} />
            </a>
          ))}
        <a className="button-link" href={`#work?kind=${kind}`}>
          Mở {name.toLocaleLowerCase()}
        </a>
      </State>
    </article>
  );
}
export default function PersonalOverview() {
  const { profile, can, branch } = useAuth();
  const visible = kinds.filter((k) => can(k[2]));
  return (
    <>
      <div className="page-heading">
        <PageTitle>
          <h1>Tổng quan của tôi</h1>
          <p>
            {profile.user.fields.fullName} ·{" "}
            {profile.branches.find((b) => b.branchId === branch)?.name}
          </p>
        </PageTitle>
      </div>
      <PageToolbar>
        {can("SHIFT_MANAGE", false) && (
          <a className="button-link" href="#workday">
            Ca làm việc của tôi
          </a>
        )}
        {can("ROOM_VIEW") && (
          <a className="button-link" href="#rooms">
            Tình trạng phòng
          </a>
        )}
        {can("INVENTORY_VIEW") && (
          <a className="button-link" href="#stock">
            Xem tồn kho
          </a>
        )}
        {can("MR_VIEW") && (
          <a className="button-link" href="#documents?kind=MATERIAL_REQUEST">
            Yêu cầu vật tư
          </a>
        )}
        {can("REPLENISHMENT_VIEW") && (
          <a className="button-link" href="#documents?kind=REPLENISHMENT">
            Bổ sung vật tư
          </a>
        )}
      </PageToolbar>
      <div className="metrics">
        {visible.map((k) => (
          <WorkCard key={k[0]} kind={k[0]} name={k[1]} />
        ))}
      </div>
      {!visible.length && (
        <Empty>Chọn chức năng phía trên để bắt đầu công việc.</Empty>
      )}
      <AttendanceSummary />
    </>
  );
}
