import { ApiError, message } from "./api";

export function passwordCriteria(password: string, currentPassword?: string) {
  return [
    {
      label: "Từ 8 đến 128 ký tự",
      met: password.length >= 8 && password.length <= 128,
    },
    { label: "Có chữ hoa (A–Z)", met: /[A-Z]/.test(password) },
    { label: "Có chữ thường (a–z)", met: /[a-z]/.test(password) },
    { label: "Có chữ số (0–9)", met: /[0-9]/.test(password) },
    {
      label: "Có ký tự đặc biệt (ví dụ: !, @, #)",
      met: /[\p{P}\p{S}]/u.test(password),
    },
    ...(currentPassword === undefined
      ? []
      : [
          {
            label: "Khác mật khẩu hiện tại",
            met: !!password && password !== currentPassword,
          },
        ]),
  ];
}

export function PasswordChecklist({
  password,
  currentPassword,
  id,
}: {
  password: string;
  currentPassword?: string;
  id?: string;
}) {
  return (
    <ul id={id} aria-live="polite" aria-label="Yêu cầu mật khẩu">
      {passwordCriteria(password, currentPassword).map(({ label, met }) => (
        <li key={label}>
          <span aria-hidden="true">{met ? "✓" : "○"}</span> {label}
          <span className="sr-only">: {met ? "Đã đạt" : "Chưa đạt"}</span>
        </li>
      ))}
    </ul>
  );
}

const accountErrors: Record<string, string> = {
  USERNAME_ALREADY_USED:
    "Tên đăng nhập đã được sử dụng. Vui lòng chọn tên khác.",
  EMAIL_ALREADY_USED: "Email đã được sử dụng bởi tài khoản khác.",
  PASSWORD_POLICY_NOT_MET: "Mật khẩu chưa đáp ứng đầy đủ các yêu cầu bên dưới.",
  INVALID_USERNAME: "Tên đăng nhập phải có từ 1 đến 100 ký tự.",
  INVALID_ACCOUNT_NAME: "Họ tên phải có từ 1 đến 200 ký tự.",
  INVALID_ACCOUNT_EMAIL: "Email không hợp lệ.",
  INVALID_ACCOUNT_PHONE: "Số điện thoại không hợp lệ (tối đa 30 ký tự).",
  INVALID_ACCOUNT_BRANCH: "Chi nhánh không còn hoạt động. Vui lòng chọn lại.",
  INVALID_ACCOUNT_DEPARTMENT:
    "Bộ phận không thuộc chi nhánh đã chọn hoặc đã ngừng hoạt động.",
  STALE_ROW_VERSION:
    "Thông tin đã thay đổi. Hãy tải lại hồ sơ trước khi chỉnh sửa tiếp.",
};
export function accountMessage(error: unknown) {
  return (
    (error instanceof ApiError && accountErrors[error.code]) || message(error)
  );
}
