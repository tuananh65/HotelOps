import { useEffect, useMemo, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import {
  createChangeDepartmentApi,
  type DepartmentResult,
} from "./changeDepartmentApi";
// Not production-mounted. Review access never grants mutation authority.
const keys = new WeakMap<object, number>();
let sequence = 0;
function key(o: object) {
  if (!keys.has(o)) keys.set(o, ++sequence);
  return keys.get(o)!;
}
export function ChangeDepartmentFlow({
  userId,
  dispatchEnabled = false,
}: {
  userId: number;
  dispatchEnabled?: boolean;
}) {
  const { state, actions } = useAuth();
  const identity = useMemo(
    () => ({}),
    [state.session, state.status, userId, dispatchEnabled],
  );
  const latest = useRef(identity);
  latest.current = identity;
  if (state.status !== "AUTHENTICATED" || !state.session)
    return <p role="status">Xác minh phiên đăng nhập không bị hạn chế.</p>;
  return (
    <Bound
      key={key(identity)}
      userId={userId}
      enabled={dispatchEnabled}
      current={() => latest.current === identity}
      recover={() => void actions.revalidate()}
    />
  );
}
function Bound({
  userId,
  enabled,
  current,
  recover,
}: {
  userId: number;
  enabled: boolean;
  current: () => boolean;
  recover: () => void;
}) {
  const [api] = useState(() =>
    createChangeDepartmentApi(globalThis.fetch.bind(globalThis), enabled),
  );
  const [review, setReview] = useState<Extract<
    DepartmentResult,
    { ok: true }
  > | null>(null);
  const [destination, setDestination] = useState("");
  const [phase, setPhase] = useState("loading");
  const [status, setStatus] = useState<number>();
  const active = useRef(false),
    busy = useRef(false),
    attempted = useRef(false),
    abort = useRef<AbortController | null>(null);
  useEffect(() => {
    active.current = true;
    const controller = new AbortController();
    abort.current = controller;
    void api.read(userId, controller.signal).then((r) => {
      if (!active.current || !current() || controller.signal.aborted) return;
      if (r.ok) {
        setReview(r);
        setPhase("ready");
      } else {
        setStatus(r.status);
        setPhase("error");
      }
    });
    return () => {
      active.current = false;
      controller.abort();
    };
  }, [api, userId]);
  const valid =
    /^[1-9][0-9]*$/.test(destination) &&
    Number.isSafeInteger(Number(destination)) &&
    review &&
    Number(destination) !== review.data.departmentId;
  async function confirm() {
    if (
      !enabled ||
      !valid ||
      !review ||
      busy.current ||
      attempted.current ||
      !active.current ||
      !current()
    )
      return;
    busy.current = true;
    attempted.current = true;
    setPhase("pending");
    const controller = new AbortController();
    abort.current = controller;
    const r = await api.change(
      review.data,
      review.etag,
      Number(destination),
      () => active.current && current(),
      controller.signal,
    );
    if (!active.current || !current()) return;
    busy.current = false;
    setReview(null);
    setDestination("");
    if (r.ok) setPhase("confirmed");
    else {
      setStatus(r.status);
      setPhase(r.outcome === "OUTCOME_UNKNOWN" ? "unknown" : "error");
    }
  }
  return (
    <main>
      <h1>Thay đổi bộ phận</h1>
      <p>
        Chỉ cùng chi nhánh chính. Lịch sử vai trò đang chặn phiên bản giới hạn này. Thao tác gửi bị khóa đến khi được chấp nhận.
      </p>
      {review && (
        <section aria-label="Phạm vi máy chủ cho phép">
          <p>
            Người dùng {review.data.userId}; Chi nhánh {review.data.branchId}; Bộ phận{" "}
            {review.data.departmentId}
          </p>
        </section>
      )}
      {phase === "loading" && <p role="status">Đang tải thông tin xét duyệt bộ phận...</p>}
      {phase === "ready" && (
        <form
          onSubmit={(e) => {
            e.preventDefault();
            if (valid) setPhase("review");
          }}
        >
          <label>
            Mã bộ phận đích
            <input
              aria-label="Mã bộ phận đích"
              value={destination}
              onChange={(e) => setDestination(e.target.value)}
            />
          </label>
          <button disabled={!valid}>Xem lại thay đổi</button>
        </form>
      )}
      {phase === "review" && (
        <section>
          <p>
            Xác nhận bộ phận đích {destination}. The server verifies
            scope and all dependencies.
          </p>
          <button onClick={() => setPhase("ready")}>Quay lại</button>
          <button disabled={!enabled} onClick={() => void confirm()}>
            Xác nhận chuyển bộ phận
          </button>
        </section>
      )}
      {phase === "pending" && <p role="status">Đang chờ chuyển bộ phận...</p>}
      {phase === "confirmed" && (
        <p role="status">
          Đã xác nhận chuyển bộ phận. Các phiên của tài khoản đã bị thu hồi.
        </p>
      )}
      {["error", "unknown"].includes(phase) && (
        <p role="alert">
          {phase === "unknown"
            ? "Chưa xác định kết quả. Không gửi lại; nhờ người có trách nhiệm xác minh."
            : status === 401
              ? "Phiên đăng nhập không hợp lệ. Xác minh lại trước khi xét duyệt."
              : status === 403
                ? "Không có quyền truy cập."
                : status === 412
                  ? "Phiên bản đã đổi. Bắt đầu xét duyệt mới; không gửi lại yêu cầu cũ."
                  : status === 409
                    ? "Xung đột phạm vi, vai trò hoặc công việc đang mở. Chưa xác nhận thay đổi."
                    : "Chưa thể chuyển bộ phận. Chưa xác nhận thay đổi."}
        </p>
      )}
      {status === 401 && <button onClick={recover}>Kiểm tra phiên đăng nhập</button>}
    </main>
  );
}
