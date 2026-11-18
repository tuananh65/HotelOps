import { ActionDialog } from "../components/workspace-interactions";
import { useEffect, useRef, useState, type RefObject } from "react";
import { Link, Navigate, useParams } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import { createUserProfileApi } from "./userProfileApi";
import type { GetProfileResult } from "./userProfileContracts";
import {
  createCredentialResetApi,
  type ResetCredential,
} from "./credentialResetApi";

function ResetFlow({
  target,
  consumed,
  sessionFailure,
}: {
  target: number;
  consumed: RefObject<boolean>;
  sessionFailure(): void;
}) {
  const [api] = useState(() => createCredentialResetApi());
  const [profiles] = useState(() => createUserProfileApi());
  const [password, setPassword] = useState("");
  const [review, setReview] = useState<Extract<
    GetProfileResult,
    { ok: true }
  > | null>(null);
  const [attested, setAttested] = useState(false);
  const [secret, setSecret] = useState<ResetCredential | null>(null);
  const [revealed, setRevealed] = useState(false);
  const [message, setMessage] = useState("");
  const [phase, setPhase] = useState(consumed.current ? "locked" : "idle");
  const mounted = useRef(false),
    busy = useRef(false),
    controller = useRef<AbortController | null>(null);
  const epoch = useRef(0);
  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
      ++epoch.current;
      controller.current?.abort();
    };
  }, []);
  const valid = () => mounted.current && !controller.current?.signal.aborted;
  async function prepare() {
    if (busy.current || consumed.current || !password.trim()) return;
    busy.current = true;
    setPhase("loading");
    setMessage("");
    setReview(null);
    setAttested(false);
    const abort = new AbortController();
    controller.current = abort;
    const supplied = password;
    setPassword("");
    try {
      const proof = await api.reauthenticate(supplied, valid, abort.signal);
      if (!valid()) return;
      if (proof.outcome !== "CONFIRMED") {
        setMessage(
          "Chưa xác minh được quản trị viên. Không gửi yêu cầu đặt lại mật khẩu.",
        );
        setPhase("idle");
        if (proof.status === 401 && proof.code !== "INVALID_CREDENTIALS")
          sessionFailure();
        return;
      }
      const result = await profiles.getProfile(target, abort.signal);
      if (!valid()) return;
      if (!result.ok) {
        setMessage(
          "Máy chủ chưa cho phép hoặc xác nhận tài khoản để xét duyệt. Chưa gửi đặt lại mật khẩu.",
        );
        setPhase("idle");
        if (result.error.status === 401) sessionFailure();
        return;
      }
      setReview(result);
      setPhase("review");
    } finally {
      busy.current = false;
    }
  }
  async function confirm() {
    if (!review || !attested || busy.current || consumed.current) return;
    busy.current = true;
    consumed.current = true;
    const selected = review;
    setReview(null);
    setAttested(false);
    setPhase("saving");
    setMessage("");
    const abort = new AbortController();
    controller.current = abort;
    const result = await api.reset(
      target,
      selected.etag,
      true,
      valid,
      abort.signal,
    );
    if (!valid()) return;
    busy.current = false;
    if (result.outcome === "CONFIRMED") {
      setSecret(result.data);
      setPhase("complete");
      return;
    }
    if (result.outcome === "OUTCOME_UNKNOWN") {
      setPhase("locked");
      setMessage(
        "Chưa xác định kết quả đặt lại. Mật khẩu có thể đã đổi và phiên có thể đã thu hồi. Không gửi lại; nhờ quản trị viên kiểm tra và thực hiện khôi phục riêng.",
      );
      return;
    }
    consumed.current = false;
    setPhase("idle");
    setMessage(
      result.status === 412 || result.status === 428
        ? "Phiên bản người dùng đã đổi. Xác minh lại và xem hồ sơ mới trước khi xác nhận tiếp."
        : result.status === 403
          ? "Máy chủ từ chối đặt lại hoặc cần xác minh quản trị viên mới. Không gửi lại yêu cầu đặt lại."
          : "Chưa xác nhận đặt lại. Xác minh lại trước lần thử mới.",
    );
    if (result.status === 401) sessionFailure();
  }
  return (
    <main>
      <h1>Đặt lại mật khẩu tài khoản</h1>
      <Link to={`/users/${target}`}>Chi tiết tài khoản</Link>
      <p>
        Mã tài khoản: {target}. Only the server decides reset authority. Profile
        access is not permission to reset.
      </p>
      {phase === "idle" && (
        <form
          onSubmit={(e) => {
            e.preventDefault();
            void prepare();
          }}
        >
          <label>
            Mật khẩu quản trị viên hiện tại
            <input
              type="password"
              autoComplete="current-password"
              value={password}
              maxLength={256}
              onChange={(e) => setPassword(e.target.value)}
            />
          </label>
          <button type="submit">Xác minh quản trị viên và tải tài khoản</button>
        </form>
      )}
      {(phase === "loading" || phase === "saving") && (
        <p role="status">Đang kiểm tra yêu cầu...</p>
      )}
      {review && (
        <ActionDialog
          open
          title="Xem lại đặt lại mật khẩu"
          onClose={() => {
            setReview(null);
            setAttested(false);
            setPhase("idle");
          }}
          busy={phase === "saving"}
          error={message}
        >
          <section aria-label="Xem lại đặt lại mật khẩu">
            <p>Họ và tên: {review.data.fullName}</p>
            <p>Email: {review.data.email}</p>
            <p>
              Mật khẩu tạm mới sẽ thay mật khẩu hiện tại và thu hồi mọi phiên
              của tài khoản. Vai trò và phân công chi nhánh không đổi.
            </p>
            <label>
              <input
                type="checkbox"
                checked={attested}
                onChange={(e) => setAttested(e.target.checked)}
              />
              Tôi đã xác minh danh tính nhân viên theo quy trình được duyệt
            </label>
            <button
              data-dialog-action
              type="button"
              disabled={!attested}
              onClick={() => void confirm()}
            >
              Xác nhận đặt lại mật khẩu
            </button>
          </section>
        </ActionDialog>
      )}
      {secret && (
        <section aria-label="Bàn giao mật khẩu đặt lại">
          <h2>Đã xác nhận đặt lại mật khẩu</h2>
          <p>Mã tài khoản: {target}</p>
          <p>Hết hạn lúc: {secret.expiresAtUtc}</p>
          {revealed ? (
            <p aria-label="Mật khẩu tạm thời">{secret.password}</p>
          ) : (
            <p>Đã ẩn mật khẩu</p>
          )}
          <button type="button" onClick={() => setRevealed((v) => !v)}>
            {revealed ? "Ẩn mật khẩu" : "Hiện mật khẩu"}
          </button>
          <button
            type="button"
            onClick={() => {
              const version = epoch.current;
              void (async () => {
                try {
                  await navigator.clipboard.writeText(secret.password);
                  if (valid() && version === epoch.current)
                    setMessage(
                      "Đã sao chép. Chưa xác nhận đã bàn giao cho nhân viên.",
                    );
                } catch {
                  if (valid() && version === epoch.current)
                    setMessage(
                      "Không dùng được bộ nhớ sao chép. Hãy bàn giao thủ công an toàn.",
                    );
                }
              })();
            }}
          >
            Sao chép mật khẩu
          </button>
          <p>
            Bộ nhớ sao chép có thể còn mật khẩu. Rời trang sẽ mất quyền xem tại
            đây và không lấy lại được.
          </p>
          <button
            type="button"
            onClick={() => {
              ++epoch.current;
              setSecret(null);
              setRevealed(false);
              setMessage(
                "Mật khẩu đã ẩn. Không đặt lại tài khoản lần nữa chỉ để lấy lại lần bàn giao này.",
              );
            }}
          >
            Tôi đã bàn giao mật khẩu an toàn
          </button>
        </section>
      )}
      {message && <p role="alert">{message}</p>}
    </main>
  );
}

