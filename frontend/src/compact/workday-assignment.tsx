import { AlertPopup } from "./alert-popup";
import { useEffect, useState } from "react";
import { action, api, type Row } from "./api";
import { ConfirmPopup, Dialog, Empty, State, useQuery } from "./ui";
import {
  monthDates,
  weekDates,
  hoursText,
  type HourWarning,
  shiftTime,
  workdayMessage,
  type WorkdayCalendar,
} from "./workday-scheduling";
import "./workday.css";

export default function WorkdayAssignment({
  slot,
  today,
  branch,
  close,
  saved,
  invalidate,
}: {
  slot: Row;
  today: string;
  branch: number;
  close: () => void;
  saved: (count: number) => void;
  invalidate: () => void;
}) {
  const [period, setPeriod] = useState("day");
  const [dismissedHours, setDismissedHours] = useState("");
  const [excluded, setExcluded] = useState<string[]>([]);
  const [weekdays, setWeekdays] = useState([0, 1, 2, 3, 4, 5, 6]);
  const [users, setUsers] = useState<number[]>([]);
  const [acknowledged, setAcknowledged] = useState(false);
  const [hoursAcknowledged, setHoursAcknowledged] = useState("");
  const [revision, refresh] = useState(0);
  const [pending, setPending] = useState(false);
  const [error, setError] = useState("");
  const [operationKey] = useState(() => crypto.randomUUID());
  const allDates = (period === "month" ? monthDates(slot.date, today) : period === "week" ? weekDates(slot.date, today) : [slot.date])
    .filter(d => d !== today || slot.endMinute > (slot.currentMinute ?? 0));
  const dates = allDates.filter(
    (d) =>
      !excluded.includes(d) &&
      (period === "day" || weekdays.includes(new Date(`${d}T12:00:00`).getDay())),
  );
  const dateList = dates.join(",");
  const primaryCalendar = useQuery<WorkdayCalendar>(
    `workday/calendar?branchId=${branch}&year=${slot.date.slice(0, 4)}`,
    revision,
  );
  const otherYear = allDates.find((d) => d.slice(0, 4) !== slot.date.slice(0, 4))?.slice(0, 4);
  const extraCalendar = useQuery<WorkdayCalendar>(otherYear ? `workday/calendar?branchId=${branch}&year=${otherYear}` : null, revision);
  const calendar = { loading: primaryCalendar.loading || (!!otherYear && extraCalendar.loading), error: primaryCalendar.error || extraCalendar.error,
    data: primaryCalendar.data && (!otherYear || extraCalendar.data) ? { ...primaryCalendar.data, markers: [...primaryCalendar.data.markers, ...(extraCalendar.data?.markers ?? [])] } : undefined };
  const candidates = useQuery<{
    users: Row[];
    slots: { workDate: string; remaining: number }[];
  }>(
    dates.length
      ? `workday/candidates/batch?shiftId=${slot.shiftId}&${dates.map((d) => `dates=${d}`).join("&")}`
      : null,
    revision,
  );
  useEffect(() => {
    setUsers([]);
    setAcknowledged(false);
    setHoursAcknowledged("");
    setError("");
  }, [dateList]);
  const available = candidates.data?.users ?? [];
  const selected = users.filter((id) => available.some((u) => u.userId === id));
  const warnings = available.filter((u) => selected.includes(u.userId)).flatMap((u) =>
    (u.hourWarnings ?? []).map((w: HourWarning) => ({ ...w, userId: u.userId, fullName: u.fullName })));
  const hoursFingerprint = JSON.stringify({dates, users: selected.map((id) => ({id, token: available.find((u) => u.userId === id)?.hoursToken}))});
  const hoursConfirmed = warnings.length === 0 || hoursAcknowledged === hoursFingerprint;
  const capacity = Math.min(
    ...(candidates.data?.slots.map((s) => s.remaining) ?? [0]),
  );
  const markers = calendar.data?.markers ?? [];
  const holidayDates = dates.filter((d) => markers.some((m) => m.date === d));
  const toggleUser = (id: number) =>
    setUsers((old) =>
      old.includes(id) ? old.filter((x) => x !== id) : [...old, id],
    );
  return (
    <Dialog
      title="Phân công ca"
      close={() => {
        if (!pending) close();
      }}
    >
      <p>
        {slot.name} · {slot.date} · {shiftTime(slot.startMinute)}–
        {shiftTime(slot.endMinute)}
      </p>
      <form
        className="workday-assignment"
        onSubmit={async (e) => {
          e.preventDefault();
          if (
            pending ||
            !selected.length ||
            !dates.length ||
            candidates.loading ||
            candidates.error ||
            !calendar.data ||
            calendar.loading ||
            calendar.error ||
            selected.length > capacity ||
            !hoursConfirmed ||
            (holidayDates.length > 0 && !acknowledged)
          )
            return;
          setPending(true);
          setError("");
          try {
            const result = await api(
              "workday/assignments/batch",
              action(slot.rowVersion, {
                operationKey,
                shiftId: slot.shiftId,
                userIds: selected,
                workDates: dates,
                holidaysAcknowledged: acknowledged,
                hoursAcknowledgements: hoursConfirmed ? Object.fromEntries(available.filter((u) => selected.includes(u.userId) && u.hourWarnings?.length).map((u) => [u.userId, u.hoursToken])) : {},
              }),
            );
            saved(result.count);
          } catch (err) {
            setHoursAcknowledged("");
            setError(workdayMessage(err));
            refresh((x) => x + 1);
            invalidate();
          } finally {
            setPending(false);
          }
        }}
      >
        <fieldset disabled={pending}>
          <label>
            Phạm vi phân ca
            <select
              value={period}
              onChange={(e) => {
                setPeriod(e.target.value);
                setExcluded([]);
              }}
            >
              <option value="day">Ngày đang chọn</option>
              <option value="week">Theo tuần (thứ Hai – Chủ nhật)</option>
              <option value="month">Theo tháng {slot.date.slice(5, 7)}/{slot.date.slice(0, 4)}</option>
            </select>
          </label>
          {period !== "day" && (
            <>
              <p>
                {period === "week" ? "Chọn các ngày trong tuần chứa ngày đã chọn, từ thứ Hai đến Chủ nhật." : "Chọn các ngày trong tháng đã chọn."} Bỏ các ngày đã qua và ca đã hết giờ; có thể chọn thứ hoặc bỏ từng ngày bên dưới.
              </p>
              <div
                className="workday-weekdays"
                role="group"
                aria-label="Thứ áp dụng"
              >
                {[1, 2, 3, 4, 5, 6, 0].map((day) => (
                  <label key={day} className="workday-check">
                    <input
                      type="checkbox"
                      checked={weekdays.includes(day)}
                      onChange={() =>
                        setWeekdays((old) =>
                          old.includes(day)
                            ? old.filter((d) => d !== day)
                            : [...old, day],
                        )
                      }
                    />
                    {day ? `Thứ ${day + 1}` : "Chủ nhật"}
                  </label>
                ))}
              </div>
            </>
          )}
          <State {...calendar} retry={() => refresh((x) => x + 1)}>
            <p className="muted">{calendar.data?.notice}</p>
            {markers.some((m) => allDates.includes(m.date)) && (
              <button
                type="button"
                onClick={() =>
                  setExcluded((old) => [
                    ...new Set([
                      ...old,
                      ...allDates.filter((d) =>
                        markers.some((m) => m.date === d),
                      ),
                    ]),
                  ])
                }
              >
                Bỏ các ngày có mốc lễ
              </button>
            )}
            <div
              className="workday-date-options"
              role="group"
              aria-label="Ngày phân ca"
            >
              {allDates.map((d) => (
                <label
                  key={d}
                  className={`workday-check ${markers.some((m) => m.date === d) ? "workday-holiday" : ""}`}
                >
                  <input
                    type="checkbox"
                    checked={dates.includes(d)}
                    disabled={
                      period !== "day" &&
                      !weekdays.includes(new Date(`${d}T12:00:00`).getDay())
                    }
                    onChange={() =>
                      setExcluded((old) =>
                        old.includes(d)
                          ? old.filter((x) => x !== d)
                          : [...old, d],
                      )
                    }
                  />
                  <span>
                    {new Date(`${d}T12:00:00`).toLocaleDateString("vi-VN", {
                      weekday: "short",
                      day: "2-digit",
                      month: "2-digit",
                    })}
                    {markers
                      .filter((m) => m.date === d)
                      .map((m) => (
                        <small key={m.name}>{m.name}</small>
                      ))}
                  </span>
                </label>
              ))}
            </div>
          </State>
          {!dates.length ? (
            <Empty>Chọn ít nhất một ngày.</Empty>
          ) : (
            <State {...candidates} retry={() => refresh((x) => x + 1)}>
              <p>
                Nhân viên đủ điều kiện trên tất cả {dates.length} ngày · Còn ít
                nhất {Number.isFinite(capacity) ? capacity : 0} chỗ mỗi ngày.
              </p>
              {available.length > 0 ? (
                <>
                  <label className="workday-check">
                    <input
                      type="checkbox"
                      checked={selected.length === available.length}
                      disabled={available.length > capacity}
                      onChange={(e) =>
                        setUsers(
                          e.target.checked
                            ? available.map((u) => u.userId)
                            : [],
                        )
                      }
                    />
                    Chọn tất cả ({available.length})
                  </label>
                  <div className="workday-people">
                    {available.map((u) => (
                      <label key={u.userId} className="workday-check">
                        <input
                          type="checkbox"
                          checked={selected.includes(u.userId)}
                          disabled={
                            !selected.includes(u.userId) &&
                            selected.length >= capacity
                          }
                          onChange={() => toggleUser(u.userId)}
                        />
                        {u.fullName} · {u.employeeCode}
                      </label>
                    ))}
                  </div>
                </>
              ) : (
                <Empty>
                  Không có nhân viên đúng nhóm, bộ phận và không trùng lịch trên
                  tất cả ngày đã chọn.
                </Empty>
              )}
              {capacity === 0 && (
                <p role="alert">
                  Có ngày đã đủ người. Bỏ ngày đó hoặc chọn ca khác.
                </p>
              )}
              <details>
                <summary>Sức chứa theo ngày</summary>
                {candidates.data?.slots.map((s) => (
                  <p key={s.workDate}>
                    {s.workDate}: còn {s.remaining} chỗ
                  </p>
                ))}
              </details>
            </State>
          )}
          {holidayDates.length > 0 && (
            <label className="workday-check">
              <input
                type="checkbox"
                checked={acknowledged}
                onChange={(e) => setAcknowledged(e.target.checked)}
              />
              Tôi đã xem {holidayDates.length} ngày có mốc lễ và xác nhận vẫn
              phân ca các ngày này.
            </label>
          )}
          {warnings.length > 0 && !hoursConfirmed && dismissedHours !== hoursFingerprint && (
            <ConfirmPopup title="Cảnh báo vượt giờ" confirm={() => { setHoursAcknowledged(hoursFingerprint); setError(""); }} cancel={() => setDismissedHours(hoursFingerprint)} pending={pending}>
              <h3>Vượt giới hạn phân ca</h3>
              <p>Giới hạn thông thường: 8 giờ/ngày, 40 giờ/tuần (thứ Hai–Chủ nhật). Tổng dưới đây gồm lịch đã có và lịch đang thêm.</p>
              <ul>{warnings.map((w) => (
                <li key={`${w.userId}-${w.period}-${w.from}`}>
                  <strong>{w.fullName}</strong> · {w.period === "DAY" ? "Ngày" : "Tuần"} {w.from}{w.period === "WEEK" ? ` – ${w.to}` : ""}: {hoursText(w.existingMinutes)} đã có + {hoursText(w.addedMinutes)} thêm = <strong>{hoursText(w.totalMinutes)}</strong> / {hoursText(w.limitMinutes)}.
                </li>
              ))}</ul>
              <p>Xác nhận để tiếp tục phân ca vượt giới hạn cho các nhân viên trên.</p>
            </ConfirmPopup>
          )}
          {warnings.length > 0 && <p role="status">{hoursConfirmed ? "Đã xác nhận số giờ vượt." : "Chưa xác nhận số giờ vượt; chưa thể lưu phân ca."} <button type="button" onClick={() => { setDismissedHours(""); setHoursAcknowledged(""); }}>Xem cảnh báo vượt giờ</button></p>}
          <p role="status">
            Sẽ tạo {selected.length} người × {dates.length} ngày ={" "}
            {selected.length * dates.length} lượt phân công. Nếu có xung đột,
            toàn bộ lượt lưu sẽ bị từ chối.
          </p>
          <button
            className="primary"
            disabled={
              pending ||
              !selected.length ||
              !dates.length ||
              candidates.loading ||
              !!candidates.error ||
              !calendar.data ||
              calendar.loading ||
              !!calendar.error ||
              selected.length > capacity ||
              !hoursConfirmed ||
              (holidayDates.length > 0 && !acknowledged)
            }
          >
            Xác nhận phân ca
          </button>
        </fieldset>
        {error && (
          <AlertPopup>
            {error}
          </AlertPopup>
        )}
      </form>
    </Dialog>
  );
}
