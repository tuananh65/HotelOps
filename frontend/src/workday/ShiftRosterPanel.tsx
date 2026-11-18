import {
  ShiftPlanningContext,
  usePlanningBusy,
  localDate,
} from "./shiftPlanningData";
import { LoadingIndicator } from "../components/LoadingIndicator";
import { displayCode } from "../i18n/vi";
import { useContext, useEffect, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import { createHttpClient, isObject } from "../auth/http";
import { safeId } from "./contracts";
type Assignment = {
  id: number;
  employeeId: number;
  employeeName: string;
  status: string;
  version: string;
};
type Shift = {
  id: number;
  templateId: number;
  templateName: string;
  date: string;
  startUtc: string;
  endUtc: string;
  status: string;
  requiredStaff: number;
  assignments: Assignment[];
};
type Page = {
  branchId: number;
  departmentId: number;
  from: string;
  to: string;
  timezone: string;
  shifts: Shift[];
  nextCursor: number | null;
};
function assignment(v: unknown): v is Assignment {
  return (
    isObject(v) &&
    safeId(v.id) &&
    safeId(v.employeeId) &&
    typeof v.employeeName === "string" &&
    typeof v.status === "string" &&
    typeof v.version === "string" &&
    /^"[A-F0-9]{64}"$/.test(v.version)
  );
}
function shift(v: unknown): v is Shift {
  return (
    isObject(v) &&
    safeId(v.id) &&
    safeId(v.templateId) &&
    typeof v.templateName === "string" &&
    typeof v.date === "string" &&
    typeof v.startUtc === "string" &&
    Number.isFinite(Date.parse(v.startUtc)) &&
    typeof v.endUtc === "string" &&
    Number.isFinite(Date.parse(v.endUtc)) &&
    typeof v.status === "string" &&
    typeof v.requiredStaff === "number" &&
    Number.isSafeInteger(v.requiredStaff) &&
    Array.isArray(v.assignments) &&
    v.assignments.every(assignment)
  );
}
export function ShiftRosterPanel({
  revision = 0,
  active = true,
}: {
  revision?: number;
  active?: boolean;
}) {
  const planning = useContext(ShiftPlanningContext);
  const { state, actions } = useAuth(),
    owner = state.authorityGeneration;
  const branch = String(planning?.scope.branchId ?? ""),
    department = String(planning?.scope.departmentId ?? "");
  const [from, setFrom] = useState(localDate());
  const [result, setResult] = useState<Page | null>(null),
    [error, setError] = useState(""),
    [busy, setBusy] = useState(false);
  const [selected, setSelected] = useState<Assignment | null>(null),
    [reason, setReason] = useState(""),
    [confirmed, setConfirmed] = useState(false);
  const [notice, setNotice] = useState<{
    owner: typeof owner;
    branch: number;
    department: number;
    text: string;
  } | null>(null);
  usePlanningBusy("roster", busy);
  const observedRevision = useRef(revision);
  const request = useRef<AbortController | null>(null),
    sending = useRef(false),
    current = useRef(owner);
  current.current = owner;
  useEffect(() => {
    if (active) void load();
  }, [active]);
  useEffect(() => {
    setResult(null);
    setSelected(null);
    setError("");
    return () => request.current?.abort();
  }, [owner]);
  useEffect(() => {
    if (observedRevision.current === revision || busy || sending.current)
      return;
    observedRevision.current = revision;
    // Refresh only an already admitted roster; never infer a new branch or department.
    if (result) void load();
  }, [revision, busy, result]);
  function clear() {
    request.current?.abort();
    setResult(null);
    setError("");
  }
  async function load(after = 0) {
    if (sending.current) return;
    const b = Number(branch),
      d = Number(department);
    if (!safeId(b) || !safeId(d) || !/^\d{4}-\d{2}-\d{2}$/.test(from)) {
      setError("Chọn chi nhánh, bộ phận và ngày đầu của khoảng 7 ngày.");
      return;
    }
    const abort = new AbortController();
    request.current = abort;
    sending.current = true;
    setBusy(true);
    setError("");
    setResult(null);
    setSelected(null);
    try {
      const response = await createHttpClient()({
        method: "GET",
        url:
          "/api/branches/" +
          b +
          "/departments/" +
          d +
          "/shift-roster?from=" +
          from +
          "&afterId=" +
          after,
        expectedStatus: 200,
        signal: abort.signal,
        decode(v) {
          if (
            !isObject(v) ||
            v.branchId !== b ||
            v.departmentId !== d ||
            v.from !== from ||
            typeof v.to !== "string" ||
            typeof v.timezone !== "string" ||
            !Array.isArray(v.shifts) ||
            v.shifts.length > 50 ||
            !v.shifts.every(shift) ||
            !(v.nextCursor === null || safeId(v.nextCursor))
          )
            return;
          return v as Page;
        },
      });
      if (abort.signal.aborted || current.current !== owner) return;
      if (response.ok) setResult(response.data);
      else {
        setError(
          "Không tải được lịch phân ca. Vui lòng thử lại hoặc kiểm tra quyền của bạn.",
        );
        if (response.error.kind === "http" && response.error.status === 401)
          actions.invalidateSession(owner);
      }
    } finally {
      sending.current = false;
      if (current.current === owner) setBusy(false);
    }
  }
  async function cancel() {
    if (sending.current || !selected || !result || !confirmed) return;
    const target = selected,
      snapshot = result,
      abort = new AbortController();
    request.current = abort;
    sending.current = true;
    setBusy(true);
    setError("");
    setNotice(null);
    let submitted = false,
      committed = false;
    try {
      const first = await fetch("/api/auth/csrf", {
        credentials: "same-origin",
        cache: "no-store",
        redirect: "error",
        signal: abort.signal,
      });
      if (!first.ok) throw Error("Xác minh phiên đăng nhập thất bại.");
      const csrf: unknown = await first.json();
      if (
        !isObject(csrf) ||
        csrf.headerName !== "X-CSRF-TOKEN" ||
        typeof csrf.csrfToken !== "string" ||
        !/^[\x21-\x7e]+$/.test(csrf.csrfToken) ||
        current.current !== owner
      )
        throw Error("Phiên đăng nhập đã thay đổi.");
      submitted = true;
      const response = await fetch(
        "/api/branches/" +
          snapshot.branchId +
          "/departments/" +
          snapshot.departmentId +
          "/shift-roster/assignments/" +
          target.id +
          "/cancel",
        {
          method: "POST",
          credentials: "same-origin",
          cache: "no-store",
          redirect: "error",
          signal: abort.signal,
          headers: {
            "Content-Type": "application/json",
            "X-CSRF-TOKEN": csrf.csrfToken,
            "If-Match": target.version,
          },
          body: JSON.stringify({ reason, confirmation: confirmed }),
        },
      );
      if (response.status !== 204) {
        const problem: unknown = await response.json();
        throw Error(
          "Chưa xác nhận được kết quả hủy. Phân công có thể đã thay đổi.",
        );
      }
      if (!abort.signal.aborted && current.current === owner) {
        committed = true;
        setNotice({
          owner,
          branch: snapshot.branchId,
          department: snapshot.departmentId,
          text: "Đã hủy phân công; thông báo đang chờ gửi.",
        });
      }
    } catch (e) {
      if (!abort.signal.aborted && current.current === owner)
        setError(
          "Chưa xác định được kết quả hủy. Tải lại lịch phân ca để kiểm tra trước khi thử lại.",
        );
    } finally {
      sending.current = false;
      if (current.current === owner) {
        setBusy(false);
        if (submitted) {
          setResult(null);
          setSelected(null);
        }
      }
    }
    if (committed && !abort.signal.aborted && current.current === owner)
      await load();
  }
  function local(value: string, zone: string) {
    try {
      return new Intl.DateTimeFormat("vi-VN", {
        dateStyle: "medium",
        timeStyle: "short",
        timeZone: zone,
      }).format(new Date(value));
    } catch {
      return value + " (UTC)";
    }
  }
  return (
    <section className="shift-card">
      <h2>Danh sách phân ca theo tuần</h2>
      <form
        onSubmit={(e) => {
          e.preventDefault();
          void load();
        }}
      >
        <fieldset disabled={busy}>
          <label>
            Xem 7 ngày từ
            <input
              required
              type="date"
              value={from}
              onChange={(e) => {
                clear();
                setFrom(e.target.value);
              }}
            />
          </label>
          <button>Tải danh sách phân ca</button>
        </fieldset>
      </form>
      {busy && <LoadingIndicator compact label="Đang tải lịch phân ca" />}
      {notice?.owner === owner &&
        notice.branch === Number(branch) &&
        notice.department === Number(department) && (
          <p role="status">{notice.text}</p>
        )}
      {error && <p role="alert">{error}</p>}
      {result && (
        <>
          <p>
            {result.from} đến {result.to}; {result.timezone}. Bao gồm lịch sử
            phân công đã hủy.
          </p>
          {result.shifts.length === 0 ? (
            <p>Không có ca làm việc trên trang này.</p>
          ) : (
            <div className="shift-table-scroll">
              <table>
                <thead>
                  <tr>
                    <th>Ca làm việc</th>
                    <th>Ngày / giờ</th>
                    <th>Trạng thái</th>
                    <th>Nhân sự cần có</th>
                    <th>Phân công</th>
                  </tr>
                </thead>
                <tbody>
                  {result.shifts.map((s) => (
                    <tr key={s.id}>
                      <td>{s.templateName}</td>
                      <td>
                        {s.date}
                        <br />
                        {local(s.startUtc, result.timezone)} –{" "}
                        {local(s.endUtc, result.timezone)}
                      </td>
                      <td>{displayCode(s.status)}</td>
                      <td>{s.requiredStaff}</td>
                      <td>
                        {s.assignments.length === 0 ? (
                          "Chưa có phân công"
                        ) : (
                          <ul>
                            {s.assignments.map((a) => (
                              <li key={a.id}>
                                {a.employeeName}: {displayCode(a.status)}{" "}
                                {a.status === "ASSIGNED" &&
                                  s.status === "PLANNED" &&
                                  Date.parse(s.startUtc) > Date.now() && (
                                    <button
                                      disabled={busy}
                                      onClick={() => {
                                        setSelected(a);
                                        setReason("");
                                        setConfirmed(false);
                                      }}
                                    >
                                      Hủy phân công
                                    </button>
                                  )}
                              </li>
                            ))}
                          </ul>
                        )}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
          {result.nextCursor !== null && (
            <button
              disabled={busy}
              onClick={() => void load(result.nextCursor!)}
            >
              Trang sau
            </button>
          )}
        </>
      )}
      {selected && result && (
        <form
          onSubmit={(e) => {
            e.preventDefault();
            void cancel();
          }}
        >
          <fieldset disabled={busy}>
            <legend>Hủy phân công cho {selected.employeeName}</legend>
            <label>
              Lý do
              <textarea
                required
                minLength={10}
                maxLength={1000}
                value={reason}
                onChange={(e) => setReason(e.target.value)}
              />
            </label>
            <label>
              <input
                type="checkbox"
                required
                checked={confirmed}
                onChange={(e) => setConfirmed(e.target.checked)}
              />
              Xác nhận hủy phân công tương lai này.
            </label>
            <button>Xác nhận hủy</button>
            <button type="button" onClick={() => setSelected(null)}>
              Giữ nguyên phân công
            </button>
          </fieldset>
        </form>
      )}
    </section>
  );
}
