import { userMessage } from "../i18n/userMessage";
import { SearchableSelect } from "../components/SearchableSelect";
import {
  ActionDialog,
  announceWorkflowSuccess,
} from "../components/workspace-interactions";
import { StatusBadge } from "../components/design-system";
import { useEffect, useMemo, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import { createWorkdayApi } from "./workdayApi";
import { createScheduledWorkdayApi } from "./scheduledWorkdayApi";
import {
  scheduledMessage,
  strongWorkdayTag,
  type ScheduledWorkdayResponse,
} from "./scheduledContracts";
import { safeId, type OwnShift, type OwnWorkday } from "./contracts";
import { UnscheduledStartForm } from "./UnscheduledStartForm";
import { LoadingIndicator } from "../components/LoadingIndicator";

const dateTime = (value: string) =>
  new Intl.DateTimeFormat("vi-VN", {
    timeZone: "Asia/Ho_Chi_Minh",
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  }).format(new Date(value));
const shiftLabel = (shift: OwnShift) =>
  `${dateTime(shift.scheduledStartAtUtc)} – ${new Intl.DateTimeFormat("vi-VN", {
    timeZone: "Asia/Ho_Chi_Minh",
    hour: "2-digit",
    minute: "2-digit",
  }).format(new Date(shift.scheduledEndAtUtc))}`;
const keys = new WeakMap<object, number>();
let generation = 0;
function key(x: object) {
  if (!keys.has(x)) keys.set(x, ++generation);
  return keys.get(x)!;
}
/** Scheduled Start/End integration; server independently authorizes every mutation. */
export function ScheduledWorkdayPanel({
  branchId,
  branchName,
  onBusyChange,
  dispatchEnabled = false,
}: {
  branchId: number;
  branchName?: string;
  onBusyChange?: (busy: boolean) => void;
  dispatchEnabled?: boolean;
}) {
  const { state, actions } = useAuth();
  const identity = useMemo(
    () => ({}),
    [state.session, state.status, state.pending],
  );
  const [invalidIdentity, setInvalidIdentity] = useState<object | null>(null);
  const binding = useMemo(
    () => ({}),
    [state.session, state.status, state.pending, branchId, dispatchEnabled],
  );
  const latest = useRef(binding);
  latest.current = binding;
  const current = useMemo(() => () => latest.current === binding, [binding]);
  if (
    state.status !== "AUTHENTICATED" ||
    !state.session ||
    state.session.mustChangePassword ||
    state.pending !== null
  )
    return (
      <section>
        <p>Cần phiên đăng nhập đã xác minh và không bị hạn chế.</p>
        <button onClick={() => void actions.revalidate()}>
          Xác minh lại phiên
        </button>
      </section>
    );
  if (invalidIdentity === identity)
    return (
      <section>
        <p role="alert">
          Phiên đăng nhập không hợp lệ. Xác minh lại trước khi xem hoặc thay đổi
          ngày làm việc.
        </p>
        <button onClick={() => void actions.revalidate()}>
          Xác minh lại phiên
        </button>
      </section>
    );
  if (!safeId(branchId)) return <p role="alert">Chọn mã chi nhánh hợp lệ.</p>;
  return (
    <Bound
      key={key(binding)}
      {...{ branchId, branchName, onBusyChange, dispatchEnabled, current }}
      invalidate={() => setInvalidIdentity(identity)}
      revalidate={() => void actions.revalidate()}
    />
  );
}
type Review = {
  operation: "start" | "end";
  shift: OwnShift | undefined;
  day: OwnWorkday | null | undefined;
};
function Bound({
  branchId,
  branchName,
  onBusyChange,
  dispatchEnabled,
  current,
  revalidate,
  invalidate,
}: {
  branchId: number;
  branchName?: string;
  onBusyChange?: (busy: boolean) => void;
  dispatchEnabled: boolean;
  current: () => boolean;
  revalidate: () => void;
  invalidate: () => void;
}) {
  const [read] = useState(() => createWorkdayApi()),
    [write] = useState(() =>
      createScheduledWorkdayApi(
        globalThis.fetch.bind(globalThis),
        dispatchEnabled,
      ),
    );
  const [shifts, setShifts] = useState<OwnShift[] | null>(null),
    [day, setDay] = useState<OwnWorkday | null | undefined>(undefined),
    [shiftError, setShiftError] = useState(""),
    [dayError, setDayError] = useState(""),
    [selection, setSelection] = useState(""),
    [refresh, setRefresh] = useState(0),
    [pending, setPending] = useState(false),
    [message, setMessage] = useState(""),
    [sessionInvalid, setSessionInvalid] = useState(false),
    [locked, setLocked] = useState(false);
  const [receipt, setReceipt] = useState<ScheduledWorkdayResponse | null>(null);
  const [review, setReview] = useState<Review | null>(null);
  useEffect(() => {
    onBusyChange?.(pending || locked || review !== null);
    return () => onBusyChange?.(false);
  }, [onBusyChange, pending, locked, review]);
  const reviewed = useRef<Review | null>(null);
  function clearReview() {
    reviewed.current = null;
    setReview(null);
  }
  const mounted = useRef(false),
    busy = useRef(false),
    uncertain = useRef(false),
    invalid = useRef(false),
    controller = useRef<AbortController | null>(null);
  function invalidateSession() {
    setReceipt(null);
    clearReview();
    invalid.current = true;
    invalidate();
    busy.current = true;
    controller.current?.abort();
    setSessionInvalid(true);
    setShifts(null);
    setDay(undefined);
    setSelection("");
    setMessage("");
    setPending(false);
  }
  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
      controller.current?.abort();
    };
  }, []);
  useEffect(() => {
    clearReview();
    const abort = new AbortController();
    let active = true;
    setShifts(null);
    setDay(undefined);
    setSelection("");
    setShiftError("");
    setDayError("");
    void read.ownShifts(branchId, abort.signal).then((r) => {
      if (!active || !current() || invalid.current) return;
      if (!r.ok && r.error === "unauthorized") {
        invalidateSession();
        return;
      }
      if (r.ok) {
        setShifts(r.data);
        if (r.data.length === 1)
          setSelection(String(r.data[0].shiftAssignmentId));
      } else
        setShiftError(
          r.error === "forbidden"
            ? "Bạn chưa được phép xem ca tại chi nhánh này."
            : "Chưa tải được ca làm việc. Vui lòng thử tải lại.",
        );
    });
    void read.current(branchId, abort.signal).then((r) => {
      if (!active || !current() || invalid.current) return;
      if (!r.ok && r.error === "unauthorized") {
        invalidateSession();
        return;
      }
      if (r.ok) {
        setDay(r.data);
        if (!uncertain.current) busy.current = false;
      } else
        setDayError(
          r.error === "forbidden"
            ? "Bạn chưa được phép ghi nhận ca tại chi nhánh này."
            : "Chưa tải được trạng thái làm việc. Vui lòng thử tải lại.",
        );
    });
    return () => {
      active = false;
      abort.abort();
    };
  }, [read, branchId, current, refresh]);
  const selected = shifts?.find(
    (s) => s.shiftAssignmentId === Number(selection),
  );
  const startAllowed =
    day === null && !!selected && selected.branchId === branchId;
  const endAllowed =
    !!day &&
    day.branchId === branchId &&
    (day.isUnscheduled
      ? day.shiftAssignmentId === null
      : safeId(day.shiftAssignmentId)) &&
    (day.openInterval === null ||
      ["TASK", "CLEANING", "REWORK", "MAINTENANCE", "OCCURRENCE"].includes(
        day.openInterval.workItemKind,
      )) &&
    strongWorkdayTag(`"${day.rowVersion}"`);
  function prepare(operation: "start" | "end") {
    if (
      !dispatchEnabled ||
      busy.current ||
      uncertain.current ||
      invalid.current ||
      !current() ||
      (operation === "start" ? !startAllowed : !endAllowed)
    )
      return;
    const next = { operation, shift: selected, day };
    setReceipt(null);
    reviewed.current = next;
    setReview(next);
    setMessage("");
  }
  async function confirm() {
    const snapshot = reviewed.current;
    if (!snapshot) return;
    const operation = snapshot.operation;
    if (
      !dispatchEnabled ||
      busy.current ||
      uncertain.current ||
      invalid.current ||
      !mounted.current ||
      !current() ||
      (operation === "start"
        ? !startAllowed || selected !== snapshot.shift || day !== snapshot.day
        : !endAllowed || day !== snapshot.day)
    )
      return;
    clearReview();
    busy.current = true;
    setPending(true);
    setMessage("");
    controller.current = new AbortController();
    const result =
      operation === "start"
        ? await write.start(
            branchId,
            snapshot.shift!.shiftAssignmentId,
            () => mounted.current && current() && !invalid.current,
            controller.current.signal,
          )
        : await write.end(
            branchId,
            snapshot.day!.workdayId,
            snapshot.day!.shiftAssignmentId,
            `"${snapshot.day!.rowVersion}"`,
            () => mounted.current && current() && !invalid.current,
            controller.current.signal,
            snapshot.day!.openInterval !== null,
          );
    if (!mounted.current || !current() || invalid.current) return;
    if (result.outcome !== "CONFIRMED" && result.error.status === 401) {
      invalidateSession();
      return;
    }
    setPending(false);
    if (result.outcome === "CONFIRMED") {
      setReceipt(result.data);
      setDay(undefined);
      setMessage(
        operation === "start"
          ? "Đã ghi nhận bắt đầu ca thành công."
          : "Đã ghi nhận kết thúc ca thành công.",
      );
      setRefresh((n) => n + 1);
    } else {
      setMessage(scheduledMessage(result));
      if (result.outcome === "OUTCOME_UNKNOWN") {
        uncertain.current = true;
        setLocked(true);
      }
    }
    // Keep guard until a fresh read renders, preventing a second click against the old snapshot.
    if (result.outcome !== "CONFIRMED") busy.current = false;
  }

  if (sessionInvalid)
    return (
      <section>
        <p role="alert">
          Phiên đăng nhập không hợp lệ. Xác minh lại trước khi xem hoặc thay đổi
          ngày làm việc.
        </p>
        <button onClick={revalidate}>Xác minh lại phiên</button>
      </section>
    );
  return (
    <div className="workday-content">
      {!dispatchEnabled && (
        <p className="workday-notice">
          Chức năng ghi nhận ca hiện chưa khả dụng.
        </p>
      )}
      <div className="workday-columns">
        <section className="workday-card" aria-label="Ca được phân">
          <span className="workday-eyebrow">01 · CA HÔM NAY</span>
          <h2>Ca được phân</h2>
          <p className="workday-muted">
            Giờ Việt Nam (UTC+7). Các ca ngày khác không hiển thị trong danh
            sách bắt đầu hôm nay.
          </p>
          {shiftError ? (
            <p role="alert">{userMessage(shiftError)}</p>
          ) : shifts === null ? (
            <LoadingIndicator label="Đang tải ca làm việc" />
          ) : shifts.length === 0 ? (
            <div className="workday-empty">
              <strong>Hôm nay chưa có ca có thể bắt đầu</strong>
              <p>
                Ca đã bắt đầu hoặc đã kết thúc sẽ không xuất hiện ở đây. Nếu bạn
                được phân ca vào ngày khác, hãy quay lại đúng ngày.
              </p>
            </div>
          ) : (
            <label>
              Ca đã lên lịch
              <SearchableSelect
                aria-label="Ca đã lên lịch"
                value={selection}
                disabled={pending || locked || !!day}
                onChange={(e) => {
                  clearReview();
                  setSelection(e.target.value);
                }}
              >
                <option value="">Chọn ca làm việc</option>
                {shifts.map((s) => (
                  <option key={s.shiftAssignmentId} value={s.shiftAssignmentId}>
                    {shiftLabel(s)}
                  </option>
                ))}
              </SearchableSelect>
            </label>
          )}
          <button
            className="workday-primary"
            disabled={!dispatchEnabled || !startAllowed || pending || locked}
            onClick={() => prepare("start")}
          >
            Bắt đầu ca đã phân
          </button>
          {day && (
            <p className="workday-muted">
              Bạn đang làm việc. Hãy kết thúc ca hiện tại trước khi bắt đầu ca
              khác.
            </p>
          )}
        </section>
        <section className="workday-card" aria-label="Ngày làm việc hiện tại">
          <span className="workday-eyebrow">02 · TRẠNG THÁI LÀM VIỆC</span>
          <h2>Ngày làm việc hiện tại</h2>
          {dayError ? (
            <p role="alert">{userMessage(dayError)}</p>
          ) : day === undefined ? (
            <LoadingIndicator label="Đang tải trạng thái làm việc" />
          ) : day === null ? (
            <div className="workday-empty">
              <strong>Chưa bắt đầu ca</strong>
              <p>
                Chọn ca được phân rồi bấm Bắt đầu ca đã phân để ghi nhận giờ vào
                làm.
              </p>
            </div>
          ) : (
            <>
              <StatusBadge>Đang làm việc</StatusBadge>
              <dl className="workday-facts">
                <div>
                  <dt>Bắt đầu lúc</dt>
                  <dd>{dateTime(day.startedAtUtc)}</dd>
                </div>
                <div>
                  <dt>Hình thức</dt>
                  <dd>
                    {day.isUnscheduled
                      ? "Ngoài kế hoạch ca"
                      : "Theo ca được phân"}
                  </dd>
                </div>
              </dl>
              <p>
                Kiểm tra công việc còn mở và bàn giao nếu cần trước khi kết thúc
                ca.
              </p>
              {day.openInterval && (
                <p className="workday-notice" role="alert">
                  {[
                    "TASK",
                    "CLEANING",
                    "REWORK",
                    "MAINTENANCE",
                    "OCCURRENCE",
                  ].includes(day.openInterval.workItemKind)
                    ? "Bạn còn công việc đang chạy. Khi kết thúc ca, hệ thống sẽ tạm dừng công việc với lý do hết ca. Hãy hoàn tất bàn giao bắt buộc trước."
                    : "Công việc đang chạy chưa hỗ trợ tạm dừng. Vui lòng hoàn tất công việc trước khi kết thúc ca."}
                </p>
              )}
              {day.openInterval?.workItemKind === "OCCURRENCE" && (
                <Link
                  to={`/operations/maintenance?branchId=${branchId}&runId=${day.openInterval.workItemId}`}
                >
                  Mở công việc bảo trì định kỳ
                </Link>
              )}
            </>
          )}
          <button
            className="workday-secondary"
            disabled={!dispatchEnabled || !endAllowed || pending || locked}
            onClick={() => prepare("end")}
          >
            {day?.openInterval
              ? "Tạm dừng tất cả và kết thúc ngày làm việc"
              : "Kết thúc ngày làm việc"}
          </button>
        </section>
      </div>
      {dispatchEnabled &&
        day === null &&
        shifts !== null &&
        shifts.length === 0 &&
        !shiftError &&
        !dayError && (
          <ActionDialog
            scope="workday/ScheduledWorkdayPanel.tsx"
            title="Làm việc ngoài kế hoạch ca"
            busy={pending}
          >
            <section>
              <p>
                Chỉ dùng khi hôm nay không có ca được phân. Bạn cần chọn bộ phận
                và ghi rõ lý do.
              </p>
              <UnscheduledStartForm
                branchId={branchId}
                current={current}
                disabled={pending || locked}
                invalidate={invalidateSession}
                onBusy={(value) => {
                  busy.current = value;
                  setPending(value);
                }}
                onUnknown={() => {
                  uncertain.current = true;
                  setLocked(true);
                }}
                onStarted={(value) => {
                  (announceWorkflowSuccess(
                    "Đã bắt đầu ngày làm việc ngoài kế hoạch.",
                    "workday/ScheduledWorkdayPanel.tsx",
                  ),
                    setMessage("Đã bắt đầu ngày làm việc ngoài kế hoạch."));
                  setReceipt(value);
                  setDay(undefined);
                  setPending(false);
                  setRefresh((n) => n + 1);
                }}
              />
            </section>
          </ActionDialog>
        )}
      {review && (
        <ActionDialog
          open
          title={
            review.operation === "start"
              ? "Xác nhận bắt đầu ca"
              : "Xác nhận kết thúc ca"
          }
          onClose={clearReview}
          busy={pending}
        >
          <section
            className="workday-card workday-review"
            aria-label="Xem lại ngày làm việc theo ca"
          >
            <h2>
              {review.operation === "start"
                ? "Xác nhận bắt đầu ca"
                : "Xác nhận kết thúc ca"}
            </h2>
            {branchName && <p>{branchName}</p>}
            <p>
              {review.operation === "start"
                ? `Ca đã chọn: ${shiftLabel(review.shift!)}`
                : `Đã bắt đầu lúc: ${dateTime(review.day!.startedAtUtc)}`}
            </p>
            <p>
              Thời gian thực tế được ghi nhận khi bạn xác nhận.
              {review.operation === "end" &&
                review.day?.openInterval &&
                " Công việc đang chạy sẽ được tạm dừng. Nếu không thể tạm dừng, ca làm việc vẫn tiếp tục."}
            </p>
            <div className="workday-actions">
              <button
                className="workday-primary"
                disabled={pending || locked || !dispatchEnabled}
                onClick={() => void confirm()}
              >
                {review.operation === "start"
                  ? "Xác nhận bắt đầu ca"
                  : "Xác nhận kết thúc ngày làm việc"}
              </button>
              <button disabled={pending} onClick={clearReview}>
                Quay lại
              </button>
            </div>
          </section>
        </ActionDialog>
      )}
      {pending && <LoadingIndicator label="Đang ghi nhận ca làm việc" />}
      {message && (
        <p
          className={`workday-notice${receipt ? " workday-notice-success" : ""}`}
          role={receipt ? "status" : "alert"}
        >
          {userMessage(message)}
        </p>
      )}
      {receipt && (
        <section
          className="workday-card"
          aria-label="Kết quả ngày làm việc đã xác nhận"
        >
          <h2>
            {receipt.status === "ENDED" ? "Đã kết thúc ca" : "Đã bắt đầu ca"}
          </h2>
          <dl className="workday-facts">
            <div>
              <dt>Giờ bắt đầu</dt>
              <dd>
                <time dateTime={receipt.startedAtUtc}>
                  {dateTime(receipt.startedAtUtc)}
                </time>
              </dd>
            </div>
            {receipt.endedAtUtc && (
              <div>
                <dt>Giờ kết thúc</dt>
                <dd>
                  <time dateTime={receipt.endedAtUtc}>
                    {dateTime(receipt.endedAtUtc)}
                  </time>
                </dd>
              </div>
            )}
          </dl>
        </section>
      )}
      <div className="workday-actions">
        <button
          disabled={pending}
          onClick={() => {
            clearReview();
            setRefresh((n) => n + 1);
          }}
        >
          Tải lại dữ liệu
        </button>
        {locked && (
          <button disabled={pending} onClick={revalidate}>
            Kiểm tra lại phiên đăng nhập
          </button>
        )}
      </div>
      {locked && (
        <p className="workday-notice">
          Kết quả thao tác chưa rõ. Hãy kiểm tra trạng thái hiện tại hoặc liên
          hệ quản lý trước khi thao tác lại.
        </p>
      )}
    </div>
  );
}
