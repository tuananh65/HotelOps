import { ActionDialog } from "../components/workspace-interactions";
import { useEffect, useMemo, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import { createUserProfileApi } from "./userProfileApi";
import { createUsersApi } from "./usersApi";
import { createRevokeUserRoleApi } from "./revokeUserRoleApi";
import { Link } from "react-router-dom";
/** Read access is not revoke authority. The server rechecks the selected assignment. */
export function RevokeUserRoleCandidate({
  userId,
  assignmentId,
  dispatchEnabled = false,
}: {
  userId: number;
  assignmentId: number;
  dispatchEnabled?: boolean;
}) {
  const { state, actions } = useAuth();
  const identity = useMemo(
    () => ({}),
    [state.session, state.status, state.pending, userId, assignmentId],
  );
  const latest = useRef(identity);
  latest.current = identity;
  if (
    state.status !== "AUTHENTICATED" ||
    state.pending !== null ||
    !state.session
  )
    return (
      <p role="status">
        Xác minh phiên không bị hạn chế trước khi xét duyệt vai trò.
      </p>
    );
  return (
    <Bound
      key={key(identity)}
      userId={userId}
      assignmentId={assignmentId}
      dispatchEnabled={dispatchEnabled}
      current={() => latest.current === identity}
      recover={() => void actions.revalidate()}
    />
  );
}
const keys = new WeakMap<object, number>();
let sequence = 0;
function key(o: object) {
  if (!keys.has(o)) keys.set(o, ++sequence);
  return keys.get(o)!;
}
function Bound({
  userId,
  assignmentId,
  dispatchEnabled,
  current,
  recover,
}: {
  userId: number;
  assignmentId: number;
  dispatchEnabled: boolean;
  current: () => boolean;
  recover: () => void;
}) {
  const [data, setData] = useState<{
    etag: string;
    name: string;
    role: string;
    branch: number | null;
  } | null>(null);
  const [phase, setPhase] = useState("loading");
  const [review, setReview] = useState(false);
  const [status, setStatus] = useState<number>();
  const [dependencies, setDependencies] = useState<string[]>([]);
  const [api] = useState(() =>
    createRevokeUserRoleApi(globalThis.fetch.bind(globalThis), dispatchEnabled),
  );
  const active = useRef(true),
    busy = useRef(false),
    attempted = useRef(false),
    abort = useRef<AbortController | null>(null);
  useEffect(() => {
    active.current = true;
    const controller = new AbortController();
    abort.current = controller;
    void (async () => {
      const profile = await createUserProfileApi().getProfile(
        userId,
        controller.signal,
      );
      if (!active.current || !current()) return;
      if (!profile.ok) {
        setPhase("unavailable");
        setStatus(profile.error.status);
        return;
      }
      const access = await createUsersApi().getEffectiveAccess(
        userId,
        undefined,
        controller.signal,
      );
      if (!active.current || !current()) return;
      if (!access.ok) {
        setPhase("unavailable");
        setStatus(
          access.error.kind === "http" ? access.error.status : undefined,
        );
        return;
      }
      const selected = access.data.assignedRoles.find(
        (r) => r.assignmentId === assignmentId,
      );
      if (!selected) {
        setPhase("unavailable");
        return;
      }
      setData({
        etag: profile.etag,
        name: profile.data.fullName,
        role: selected.name,
        branch: selected.branchId,
      });
      setPhase("ready");
    })();
    return () => {
      active.current = false;
      controller.abort();
      abort.current?.abort();
    };
    // Bound is keyed by session and selectors; no old protected snapshot survives identity replacement.
  }, [userId, assignmentId]);
  async function confirm() {
    if (
      busy.current ||
      attempted.current ||
      !data ||
      !review ||
      !dispatchEnabled ||
      !current()
    )
      return;
    busy.current = true;
    attempted.current = true;
    setPhase("pending");
    const controller = new AbortController();
    abort.current = controller;
    const result = await api.revoke(
      userId,
      assignmentId,
      data.etag,
      () => active.current && current(),
      controller.signal,
    );
    if (!active.current || !current()) return;
    setData(null);
    setReview(false);
    busy.current = false;
    if (result.outcome === "CONFIRMED") setPhase("confirmed");
    else {
      setStatus(result.status);
      setDependencies(result.dependencies ?? []);
      setPhase(
        result.reason === "OPEN_WORK"
          ? "blocked"
          : result.outcome === "OUTCOME_UNKNOWN"
            ? "unknown"
            : "rejected",
      );
    }
  }
  return (
    <main>
      <h1>Thu hồi vai trò người dùng</h1>
      <p>
        Thu hồi sẽ đổi quyền có hiệu lực và đăng xuất mọi phiên của tài khoản.
      </p>
      {data && (
        <section aria-label="Vai trò cần thu hồi">
          <p>
            {data.name}: {data.role}; Chi nhánh {data.branch ?? "unbound"}
          </p>
          <p>Mã phân công: {assignmentId}</p>
        </section>
      )}
      {phase === "ready" && !review && (
        <button onClick={() => setReview(true)}>Xem lại thu hồi quyền</button>
      )}
      {phase === "ready" && review && (
        <ActionDialog
          open
          title="Xác nhận thu hồi vai trò"
          onClose={() => setReview(false)}
        >
          <p>
            Kiểm tra đúng phân công này. Máy chủ quyết định có được thu hồi hay
            không.
          </p>
          <button
            data-dialog-action
            disabled={!dispatchEnabled}
            onClick={() => void confirm()}
          >
            Xác nhận thu hồi
          </button>
        </ActionDialog>
      )}
      {phase === "loading" && (
        <p role="status">Đang tải thông tin xét duyệt từ máy chủ...</p>
      )}
      {phase === "pending" && <p role="status">Đang chờ thu hồi quyền...</p>}
      {phase === "confirmed" && (
        <p role="status">
          Đã thu hồi vai trò và vô hiệu hóa phiên của tài khoản.
        </p>
      )}
      {["unavailable", "rejected", "unknown", "blocked"].includes(phase) && (
        <p role="alert">
          {phase === "blocked"
            ? "Trách nhiệm chưa hoàn tất đang chặn thu hồi quyền. Bàn giao hợp lệ trước khi xét duyệt lại."
            : phase === "unknown"
              ? "Chưa xác định kết quả. Không gửi lại; nhờ người có trách nhiệm xác minh."
              : status === 412
                ? "Phiên bản đã đổi. Tải lại và xét duyệt theo quy trình mới; không gửi lại yêu cầu cũ."
                : status === 403
                  ? "Máy chủ từ chối quyền truy cập."
                  : status === 503
                    ? "Chưa thể thu hồi. Chưa hoàn tất kiểm tra trách nhiệm."
                    : "Chưa xác nhận thu hồi quyền. Không tự gửi lại."}
        </p>
      )}
      {phase === "blocked" && dependencies.length > 0 && (
        <section aria-label="Công việc đang chặn thu hồi quyền">
          <h2>Trách nhiệm chưa hoàn tất</h2>
          <ul>
            {dependencies.map((d) => (
              <li key={d}>{d.replaceAll("_", " ")}</li>
            ))}
          </ul>
          {dependencies.length === 200 && (
            <p>
              Hiển thị 200 ràng buộc đầu tiên. Xử lý chúng trước khi kiểm tra
              lại.
            </p>
          )}
          <p>Các mã này không cấp quyền truy cập hồ sơ bên dưới.</p>
        </section>
      )}
      <Link to={`/users/${userId}`}>
        Quay lại tài khoản và kiểm tra quyền hiện tại
      </Link>
      {status === 401 && (
        <button onClick={recover}>Kiểm tra phiên đăng nhập</button>
      )}
    </main>
  );
}
