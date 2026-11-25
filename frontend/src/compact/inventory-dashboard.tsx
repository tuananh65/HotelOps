import { useState } from "react";
import AttendanceSummary from "./attendance-summary";
import { metricClass } from "./metric-tone";
import {
  Boxes,
  PackageCheck,
  PackageX,
  TriangleAlert,
  ClipboardList,
  ArrowDownToLine,
  ArrowUpFromLine,
  ArrowLeftRight,
  ClipboardCheck,
  Truck,
  CalendarDays,
} from "lucide-react";
import { useAuth } from "./auth";
import { timestamp, type Row } from "./api";
import { Badge, Empty, State, useQuery } from "./ui";
import PageTitle from "./page-title";
import PageToolbar from "./page-toolbar";
import MetricHeading from "./metric-heading";
import { documentNames, documentHref } from "./inventory-document-model";

export default function InventoryDashboard() {
  const { branch, can } = useAuth();
  const [revision, bump] = useState(0);
  const query = useQuery(`insights/inventory?branchId=${branch}`, revision);
  const data = query.data;
  const metrics = [
    ["trackedItems", "Mặt hàng theo dõi tại chi nhánh", Boxes],
    ["stockedItems", "Mặt hàng có tồn", PackageCheck],
    ["emptyItems", "Mặt hàng hết tồn", PackageX],
    ["lowStock", "Dưới định mức tồn tối thiểu", TriangleAlert],
    ["pendingRequests", "Yêu cầu chờ cấp phát", ClipboardList],
    ["receipts", "Phiếu nhập · 30 ngày", ArrowDownToLine],
    ["issues", "Phiếu xuất · 30 ngày", ArrowUpFromLine],
    ["transfers", "Phiếu chuyển · 30 ngày", ArrowLeftRight],
    ["openStocktakes", "Phiên kiểm kê đang mở", ClipboardCheck],
    ["openReplenishments", "Yêu cầu bổ sung đang mở", Truck],
  ] as const;
  return (
    <>
      <PageTitle>
        <h1>Tổng quan kho</h1>
        <p>Tồn kho và chứng từ trong chi nhánh</p>
      </PageTitle>
      <PageToolbar>
        {can("STOCK_RECEIPT_POST") && (
          <a className="button-link" href="#transactions?kind=RECEIPT">
            Nhập kho
          </a>
        )}
        {can("MR_FULFILL") && (
          <a className="button-link" href="#fulfillment">
            Cấp phát vật tư
          </a>
        )}
        {can("ATTENDANCE_RECORD") && (
          <a className="button-link" href="#workday">
            <CalendarDays size={16} /> Lịch cá nhân
          </a>
        )}
      </PageToolbar>
      <State {...query} retry={() => bump((x) => x + 1)}>
        {data && (
          <>
            <div className="branch-metrics">
              {metrics
                .filter(([key]) => data.metrics[key] !== null)
                .map(([key, name, icon]) => (
                  <article className={metricClass(key)} key={key}>
                    <MetricHeading icon={icon}>{name}</MetricHeading>
                    <strong className="branch-metric-value">
                      {data.metrics[key]}
                    </strong>
                  </article>
                ))}
            </div>
          </>
        )}
      </State>
      <AttendanceSummary />
      {data && !query.error && (
        <State {...query}>
          <>
            <div className="chart-grid inventory-panels">
              <section className="panel">
                <MetricHeading icon={ArrowLeftRight}>
                  Chứng từ nhập / xuất · 30 ngày
                </MetricHeading>
                {data.timeline.length ? (
                  <div className="inventory-timeline">
                    {data.timeline.map((row: Row) => (
                      <div key={`${row.day}-${row.kind}`}>
                        <span>
                          {new Date(row.day).toLocaleDateString("vi-VN")} ·{" "}
                          {documentNames[row.kind]}
                        </span>
                        <meter
                          min={0}
                          max={Math.max(
                            ...data.timeline.map((r: Row) => r.count),
                            1,
                          )}
                          value={row.count}
                        />
                        <strong>{row.count}</strong>
                      </div>
                    ))}
                  </div>
                ) : (
                  <Empty>Chưa có chứng từ nhập / xuất trong 30 ngày.</Empty>
                )}
              </section>
              <section className="panel">
                <MetricHeading icon={TriangleAlert}>
                  Vật tư dưới định mức
                </MetricHeading>
                {data.lowStock.map((row: Row) => (
                  <a
                    className="overview-task"
                    href="#stock"
                    key={`${row.warehouseId}-${row.itemId}`}
                  >
                    <span>
                      {row.item}
                      <small>
                        {row.warehouse} · tối thiểu {row.minimumQty} {row.unit}
                      </small>
                    </span>
                    <strong>
                      {row.availableQty ?? row.baseQty} {row.unit} khả dụng
                    </strong>
                  </a>
                ))}
                {!data.lowStock.length && (
                  <Empty>Không có vật tư dưới định mức đã cấu hình.</Empty>
                )}
              </section>
              {data.pending && (
                <section className="panel">
                  <MetricHeading icon={ClipboardList}>
                    Yêu cầu chờ cấp phát
                  </MetricHeading>
                  {data.pending.map((row: Row) => (
                    <a
                      className="overview-task"
                      href={documentHref(row)}
                      key={row.documentId}
                    >
                      <span>
                        {row.documentNo}
                        <small>{timestamp(row.createdAt)}</small>
                      </span>
                      <Badge value={row.status} />
                    </a>
                  ))}
                  {!data.pending.length && <Empty />}
                </section>
              )}
              <section className="panel">
                <MetricHeading icon={PackageCheck}>
                  Giao dịch gần đây
                </MetricHeading>
                {data.recent.map((row: Row) => (
                  <a
                    className="overview-task"
                    href={documentHref(row)}
                    key={row.documentId}
                  >
                    <span>
                      {documentNames[row.kind]}
                      <small>
                        {row.documentNo} · {timestamp(row.postedAt)}
                      </small>
                    </span>
                    <Badge value={row.status} />
                  </a>
                ))}
                {!data.recent.length && <Empty />}
              </section>
            </div>
          </>
        </State>
      )}
    </>
  );
}
