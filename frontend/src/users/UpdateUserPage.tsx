import { useState, type FormEvent } from "react";
import { Link } from "react-router-dom";
import type { UserProfileDto, UserProfilePatch } from "./userProfileContracts";
import {
  profileFailureMessage,
  unknownProfileMessage,
  type useUpdateUserProfile,
} from "./useUpdateUserProfile";

function ProfileForm({
  profile,
  disabled,
  emailInUse,
  save,
}: {
  profile: UserProfileDto;
  disabled: boolean;
  emailInUse: boolean;
  save(patch: UserProfilePatch): Promise<void>;
}) {
  const [draft, setDraft] = useState({
    fullName: profile.fullName,
    email: profile.email,
    phone: profile.phone ?? "",
  });
  const [validation, setValidation] = useState("");
  function submit(event: FormEvent) {
    event.preventDefault();
    if (disabled) return;
    const fullName = draft.fullName.trim(),
      email = draft.email.trim(),
      phone = draft.phone.trim() || null;
    if (
      !fullName ||
      fullName.length > 200 ||
      !email ||
      email.length > 150 ||
      (phone?.length ?? 0) > 20
    ) {
      setValidation(
        "Họ tên và email là bắt buộc. Giới hạn họ tên 200, email 150, điện thoại 20 ký tự.",
      );
      return;
    }
    setValidation("");
    const patch: UserProfilePatch = {};
    if (fullName !== profile.fullName) patch.fullName = fullName;
    if (email !== profile.email) patch.email = email;
    if (phone !== profile.phone) patch.phone = phone;
    // An explicit Save with no edits is a valid conditional no-op, not an effect-driven update.
    if (!Object.keys(patch).length) patch.fullName = fullName;
    void save(patch);
  }
  return (
    <form aria-label="Cập nhật hồ sơ người dùng" onSubmit={submit} noValidate>
      <fieldset disabled={disabled}>
        <legend>Các trường hồ sơ</legend>
        <label>
          Họ và tên
          <input
            name="fullName"
            value={draft.fullName}
            maxLength={200}
            required
            autoComplete="off"
            onChange={(event) =>
              setDraft({ ...draft, fullName: event.target.value })
            }
          />
        </label>
        <label>
          Email
          <input
            name="email"
            type="email"
            aria-invalid={emailInUse || undefined}
            aria-describedby={emailInUse ? "profile-email-duplicate" : undefined}
            value={draft.email}
            maxLength={150}
            required
            autoComplete="off"
            onChange={(event) =>
              setDraft({ ...draft, email: event.target.value })
            }
          />
        </label>
        {emailInUse && <p id="profile-email-duplicate" role="alert">MSG35: Email đã được sử dụng.</p>}
        <label>
          Số điện thoại
          <input
            name="phone"
            value={draft.phone}
            maxLength={20}
            autoComplete="off"
            onChange={(event) =>
              setDraft({ ...draft, phone: event.target.value })
            }
          />
        </label>
        <button type="submit">Lưu hồ sơ cá nhân</button>
      </fieldset>
      {validation && <p role="alert">{validation}</p>}
    </form>
  );
}

export function UpdateUserPage({
  target,
  flow,
}: {
  target: number;
  flow: ReturnType<typeof useUpdateUserProfile>;
}) {
  const { snapshot, progress } = flow;
  const locked =
    progress.phase === "saving" ||
    progress.phase === "stale" ||
    progress.phase === "unknown";
  return (
    <main>
      <h1>Cập nhật hồ sơ người dùng {target}</h1>
      <Link to="/profile">Hồ sơ cá nhân</Link>
      <p>MSG03: Gán vai trò và kích hoạt tài khoản được quản lý riêng với thông tin cá nhân.</p>
      <Link to={`/users/${target}/reset-credential`}>Đặt lại mật khẩu bị quên</Link>
      <p>
        Máy chủ kiểm tra quyền cho từng yêu cầu. Tải hồ sơ không cấp quyền lưu thay đổi.
      </p>
      {progress.phase === "unknown" && (
        <>
          <p role="alert">{unknownProfileMessage}</p>
          <p>
            Việc tải lại chỉ xác nhận hồ sơ hiện tại, không xác nhận cập nhật trước đã lưu. Thao tác lưu vẫn bị khóa tại đây.
          </p>
        </>
      )}
      {progress.phase === "updated" && (
        <p role="status">MSG08: Máy chủ đã xác nhận cập nhật hồ sơ.</p>
      )}
      {progress.phase === "saving" && <p role="status">Đang lưu hồ sơ cá nhân...</p>}
      {progress.error && (
        <p role="alert">
          {progress.phase === "not-dispatched"
            ? "Không gửi yêu cầu cập nhật hồ sơ."
            : ""}
          {profileFailureMessage(progress.error)}
        </p>
      )}
      {flow.error && <p role="alert">{profileFailureMessage(flow.error)}</p>}
      {flow.loading && <p role="status">Đang tải hồ sơ cá nhân...</p>}
      {snapshot && (
        <section aria-label="Bản chụp hồ sơ từ máy chủ">
          <p>Mã tài khoản: {snapshot.profile.id}</p>
          <p>
            Phiên bản: <code>{snapshot.etag}</code>
          </p>
          <ProfileForm
            key={snapshot.revision}
            profile={snapshot.profile}
            disabled={locked}
            emailInUse={progress.error?.reason === "EMAIL_IN_USE"}
            save={flow.save}
          />
        </section>
      )}
      {!snapshot &&
        flow.paused &&
        !progress.error &&
        progress.phase !== "unknown" && (
          <p>
            Tải lại hồ sơ để xác nhận quyền và lấy phiên bản hiện tại.
          </p>
        )}
      <button
        type="button"
        disabled={flow.loading || progress.phase === "saving"}
        onClick={flow.reload}
      >
        Tải lại hồ sơ cá nhân
      </button>
    </main>
  );
}
