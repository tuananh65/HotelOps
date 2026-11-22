import PageTitle from "./page-title";
import { useState } from "react";
import { api, action, dateKey, message, timestamp, type Row } from "./api";
import { useAuth } from "./auth";
import { Badge, Dialog, Empty, History, State, useQuery } from "./ui";
const time = (n: number) =>
  `${String(Math.floor(n / 60)).padStart(2, "0")}:${String(n % 60).padStart(2, "0")}`;
const audienceKey = (shift: Row) => `${shift.departmentId}:${shift.roleId}`;
function days(start: string) {
  return Array.from({ length: 7 }, (_, i) => {
    const d = new Date(`${start}T12:00:00`);
    d.setDate(d.getDate() + i);
    return dateKey(d);
  });
}
export default function Workday() {
  const { branch, profile } = useAuth();
  const [date, setDate] = useState(dateKey()),
    [audience, setAudience] = useState(""),
    [revision, bump] = useState(0),
    [slot, setSlot] = useState<Row | null>(null),
    [detail, setDetail] = useState<Row | null>(null),
    [pending, setPending] = useState(false),
    [error, setError] = useState(""),
    [notice, setNotice] = useState("");
  const query = useQuery(`workday?branchId=${branch}&date=${date}`, revision),
    data = query.data;
  const audiences = [
    ...new Map<string, string>(
      (data?.shifts ?? []).map((s: Row) => [
        audienceKey(s),
        `${s.role} · ${s.department}`,
      ]),
    ),
  ].sort((a, b) => a[1].localeCompare(b[1], "vi"));
  const selectedAudience = audiences.some(([key]) => key === audience)
    ? audience
    : audiences[0]?.[0] || "";
  const visibleShifts = (data?.shifts ?? []).filter(
    (s: Row) => audienceKey(s) === selectedAudience,
  );
  const candidates = useQuery<Row[]>(
    slot
      ? `workday/candidates?shiftId=${slot.shiftId}&date=${slot.date}`
      : null,
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
  return (
    <>
      <div className="page-heading workday-controls">
        <PageTitle>
          <h1>Lịch làm việc</h1>
          <p>Phân ca theo tuần · Giờ vào, ra được ghi nhận thực tế</p>
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
          <button onClick={() => jump(-7)} aria-label="Tuần trước">
            ←
          </button>
          <input
            id="week-date"
            aria-label="Ngày trong tuần"
            type="date"
            value={date}
            onChange={(e) => setDate(e.target.value || dateKey())}
          />
          <button onClick={() => jump(7)} aria-label="Tuần sau">
            →
          </button>
          <button onClick={() => setDate(data?.today || dateKey())}>
            Hôm nay
          </button>
        </div>
      </div>
      {error && (
        <p role="alert" className="alert">
          {error}
        </p>
      )}
      {notice && (
        <p role="status" className="notice">
          {notice}
        </p>
      )}
      <State {...query} retry={() => bump((x) => x + 1)}>
        {data && (
          <>
            {data.active && (
              <section className="active-shift">
                <div>
                  <strong>Ca đang hoạt động: {data.active.name}</strong>
                  <p>
                    Từ {timestamp(data.active.startedAt)} · Thời gian công việc
                    được quản lý riêng.
                  </p>
                </div>
                <button
                  className="primary"
                  disabled={pending}
                  onClick={() =>
                    mutate(
                      `workday/assignments/${data.active.assignmentId}/end`,
                      action(data.active.rowVersion),
                    )
                  }
                >
                  Kết thúc ca
                </button>
              </section>
            )}
            <div className="section-title">
              <h2>
                {data.canManage
                  ? "Lịch theo đối tượng"
                  : "Ca được phân cho tôi"}
              </h2>
              <span>
                Tuần bắt đầu{" "}
                {new Date(data.weekStart + "T12:00:00").toLocaleDateString(
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
                        </th>
                      ))}
                    </tr>
                  </thead>
                  <tbody>
                    {visibleShifts.map((s: Row) => (
                      <tr key={s.shiftId}>
                        <th>
                          <strong>{s.name}</strong>
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
                              a.status !== "CANCELLED",
                          );
                          return (
                            <td
                              key={d}
                              className={d === data.today ? "today" : ""}
                            >
                              <span className="capacity">
                                {rows.length}/{s.capacity}
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
                                  <Badge value={a.status} />
                                </button>
                              ))}
                              {d >= data.today &&
                                s.isActive &&
                                rows.length < s.capacity && (
                                  <button
                                    className="add-shift"
                                    aria-label={`Phân ${s.name} ngày ${d}`}
                                    onClick={() => {
                                      setError("");
                                      setSlot({ ...s, date: d });
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
                {data.assignments
                  .filter((a: Row) => a.status !== "CANCELLED")
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
                          <Badge value={a.status} />
                        </div>
                        <div className="toolbar">
                          {a.status === "PLANNED" &&
                            a.workDate === data.today &&
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
                {!data.assignments.length && (
                  <Empty>Bạn chưa được phân ca trong tuần này.</Empty>
                )}
              </div>
            )}
          </>
        )}
      </State>
      {slot && (
        <Dialog
          title="Phân công ca"
          close={() => {
            if (!pending) setSlot(null);
          }}
        >
          <p>
            {slot.name} · {slot.date} · {time(slot.startMinute)}–
            {time(slot.endMinute)}
          </p>
          <State {...candidates}>
            <form
              onSubmit={(e) => {
                e.preventDefault();
                const f = new FormData(e.currentTarget);
                mutate(
                  "workday/assignments",
                  action(slot.rowVersion, {
                    shiftId: slot.shiftId,
                    userId: Number(f.get("userId")),
                    workDate: slot.date,
                  }),
                );
              }}
            >
              <label>
                Nhân viên đủ điều kiện
                <select name="userId" required defaultValue="">
                  <option value="" disabled>
                    Chọn nhân viên
                  </option>
                  {candidates.data?.map((u) => (
                    <option key={u.userId} value={u.userId}>
                      {u.fullName} · {u.employeeCode}
                    </option>
                  ))}
                </select>
              </label>
              {!candidates.data?.length && (
                <Empty>
                  Không có nhân viên đúng nhóm, bộ phận và không trùng lịch.
                </Empty>
              )}
              <button
                className="primary"
                disabled={pending || !candidates.data?.length}
              >
                Xác nhận phân ca
              </button>
            </form>
          </State>
          {error && (
            <p role="alert" className="alert">
              {error}
            </p>
          )}
        </Dialog>
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
            {detail.workDate} · <Badge value={detail.status} />
          </p>
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
            <History rows={history.data || []} />
          </State>
          {error && (
            <p role="alert" className="alert">
              {error}
            </p>
          )}
        </Dialog>
      )}
    </>
  );
}
