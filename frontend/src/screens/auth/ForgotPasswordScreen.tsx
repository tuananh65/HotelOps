import { useState } from "react";
import { Link } from "react-router-dom";
export const forgotPasswordInstruction =
  "Để được hỗ trợ mật khẩu, liên hệ quản trị viên qua kênh hỗ trợ nội bộ.";
/** UC001 instruction-only alternative. No lookup, reset, storage or telemetry. */
export default function ForgotPasswordScreen() {
  const [identifier, setIdentifier] = useState("");
  const [required, setRequired] = useState(false),
    [submitted, setSubmitted] = useState(false);
  return (
    <main>
      <h1>Quên mật khẩu</h1>
      <form
        noValidate
        onSubmit={(event) => {
          event.preventDefault();
          if (!identifier.trim()) {
            setRequired(true);
            setSubmitted(false);
            return;
          }
          setRequired(false);
          setSubmitted(true);
          setIdentifier("");
        }}
      >
        <label>
          Tên đăng nhập hoặc email
          <input
            name="identifier"
            autoComplete="off"
            value={identifier}
            aria-invalid={required}
            aria-describedby={required ? "forgot-required" : undefined}
            onChange={(event) => {
              setIdentifier(event.target.value);
              setRequired(false);
              setSubmitted(false);
            }}
          />
        </label>
        {required && (
          <p id="forgot-required" role="alert">
            Nhập tên đăng nhập hoặc email.
          </p>
        )}
        <button type="submit">Hiện hướng dẫn</button>
      </form>
      {submitted && <p role="status">{forgotPasswordInstruction}</p>}
      <Link to="/login">Quay lại đăng nhập</Link>
    </main>
  );
}
