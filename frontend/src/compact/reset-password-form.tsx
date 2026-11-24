import { AlertPopup } from "./alert-popup";
type Props = {
  recipient?: string;
  sender?: string;
  pending: boolean;
  error: string;
  submit: (form: HTMLFormElement) => Promise<void>;
  cancel: () => void;
};

/** Shared reset form: credentials remain in form controls for this send only. */
export default function ResetPasswordForm({ recipient, sender, pending, error, submit, cancel }: Props) {
  return (
    <form className="account-management-form" autoComplete="off" onSubmit={(event) => {
      event.preventDefault();
      void submit(event.currentTarget);
    }}>
      <fieldset disabled={pending}>
        <h3>Gửi mật khẩu tạm qua Gmail</h3>
        <label>
          Email người nhận
          <input type="email" value={recipient || ""} readOnly placeholder="Chưa có email" />
        </label>
        {!recipient && <AlertPopup>Hãy bổ sung email trong phần Chỉnh sửa thông tin.</AlertPopup>}
        <label>
          Gmail của Admin
          <input name="gmailAddress" type="email" required maxLength={254}
            defaultValue={sender || ""} placeholder="admin@gmail.com" autoComplete="off" />
        </label>
        <label>
          Mật khẩu ứng dụng Gmail
          <input name="gmailAppPassword" type="password" required maxLength={64}
            autoComplete="new-password" spellCheck={false} aria-describedby="gmail-password-help" />
        </label>
        <p id="gmail-password-help">
          Dùng mật khẩu ứng dụng 16 ký tự, không dùng mật khẩu đăng nhập Gmail.
          {" "}<a href="https://myaccount.google.com/apppasswords" target="_blank" rel="noopener noreferrer">Tạo mật khẩu ứng dụng</a>
          {" "}(cần bật xác minh 2 bước). Thông tin Gmail chỉ dùng cho lần gửi này.
        </p>
        <label>
          Mật khẩu HotelOps của bạn
          <input name="adminPassword" type="password" required maxLength={1024} autoComplete="current-password" />
        </label>
        <p>Mật khẩu tạm được tạo tự động, có hiệu lực 24 giờ. Người nhận phải đổi mật khẩu khi đăng nhập; các phiên cũ sẽ kết thúc.</p>
        {error && <AlertPopup>{error}</AlertPopup>}
        <div className="toolbar">
          <button className="primary" disabled={pending || !recipient}>
            {pending ? "Đang gửi…" : "Xác nhận và gửi email"}
          </button>
          <button type="button" onClick={cancel}>Hủy</button>
        </div>
      </fieldset>
    </form>
  );
}
