import { useState } from "react";
import { Link } from "react-router-dom";
export const forgotPasswordInstruction =
  "For password assistance, contact your Administrator through your usual internal support channel.";
/** UC001 instruction-only alternative. No lookup, reset, storage or telemetry. */
export default function ForgotPasswordScreen() {
  const [identifier, setIdentifier] = useState("");
  const [required, setRequired] = useState(false),
    [submitted, setSubmitted] = useState(false);
  return (
    <main>
      <h1>Forgot Password</h1>
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
          Username or email
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
            Enter your username or email.
          </p>
        )}
        <button type="submit">Show instructions</button>
      </form>
      {submitted && <p role="status">{forgotPasswordInstruction}</p>}
      <Link to="/login">Return to sign in</Link>
    </main>
  );
}
