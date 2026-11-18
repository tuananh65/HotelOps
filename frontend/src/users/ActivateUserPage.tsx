import { ActionDialog } from "../components/workspace-interactions";
import { useRef, useState } from "react";
import { Link } from "react-router-dom";
import {
  activationReason,
  unknownActivationMessage,
  type useActivateUser,
} from "./useActivateUser";

function ConfirmationForm({
  disabled,
  activate,
}: {
  disabled: boolean;
  activate(reason: string): Promise<void>;
}) {
  const [draft, setDraft] = useState("");
  const [review, setReview] = useState<string | null>(null);
  const reviewed = useRef<string | null>(null);
  const [error, setError] = useState("");
  function preview() {
    const validated = activationReason(draft);
    if (validated.error) {
      setError(validated.error);
      return;
    }
    setError("");
    reviewed.current = validated.reason;
    setReview(validated.reason);
  }
  function confirm() {
    if (disabled || reviewed.current === null) return;
    const reason = reviewed.current;
    reviewed.current = null;
    setReview(null);
    void activate(reason);
  }
  return (
    <form
      aria-label="Kích hoạt tài khoản người dùng"
      onSubmit={(e) => e.preventDefault()}
      noValidate
    >
      <fieldset disabled={disabled}>
        <legend>Lý do kích hoạt</legend>
        <label>
          Lý do
          <textarea
            name="reason"
            required
            autoComplete="off"
            value={draft}
            onChange={(e) => {
              setDraft(e.target.value);
              reviewed.current = null;
              setReview(null);
              setError("");
            }}
          />
        </label>
        <button type="button" onClick={preview}>
          Xem lại kích hoạt
        </button>
        {review !== null && (
          <ActionDialog
            open
            title="Xem lại kích hoạt"
            onClose={() => {
              setReview(null);
              reviewed.current = null;
            }}
            busy={disabled}
          >
            <section aria-label="Xem lại kích hoạt">
              <p>
                Kiểm tra tài khoản đích bên trên và lý do này trước khi gửi:
              </p>
              <p>{review}</p>
              <button data-dialog-action type="button" onClick={confirm}>
                Xác nhận kích hoạt
              </button>
            </section>
          </ActionDialog>
        )}
      </fieldset>
      {error && <p role="alert">{error}</p>}
    </form>
  );
}

export function ActivateUserPage({
  target,
  flow,
}: {
  target: number;
  flow: ReturnType<typeof useActivateUser>;
}) {
  const { snapshot, progress } = flow;
  const locked = ["pending", "unknown", "stale", "confirmed"].includes(
    progress.phase,
  );
  return (
    <main>
      <h1>Kích hoạt tài khoản người dùng {target}</h1>
      <Link to="/profile">Hồ sơ cá nhân</Link>
      <p>
        Máy chủ kiểm tra trạng thái tài khoản và quyền ngay khi kích hoạt. Tải
        hồ sơ không cấp quyền kích hoạt, kể cả tài khoản của bạn.
      </p>
      <p>
        Kích hoạt không đặt lại mật khẩu, trạng thái khóa hay hạn mật khẩu, và
        không khôi phục phiên đã thu hồi.
      </p>
      {progress.phase === "unknown" && (
        <>
          <p role="alert">{unknownActivationMessage}</p>
          <p>
            Việc tải lại chỉ xác nhận hồ sơ và phiên bản, không xác nhận kích
            hoạt thành công. Thao tác kích hoạt vẫn bị khóa tại đây.
          </p>
        </>
      )}
      {progress.phase === "confirmed" && (
        <p role="status">
          Máy chủ đã xác nhận kích hoạt. Mã tài khoản: {target}. accountStatus:
          ACTIVE.
        </p>
      )}
      {progress.phase === "pending" && (
        <p role="status">Đang chờ kích hoạt...</p>
      )}
      {progress.message && (
        <p role="alert">
          {progress.phase === "not-dispatched"
            ? "Không gửi yêu cầu kích hoạt."
            : ""}
          {progress.message}
        </p>
      )}
      {flow.error && <p role="alert">{flow.error}</p>}
      {flow.loading && <p role="status">Đang tải hồ sơ cá nhân...</p>}
      {snapshot && (
        <section aria-label="Bản chụp thông tin quản lý từ máy chủ">
          <p>Mã tài khoản: {snapshot.profile.id}</p>
          <p>Họ và tên: {snapshot.profile.fullName}</p>
          <p>Email: {snapshot.profile.email}</p>
          {!locked && (
            <ConfirmationForm
              key={snapshot.revision}
              disabled={locked}
              activate={flow.activate}
            />
          )}
        </section>
      )}
      <button
        type="button"
        disabled={flow.loading || progress.phase === "pending"}
        onClick={flow.reload}
      >
        Tải lại hồ sơ cá nhân
      </button>
    </main>
  );
}
