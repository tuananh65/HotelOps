import { Badge, Empty } from './ui';
import { timestamp, type Row } from './api';
import { metricClass } from './metric-tone';

const names: Record<string, string> = {
  UNASSIGNED_TASKS: 'Vận hành chờ phân công', TASKS_TO_VERIFY: 'Vận hành chờ xác nhận',
  OVERDUE_TASKS: 'Công việc quá hạn', ASSET_HANDOVERS: 'Bàn giao tài sản',
  WORK_HANDOVERS: 'Bàn giao công việc', STOCKTAKE_DIFFERENCES: 'Chênh lệch kiểm kê',
  TRANSFER_APPROVALS: 'Điều chuyển chờ duyệt', REPLENISHMENT_APPROVALS: 'Bổ sung kho chờ xác nhận',
};
const groupLinks: Record<string, string> = {
  UNASSIGNED_TASKS: '#work?kind=OPERATIONAL&status=PENDING_ASSIGNMENT',
  TASKS_TO_VERIFY: '#work?kind=OPERATIONAL&status=PENDING_VERIFICATION',
  OVERDUE_TASKS: '#work?kind=OPERATIONAL', ASSET_HANDOVERS: '#assets',
  WORK_HANDOVERS: '#work?kind=OPERATIONAL', STOCKTAKE_DIFFERENCES: '#reports?type=stocktake-differences',
  TRANSFER_APPROVALS: '#documents?transfer=1', REPLENISHMENT_APPROVALS: '#requests',
};
function href(item: Row) {
  const id = encodeURIComponent(item.id), kind = encodeURIComponent(item.kind);
  if (item.entityKind === 'request') return `#requests?id=${id}`;
  if (item.entityKind === 'asset') return `#assets?id=${id}`;
  if (item.entityKind === 'document') return `#documents?kind=${kind}&id=${id}${item.kind === 'TRANSFER_REQUEST' ? '&transfer=1' : ''}`;
  return `#work?kind=${kind}&id=${id}`;
}
export default function OperationsActionCenter({ data }: { data: Row }) {
  const center = data.actionCenter;
  if (!center) return null;
  return <section className="operations-action-center" aria-label="Công việc cần xử lý">
    <h2>Công việc cần xử lý</h2>
    <div className="metrics">
      {[[center.activeAssets, 'Tài sản đang dùng'], [center.retiredAssets, 'Tài sản ngừng dùng'],
        [center.lowStockItems, 'Vật tư dưới định mức'], [center.transfersInTransit, 'Điều chuyển đang giao']].map(([value, title]) =>
        <article className={metricClass(title === 'Vật tư dưới định mức' ? 'overdue' : title === 'Tài sản ngừng dùng' ? 'blocked' : 'ready')} key={title}><h3>{title}</h3><strong className="metric">{value}</strong></article>)}
    </div>
    <div className="request-lines">{center.groups.map((group: Row) => <details className="panel" key={group.code}>
      <summary>{names[group.code] || group.code} ({group.count})</summary>
      {group.items.length ? <ul>{group.items.map((item: Row) => <li key={`${item.entityKind}-${item.id}`}>
        <a href={href(item)}>{item.title}</a> <Badge value={item.status} />
        <small>{item.canAct ? 'Có thể xử lý' : 'Theo dõi'} · {timestamp(item.occurredAt)}{item.dueAt ? ` · Hạn: ${timestamp(item.dueAt)}` : ''}</small>
      </li>)}</ul> : <Empty>Không có hồ sơ cần xử lý.</Empty>}
      {group.count > group.items.length && <p className="hint">Hiển thị {group.items.length} hồ sơ mới nhất trong tổng số {group.count}.</p>}
      {groupLinks[group.code] && <a href={groupLinks[group.code]}>Mở danh sách →</a>}
    </details>)}</div>
    <small>Cập nhật {timestamp(center.updatedAt)}</small>
  </section>;
}
