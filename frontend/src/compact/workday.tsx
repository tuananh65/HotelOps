import { AlertPopup } from "./alert-popup";
import PageTitle from "./page-title";
import { useEffect, useState } from "react";
import WorkdayAssignment from "./workday-assignment";
import WorkdaySettings from "./workday-settings";
import type { WorkdayCalendar } from "./workday-scheduling";
import { api, action, dateKey, message, timestamp, type Row } from "./api";
import { useAuth } from "./auth";
import { Badge, ConfirmPopup, Dialog, Empty, History, State, useQuery } from "./ui";
const time = (n: number) =>
  `${String(Math.floor(n / 60)).padStart(2, "0")}:${String(n % 60).padStart(2, "0")}`;
const audienceKey = (shift: Row) => `${shift.departmentId}:${shift.roleId}`;
const visibleAssignment = (assignment: Row) => assignment.status !== "CANCELLED" || assignment.expiredWithoutStart === true;
const assignmentStatus = (assignment: Row) => assignment.expiredWithoutStart === true
  ? "Đã kết thúc · chưa vào ca"
  : assignment.status;
function days(start: string) {
  return Array.from({ length: 7 }, (_, i) => {
    const d = new Date(`${start}T12:00:00`);
    d.setDate(d.getDate() + i);
    return dateKey(d);
  });
}
export default function Workday() {
  const { branch, profile } = useAuth();
  const branchRoles = profile.roles.filter(r => r.branchId == null || r.branchId === branch);
  const operationsManager = branchRoles.length > 0 && branchRoles.every(r => r.code === "OPERATION_MANAGER");
  const [date, setDate] = useState(dateKey()),
    [audience, setAudience] = useState(""),
    [revision, bump] = useState(0),
    [slot, setSlot] = useState<Row | null>(null),
    [detail, setDetail] = useState<Row | null>(null),
    [pending, setPending] = useState(false),
    [error, setError] = useState(""),
    [notice, setNotice] = useState("");
  const [settings, setSettings] = useState(false);
  const [showHistorical, setShowHistorical] = useState(false);
  const [ending, setEnding] = useState(false);
  useEffect(() => {
    const timer = window.setInterval(() => {
      if (!pending && !slot && !detail && document.visibilityState === "visible") bump((n) => n + 1);
    }, 30000);
    return () => window.clearInterval(timer);
  }, [pending, slot, detail]);
  useEffect(() => {
    setSlot(null);
    setDetail(null);
    setSettings(false);
    setEnding(false);
    setError("");
    setNotice("");
  }, [branch]);
  const query = useQuery(`workday?branchId=${branch}&date=${date}`, revision),
    data = query.data;
  const calendar = useQuery<WorkdayCalendar>(
    `workday/calendar?branchId=${branch}&year=${date.slice(0, 4)}`,
  );
  const audiences = [
    ...new Map<string, string>(
      (data?.shifts ?? []).filter((s: Row) => !data?.canManage || s.isActive || showHistorical).map((s: Row) => [
        audienceKey(s),
        `${s.role} · ${s.department}`,
      ]),
    ),
  ].sort((a, b) => a[1].localeCompare(b[1], "vi"));
  const selectedAudience = audiences.some(([key]) => key === audience)
    ? audience
    : audiences[0]?.[0] || "";
  const visibleShifts = (data?.shifts ?? []).filter(
    (s: Row) => audienceKey(s) === selectedAudience && (!data?.canManage || s.isActive || showHistorical),
  );
  const history = useQuery<Row[]>(
    detail ? `workday/assignments/${detail.assignmentId}/history` : null,
    revision,
  );
  async function mutate(path: string, body: Row) {
    if (pending) return;
    setPending(true);
    setError("");
    setNotice("");
    try {
      await api(path, body);
      setSlot(null);
      setDetail(null);
      setEnding(false);
      setNotice("Đã cập nhật lịch làm việc.");
      bump((x) => x + 1);
    } catch (e) {
      setError(message(e));
      bump((x) => x + 1);
    } finally {
      setPending(false);
    }
  }
  const jump = (n: number) => {
    const d = new Date(`${date}T12:00:00`);
    d.setDate(d.getDate() + n);
    setDate(dateKey(d));
  };
  const mine = (a: Row) => a.userId === profile.user.fields.userId;
  const dailyAssignments = (data?.assignments ?? []).filter(
    (a: Row) => visibleAssignment(a) && a.workDate === date,
  );
  if (settings && data?.canManage)
    return (
      <WorkdaySettings
        close={() => {
          setSettings(false);
          bump((x) => x + 1);
        }}
      />
    );
  return (
    <>
      <div className="page-heading workday-controls">
        <PageTitle>
          <h1>Lịch làm việc</h1>
          <p>Phân ca theo tuần · Ca tự kết thúc khi hết giờ; vật tư còn giữ được đối soát riêng</p>
        </PageTitle>
        {data?.canManage && (
          <label className="workday-audience">
            Đối tượng
            <select
              value={selectedAudience}
              disabled={!audiences.length}
              onChange={(e) => {
                setAudience(e.target.value);
                setSlot(null);
                setDetail(null);
              }}
            >
              {!audiences.length && <option value="">Chưa có đối tượng</option>}
              {audiences.map(([key, name]) => (
                <option key={key} value={key}>
                  {name}
                </option>
              ))}
            </select>
          </label>
        )}
        <div className="toolbar">
          {data?.canManage && (
            <button onClick={() => { if (operationsManager) location.hash = "configuration?tab=shifts"; else setSettings(true); }}>Cấu hình ca</button>
          )}
          <button disabled={query.loading} onClick={() => jump(data?.canManage ? -7 : -1)} aria-label={data?.canManage ? "Tuần trước" : "Ngày trước"}>
            ← {data?.canManage ? "Tuần trước" : "Ngày trước"}
          </button>
          <input
            id="week-date"
            aria-label={data?.canManage ? "Ngày trong tuần" : "Ngày làm việc"}
            type="date"
            value={date}
            onChange={(e) => setDate(e.target.value || dateKey())}
          />
          <button disabled={query.loading} onClick={() => jump(data?.canManage ? 7 : 1)} aria-label={data?.canManage ? "Tuần sau" : "Ngày sau"}>
            {data?.canManage ? "Tuần sau" : "Ngày sau"} →
          </button>
          <button onClick={() => setDate(data?.today || dateKey())}>
            Hôm nay
          </button>
        </div>
      </div>
      {error && (
        <AlertPopup>
          {error}
        </AlertPopup>
      )}
      {notice && (
        <AlertPopup tone="success">
          {notice}
        </AlertPopup>
      )}
      {calendar.error && (
        <AlertPopup>
          Chưa tải được mốc lễ. {message(calendar.error)}
        </AlertPopup>
      )}
      {data?.canManage && calendar.data?.notice && <p className="muted">{calendar.data.notice}</p>}
      {data?.canManage && <label className="checkbox-label"><input type="checkbox" checked={showHistorical} onChange={e=>setShowHistorical(e.target.checked)}/>Hiện lịch sử ca đã ngừng sử dụng</label>}
      <State {...query} retry={() => bump((x) => x + 1)}>
        {data && (
          <>
            {data.active && (
              <section className="active-shift shift-working">
                <div>
                  <strong>{data.active.status === "ENDED" ? "Ca đã kết thúc, chờ đối soát" : "Ca đang hoạt động"}: {data.active.name}</strong>
                  <p>
                    Từ {timestamp(data.active.startedAt)} · Thời gian công việc
                    được quản lý riêng.
                  </p>
                </div>
                {data.active.canEnd && <button
                  className="primary"
                  disabled={pending}
                  onClick={() => setEnding(true)}
                >
                  {data.active.status === "ENDED" ? "Đối soát và hoàn trả vật tư" : "Kết thúc ca"}
                </button>}
                {!data.active.canEnd && <p role="status">Hoàn tất hoặc bàn giao công việc, xử lý vật tư giữ chỗ và yêu cầu bổ sung còn mở để đối soát vật tư.</p>}
              </section>
            )}
            {ending && data.active && <ConfirmPopup title={data.active.status === "ENDED" ? "Đối soát vật tư cuối ca" : "Kết thúc ca"} disabled={!data.active.canEnd} pending={pending} cancel={()=>setEnding(false)} confirm={()=>{if(data.active.canEnd)mutate(`workday/assignments/${data.active.assignmentId}/end`,action(data.active.rowVersion));}}><p>Xác nhận hoàn trả vật tư chưa sử dụng về kho. Ca đã hết giờ vẫn giữ nguyên thời điểm kết thúc.</p>{error&&<AlertPopup>{error}</AlertPopup>}</ConfirmPopup>}
            <div className="section-title">
              <h2>
                {data.canManage
                  ? "Lịch theo đối tượng"
                  : "Công việc"}
              </h2>
              <span>
                {data.canManage ? "Tuần bắt đầu " : "Ngày "}
                {new Date((data.canManage ? data.weekStart : date) + "T12:00:00").toLocaleDateString(
                  "vi-VN",
                )}
              </span>
            </div>
            {data.canManage && !visibleShifts.length ? (
              <Empty>Chưa có ca làm việc cho đối tượng đã chọn.</Empty>
            ) : data.canManage ? (
              <div className="calendar-scroll">
                <table className="calendar">
                  <thead>
                    <tr>
                      <th>Nhóm / ca</th>
                      {days(data.weekStart).map((d) => (
                        <th key={d} className={d === data.today ? "today" : ""}>
                          {new Date(d + "T12:00:00").toLocaleDateString(
                            "vi-VN",
                            {
                              weekday: "short",
                              day: "2-digit",
                              month: "2-digit",
                            },
                          )}
                          {calendar.data?.markers
                            .filter((m) => m.date === d)
                            .map((m) => (
                              <small
                                key={m.name}
                                className="workday-calendar-marker"
                              >
                                {m.name}
                              </small>
                            ))}
                        </th>
                      ))}
                    </tr>
                  </thead>
                  <tbody>
                    {visibleShifts.map((s: Row) => (
                      <tr key={s.shiftId}>
                        <th>
                          <strong>{s.name}</strong>
                          {!s.isActive && <small>Đã ngừng sử dụng · chỉ xem lịch sử</small>}
                          <small>
                            {time(s.startMinute)}–{time(s.endMinute)} ·{" "}
                            {s.capacity} người
                          </small>
                        </th>
                        {days(data.weekStart).map((d) => {
                          const rows = data.assignments.filter(
                            (a: Row) =>
                              a.shiftId === s.shiftId &&
                              a.workDate === d &&
                              visibleAssignment(a),
                          );
                          const occupied = rows.filter((a: Row) => a.status !== "CANCELLED").length;
                          return (
                            <td
                              key={d}
                              data-label={new Date(d + "T12:00:00").toLocaleDateString("vi-VN", { weekday: "long", day: "2-digit", month: "2-digit" })}
                              className={d === data.today ? "today" : ""}
                            >
                              <span className="capacity">
                                {occupied}/{s.capacity}
                              </span>
                              {rows.map((a: Row) => (
                                <button
                                  className="shift-person"
                                  key={a.assignmentId}
                                  onClick={() => {
                                    setError("");
                                    setDetail(a);
                                  }}
                                >
                                  <strong>{a.fullName}</strong>
                                  <Badge value={assignmentStatus(a)} />
                                </button>
                              ))}
                              {s.isActive && d === data.today && s.endMinute <= data.currentMinute && <small>Ca đã hết giờ</small>}
                              {(d > data.today || (d === data.today && s.endMinute > data.currentMinute)) &&
                                s.isActive &&
                                occupied < s.capacity && (
                                  <button
                                    className="add-shift"
                                    aria-label={`Phân ${s.name} ngày ${d}`}
                                    onClick={() => {
                                      setError("");
                                      setSlot({ ...s, date: d, currentMinute: data.currentMinute });
                                    }}
                                  >
                                    + Phân ca
                                  </button>
                                )}
                            </td>
                          );
                        })}
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            ) : (
              <div className="shift-list">
                {dailyAssignments
                  .map((a: Row) => {
                    const s = data.shifts.find(
                      (s: Row) => s.shiftId === a.shiftId,
                    );
                    return (
                      <article className="panel" key={a.assignmentId}>
                        <div>
                          <span className="eyebrow">
                            {new Date(
                              a.workDate + "T12:00:00",
                            ).toLocaleDateString("vi-VN", {
                              weekday: "long",
                              day: "2-digit",
                              month: "2-digit",
                            })}
                          </span>
                          <h3>{s?.name}</h3>
                          <p>
                            {time(s.startMinute)}–{time(s.endMinute)}
                          </p>
                          <Badge value={assignmentStatus(a)} />
                          {calendar.data?.markers
                            .filter((m) => m.date === a.workDate)
                            .map((m) => (
                              <small
                                key={m.name}
                                className="workday-calendar-marker"
                              >
                                {m.name}
                              </small>
                            ))}
                        </div>
                        <div className="toolbar">
                          {a.status === "PLANNED" &&
                            a.workDate === data.today &&
                            data.currentMinute >= s.startMinute && data.currentMinute < s.endMinute &&
                            !data.active && (
                              <button
                                className="primary"
                                disabled={pending}
                                onClick={() =>
                                  mutate(
                                    `workday/assignments/${a.assignmentId}/start`,
                                    action(a.rowVersion),
                                  )
                                }
                              >
                                Bắt đầu ca
                              </button>
                            )}
                          <button onClick={() => setDetail(a)}>Chi tiết</button>
                        </div>
                      </article>
                    );
                  })}
                {!dailyAssignments.length && (
                  <Empty>Bạn chưa được phân ca trong ngày này.</Empty>
                )}
              </div>
            )}
          </>
        )}
      </State>
      {slot && (
        <WorkdayAssignment
          key={`${branch}:${slot.shiftId}:${slot.date}`}
          slot={slot}
          today={data.today}
          branch={branch}
          close={() => setSlot(null)}
          invalidate={() => bump((x) => x + 1)}
          saved={(count) => {
            setSlot(null);
            setNotice(`Đã tạo ${count} lượt phân công.`);
            bump((x) => x + 1);
          }}
        />
      )}
      {detail && (
        <Dialog
          title="Chi tiết ca làm việc"
          close={() => {
            if (!pending) setDetail(null);
          }}
        >
          <h3>{detail.fullName}</h3>
          <p>
            {detail.workDate} · <Badge value={assignmentStatus(detail)} />
          </p>
          {detail.expiredWithoutStart === true && <p>Ca đã quá giờ kết thúc 1 giờ và được hệ thống đóng tự động. Nhân viên chưa vào ca; không ghi nhận thời gian làm việc.</p>}
          <p>
            Bắt đầu: {timestamp(detail.startedAt)}
            <br />
            Kết thúc: {timestamp(detail.endedAt)}
          </p>
          {mine(detail) &&
            detail.status === "PLANNED" &&
            detail.workDate === data?.today &&
            !data?.active && (
              <button
                className="primary"
                disabled={pending}
                onClick={() =>
                  mutate(
                    `workday/assignments/${detail.assignmentId}/start`,
                    action(detail.rowVersion),
                  )
                }
              >
                Bắt đầu ca
              </button>
            )}
          {data?.canManage && detail.status === "PLANNED" && (
            <form
              onSubmit={(e) => {
                e.preventDefault();
                const f = new FormData(e.currentTarget);
                mutate(
                  `workday/assignments/${detail.assignmentId}/cancel`,
                  action(detail.rowVersion, { reason: f.get("reason") }),
                );
              }}
            >
              <label>
                Lý do hủy ca
                <textarea
                  name="reason"
                  required
                  minLength={10}
                  maxLength={2000}
                />
              </label>
              <button disabled={pending}>Hủy phân công</button>
            </form>
          )}
          <h3>Lịch sử</h3>
          <State {...history}>
            <History rows={(history.data || []).map((row: Row) => row.action === "AUTO_CANCEL_EXPIRED"
              ? { ...row, action: "Tự động kết thúc ca chưa bắt đầu" }
              : row)} />
          </State>
          {error && (
            <AlertPopup>
              {error}
            </AlertPopup>
          )}
        </Dialog>
      )}
    </>
  );
}
