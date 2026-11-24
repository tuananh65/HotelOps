import { useEffect, useState } from "react";
import { useAuth } from "./auth";
import { timestamp } from "./api";
import { State, useQuery } from "./ui";

type Summary = {
  timezone: string;
  updatedAt: string;
  periods: {
    period: string;
    from: string;
    to: string;
    completedShifts: number;
    workedHours: number;
    workingShifts: number;
  }[];
};
export default function AttendanceSummary() {
  const { can } = useAuth();
  return can("ATTENDANCE_RECORD") ? <AttendanceSummaryContent /> : null;
}
function AttendanceSummaryContent() {
  const { branch } = useAuth();
  const [revision, refresh] = useState(0);
  const query = useQuery<Summary>(
    `workday/personal-summary?branchId=${branch}`,
    revision,
  );
  useEffect(() => {
    const timer = window.setInterval(() => {
      if (document.visibilityState === "visible") refresh((n) => n + 1);
    }, 30000);
    return () => window.clearInterval(timer);
  }, []);
  const day = (value: string) =>
    new Date(`${value}T12:00:00`).toLocaleDateString("vi-VN");
  return (
    <section
      className="panel attendance-summary"
      aria-label="Thống kê ca làm của tôi"
    >
      <div className="section-title">
        <h2>Ca làm của tôi</h2>
        <a className="button-link" href="#workday">
          Lịch làm việc · Bắt đầu/kết thúc ca
        </a>
      </div>
      <p>
        Số ca đã kết thúc và giờ vào–ra thực tế tại chi nhánh đang chọn. Ca đang
        làm được hiển thị riêng, chưa cộng vào giờ đã làm.
      </p>
      <State {...query} retry={() => refresh((n) => n + 1)}>
        {query.data && (
          <>
            <div className="table-scroll">
              <table className="mobile-records">
                <thead>
                  <tr>
                    <th>Kỳ thống kê</th>
                    <th>Khoảng ngày</th>
                    <th>Ca đã làm</th>
                    <th>Giờ đã làm</th>
                    <th>Ca đang làm</th>
                  </tr>
                </thead>
                <tbody>
                  {query.data.periods.map((p) => (
                    <tr key={p.period}>
                      <th data-label="Kỳ thống kê">{p.period === "WEEK" ? "Tuần này" : "Tháng này"}</th>
                      <td data-label="Khoảng ngày">
                        {day(p.from)} – {day(p.to)}
                      </td>
                      <td data-label="Ca đã làm">
                        <span className="badge shift-ended">
                          {p.completedShifts}
                        </span>
                      </td>
                      <td data-label="Giờ đã làm">
                        {p.workedHours.toLocaleString("vi-VN", {
                          maximumFractionDigits: 2,
                        })}{" "}
                        giờ
                      </td>
                      <td data-label="Ca đang làm">
                        <span className="badge shift-working">
                          {p.workingShifts}
                        </span>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <small>
              Cập nhật {timestamp(query.data.updatedAt)} · {query.data.timezone}
            </small>
          </>
        )}
      </State>
    </section>
  );
}
