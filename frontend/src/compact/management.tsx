import { useState } from "react";
import AttendanceSummary from "./attendance-summary";
import { metricClass } from "./metric-tone";
import { Building2, Users, BedDouble } from "lucide-react";
import MetricHeading from "./metric-heading";
import PageTitle from "./page-title";
import { State, useQuery } from "./ui";
import type { Row } from "./api";
import StatChart from "./stat-chart";
import useManagementDetails from "./management-details";
export default function Management() {
  const [revision, bump] = useState(0);
  const query = useQuery("insights/administration", revision);
  const details = useManagementDetails(revision);
  const count = (
    rows: Row[] | undefined,
    test: (r: Row) => boolean = () => true,
  ) => (rows || []).filter(test).reduce((sum, r) => sum + r.count, 0);
  return (
    <>
      <PageTitle>
        <h1>Tổng quan quản trị</h1>
        <p>Theo dõi cơ sở và tài khoản trên toàn hệ thống</p>
      </PageTitle>
      <State {...query} retry={() => bump((n) => n + 1)}>
        <div className="metrics dashboard-metrics">
          {query.data?.branches && (
            <article className={metricClass("ready")}>
              <MetricHeading icon={Building2}>
                Chi nhánh hoạt động
              </MetricHeading>
              <strong className="metric">
                {count(query.data.branches, (r) => r.active)}
              </strong>
              <p>{count(query.data.branches)} chi nhánh trong hệ thống</p>
              <a className="button-link" href="#catalog">
                Quản lý chi nhánh
              </a>
            </article>
          )}
          {query.data?.users && (
            <article className={metricClass("users")}>
              <MetricHeading icon={Users}>Tài khoản hoạt động</MetricHeading>
              <strong className="metric">
                {count(query.data.users, (r) => r.status === "ACTIVE")}
              </strong>
              <p>
                {count(query.data.users, (r) => r.status !== "ACTIVE")} tài
                khoản chưa hoạt động
              </p>
              <a className="button-link" href="#users">
                Quản lý nhân sự
              </a>
            </article>
          )}
          {details.enabled && (
            <article className={metricClass("rooms")}>
              <MetricHeading icon={BedDouble}>Phòng</MetricHeading>
              <State {...details}>
                <strong className="metric" data-stat="rooms">
                  {details.data?.hotels.reduce(
                    (total, hotel) => total + hotel.rooms,
                    0,
                  )}
                </strong>
                <p>Trong danh mục các khách sạn</p>
                <a className="button-link" href="#catalog">
                  Xem cấu trúc khách sạn
                </a>
              </State>
            </article>
          )}
        </div>
      </State>
      <AttendanceSummary />
      {query.data && !query.error && (
        <State {...query}>
        <div className="chart-grid">
          {details.enabled && details.data && (
            <StatChart
              title="Số phòng theo khách sạn"
              rows={details.data.hotels.map((hotel) => ({
                name: hotel.name,
                value: hotel.rooms,
                color: "#34718b",
              }))}
              href="#catalog"
            />
          )}
          {query.data?.users && (
            <StatChart
              title="Trạng thái tài khoản"
              type="donut"
              rows={query.data.users.map((r: Row) => ({
                name:
                  r.status === "ACTIVE"
                    ? "Hoạt động"
                    : r.status === "INACTIVE"
                      ? "Không hoạt động"
                      : r.status === "LOCKED"
                        ? "Đã khóa"
                        : r.status,
                value: r.count,
              }))}
              href="#users"
            />
          )}
        </div>
        </State>
      )}
    </>
  );
}
