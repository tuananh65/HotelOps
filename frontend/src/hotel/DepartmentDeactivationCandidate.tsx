import { useEffect, useMemo, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import { createUsersApi } from "../users/usersApi";
import type { UserDirectoryItemDto } from "../users/contracts";
import { createHotelApi } from "./hotelApi";
import { strongTag, type DepartmentDto } from "./contracts";
import { createDepartmentDeactivationApi } from "./departmentDeactivationApi";
type Accounts =
  | { kind: "loading" }
  | { kind: "ready"; items: readonly UserDirectoryItemDto[]; total: number }
  | { kind: "unavailable"; status?: number };
/** Independent server reads, never a complete dependency/permission proof; no production route imports. */
export function DepartmentDeactivationCandidate({
  branchId,
  departmentId,
  dispatchEnabled = false,
}: {
  branchId: number;
  departmentId: number;
  dispatchEnabled?: boolean;
}) {
  const { state, actions } = useAuth();
  const identity = useMemo(
    () => ({}),
    [state.session, state.status, state.pending, branchId, departmentId],
  );
  const latest = useRef(identity);
  latest.current = identity;
  if (
    state.status !== "AUTHENTICATED" ||
    !state.session ||
    state.pending !== null
  )
    return (
      <p role="status">
        Xác minh phiên không bị hạn chế trước khi xét duyệt bộ phận.
      </p>
    );
  return (
    <Bound
      key={key(identity)}
      branchId={branchId}
      departmentId={departmentId}
      enabled={dispatchEnabled}
      current={() => latest.current === identity}
      recover={() => void actions.revalidate()}
    />
  );
}
const keys = new WeakMap<object, number>();
let serial = 0;
function key(v: object) {
  if (!keys.has(v)) keys.set(v, ++serial);
  return keys.get(v)!;
}
function Bound({
  branchId,
  departmentId,
  enabled,
  current,
  recover,
}: {
  branchId: number;
  departmentId: number;
  enabled: boolean;
  current: () => boolean;
  recover: () => void;
}) {
  const [data, setData] = useState<{
      department: DepartmentDto;
      etag: string;
    } | null>(null),
    [accounts, setAccounts] = useState<Accounts>({ kind: "loading" }),
    [phase, setPhase] = useState("loading"),
    [reason, setReason] = useState(""),
    [review, setReview] = useState(false),
    [status, setStatus] = useState<number>();
  const [api] = useState(() =>
    createDepartmentDeactivationApi(globalThis.fetch.bind(globalThis), enabled),
  );
  const alive = useRef(true),
    busy = useRef(false),
    attempted = useRef(false),
    controller = useRef<AbortController | null>(null);
  useEffect(() => {
    alive.current = true;
    const abort = new AbortController();
    controller.current = abort;
    void (async () => {
      const r = await createHotelApi().read(
        "department",
        { branchId, id: departmentId },
        {},
        abort.signal,
      );
      if (!alive.current || !current()) return;
      if (!r.ok || !strongTag(r.etag)) {
        setPhase("unavailable");
        if (!r.ok) setStatus(r.error.status);
        return;
      }
      setData({ department: r.data, etag: r.etag });
      setPhase("ready");
      if (!r.data.isActive) {
        setAccounts({ kind: "unavailable" });
        return;
      }
      const users = await createUsersApi().listUsers(
        {
          branchId,
          departmentId,
          accountStatus: "ACTIVE",
          page: 1,
          pageSize: 20,
        },
        abort.signal,
      );
      if (!alive.current || !current()) return;
      if (!users.ok) {
        const s = users.error.kind === "http" ? users.error.status : undefined;
        setAccounts({ kind: "unavailable", status: s });
        if (s === 401) {
          setData(null);
          setReason("");
          setReview(false);
          setPhase("unavailable");
          setStatus(401);
        }
        return;
      }
      // Never display an unrelated department or a non-ACTIVE account in this scoped dependency view.
      if (
        users.data.items.some(
          (u) =>
            u.department?.id !== departmentId || u.accountStatus !== "ACTIVE",
        )
      ) {
        setAccounts({ kind: "unavailable" });
        return;
      }
      setAccounts({
        kind: "ready",
        items: users.data.items,
        total: users.data.totalCount,
      });
    })();
    return () => {
      alive.current = false;
      abort.abort();
      controller.current?.abort();
    };
  }, [branchId, departmentId]);
  const observedUsers = accounts.kind === "ready" && accounts.total > 0;
  const valid =
    reason.trim().length >= 10 &&
    reason.trim().length <= 1000 &&
    !/[\x00-\x1f\x7f-\x9f]/.test(reason);
  async function confirm() {
    if (
      !enabled ||
      !data ||
      !review ||
      !valid ||
      observedUsers ||
      accounts.kind === "loading" ||
      busy.current ||
      attempted.current ||
      !current()
    )
      return;
    busy.current = true;
    attempted.current = true;
    setPhase("pending");
    const abort = new AbortController();
    controller.current = abort;
    const r = await api.change(
      branchId,
      departmentId,
      data.etag,
      reason,
      () => alive.current && current(),
      abort.signal,
    );
    if (!alive.current || !current()) return;
    setData(null);
    setAccounts({ kind: "unavailable" });
    setReason("");
    setReview(false);
    busy.current = false;
    if (r.outcome === "CONFIRMED") setPhase("confirmed");
    else {
      setStatus(r.status);
      setPhase(r.outcome === "OUTCOME_UNKNOWN" ? "unknown" : "rejected");
    }
  }
  return (
    <main>
      <h1>Xem xét vô hiệu hóa bộ phận</h1>
      <p>
        Kích hoạt nghiệp vụ vẫn bị khóa. Dữ liệu đọc không thay thế quyền hoặc xác nhận đã hết ràng buộc.
      </p>
      {data && (
        <>
          <section aria-label="Bộ phận đã chọn">
            <h2>{data.department.name}</h2>
            <p>
              Chi nhánh {data.department.branchId}; Bộ phận {data.department.id}
              ; {data.department.isActive ? "Đang hoạt động" : "Ngừng hoạt động"}
            </p>
          </section>
          <section aria-label="Thông tin tài khoản đang hoạt động">
            <h2>Tài khoản đang hoạt động bạn được phép xem</h2>
            {accounts.kind === "loading" ? (
              <p role="status">Đang tải tài khoản trong phạm vi...</p>
            ) : accounts.kind === "unavailable" ? (
              <p>
                {accounts.status === 403
                  ? "Máy chủ từ chối xem xét tài khoản; điều này không có nghĩa là không còn người dùng hoạt động."
                  : "Chưa kiểm tra được phạm vi tài khoản; chưa thể kết luận đã hết ràng buộc."}
              </p>
            ) : (
              <>
                <p>
                  Tổng số được phép xem: {accounts.total}. First page only;
                  authorization scope may omit other accounts.
                </p>
                {accounts.items.length ? (
                  <ul>
                    {accounts.items.map((u) => (
                      <li key={u.id}>
                        <a href={`/users/${u.id}`}>{u.fullName}</a>
                      </li>
                    ))}
                  </ul>
                ) : (
                  <p>
                    Không thấy tài khoản đang hoạt động. Điều này không xác nhận đã hết mọi ràng buộc.
                  </p>
                )}
              </>
            )}
            {observedUsers && (
              <p role="alert">
                Xử lý các tài khoản hoạt động đã ghi nhận trước khi xác nhận vô hiệu hóa.
              </p>
            )}
          </section>
          <section aria-label="Kiểm tra ràng buộc còn lại">
            <h2>Cần người có trách nhiệm xác minh</h2>
            <ul>
              {[
                "Kho",
                "Công việc đang mở",
                "Mở yêu cầu nội bộ",
                "Tài sản đang quản lý",
              ].map((name) => (
                <li key={name}>
                  {name}: chưa xác minh; cần chấp nhận cả quyền đọc và ghi.
                </li>
              ))}
            </ul>
          </section>
          {!data.department.isActive && (
            <p>
              Bộ phận đã ngừng hoạt động; không có thao tác vô hiệu hóa.
            </p>
          )}
          {phase === "ready" && data.department.isActive && (
            <>
              <label>
                Lý do
                <textarea
                  value={reason}
                  disabled={review}
                  onChange={(e) => setReason(e.target.value)}
                />
              </label>
              {!review ? (
                <button
                  disabled={
                    !valid || observedUsers || accounts.kind === "loading"
                  }
                  onClick={() => setReview(true)}
                >
                  Xem lại vô hiệu hóa
                </button>
              ) : (
                <>
                  <p>
                    Xác nhận bộ phận và lý do. Máy chủ kiểm tra lại mọi ràng buộc trong giao dịch.
                  </p>
                  <button
                    disabled={!enabled || observedUsers}
                    onClick={() => void confirm()}
                  >
                    Xác nhận vô hiệu hóa bộ phận
                  </button>
                  <button onClick={() => setReview(false)}>Chỉnh sửa lý do</button>
                </>
              )}
            </>
          )}
        </>
      )}
      {phase === "loading" && <p role="status">Đang tải bộ phận...</p>}
      {phase === "pending" && <p role="status">Đang chờ chuyển bộ phận...</p>}
      {phase === "confirmed" && (
        <p role="status">Máy chủ đã xác nhận vô hiệu hóa bộ phận.</p>
      )}
      {["unavailable", "rejected", "unknown"].includes(phase) && (
        <p role="alert">
          {phase === "unknown"
            ? "Chưa xác định kết quả. Không gửi lại; xác minh với người có trách nhiệm."
            : status === 412
              ? "Phiên bản bộ phận đã đổi. Tải lại và bắt đầu xét duyệt mới."
              : status === 403
                ? "Máy chủ từ chối truy cập bộ phận."
                : status === 409
                  ? "Ràng buộc hoặc trạng thái bộ phận không cho phép vô hiệu hóa."
                  : "Chưa thể xét duyệt hoặc vô hiệu hóa bộ phận. Không tự gửi lại."}
        </p>
      )}
      {status === 401 && <button onClick={recover}>Kiểm tra phiên đăng nhập</button>}
    </main>
  );
}