/** Additive route for the existing active backend; no permission or proof inferred on the client. */
export function CredentialResetRoute() {
  const { id } = useParams();
  const { state, actions } = useAuth();
  const target =
    id && /^[1-9][0-9]*$/.test(id) && Number.isSafeInteger(Number(id))
      ? Number(id)
      : null;
  const consumed = useRef(false),
    mounted = useRef(false),
    recovering = useRef(false);
  const [blocked, setBlocked] = useState(false);
  const owner = useRef(state.session),
    revision = useRef(0);
  if (owner.current !== state.session) {
    owner.current = state.session;
    ++revision.current;
  }
  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
    };
  }, []);
  if (state.status === "ANONYMOUS" || state.status === "EXPIRED_INVALID")
    return <Navigate replace to="/login" />;
  if (state.status === "RESTRICTED")
    return <Navigate replace to="/change-password" />;
  if (
    blocked ||
    state.status !== "AUTHENTICATED" ||
    !state.session ||
    state.pending !== null
  )
    return (
      <main aria-label="Khôi phục phiên đặt lại mật khẩu">
        <p>
          Thông tin tài khoản và mật khẩu đã ẩn. Không có yêu cầu đặt lại nào
          được gửi lại.
        </p>
        <button
          type="button"
          onClick={() => {
            if (recovering.current) return;
            recovering.current = true;
            void actions
              .revalidate()
              .then((result) => {
                if (mounted.current && result.ok) setBlocked(false);
              })
              .finally(() => {
                recovering.current = false;
              });
          }}
        >
          Kiểm tra phiên đăng nhập
        </button>
      </main>
    );
  if (!target) return <p role="alert">Mã người dùng không hợp lệ</p>;
  if (String(target) === state.session.user.id)
    return (
      <p role="alert">
        Dùng Đổi mật khẩu cho tài khoản của bạn. Không thể tự đặt lại theo quy
        trình này.
      </p>
    );
  return (
    <ResetFlow
      key={revision.current + ":" + target}
      target={target}
      consumed={consumed}
      sessionFailure={() => setBlocked(true)}
    />
  );
}
