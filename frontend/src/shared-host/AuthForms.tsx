import { useRef } from "react";
import { Link, Navigate } from "react-router-dom";
import ForgotPasswordScreen from "../screens/auth/ForgotPasswordScreen";
import SignInScreen from "../screens/auth/SignInScreen";
import ProfileScreen from "../screens/auth/ProfileScreen";
import ChangePasswordScreen from "../screens/auth/ChangePasswordScreen";
import { useAuth } from "../auth/AuthProvider";
export { ForgotPasswordScreen };
// Auth route lifecycle extracted from accepted AuthRouteHost at 8fd30f2.
export function Recovery() {
  const { state, actions } = useAuth();
  if (state.status === "INITIALIZING")
    return <p role="status">Checking your session...</p>;
  return (
    <section aria-label="Session recovery">
      <p role="alert">
        Your session could not be confirmed. The previous operation's outcome is
        not confirmed. No request has been retried automatically.
      </p>
      <button type="button" onClick={() => void actions.revalidate()}>
        Check session
      </button>
      <Link to="/forgot-password">Contact Administrator</Link>
    </section>
  );
}

type FormKind = "login" | "profile" | "changePassword";

export function AuthFormRoute({ kind }: { kind: FormKind }) {
  const { state } = useAuth();
  // This admission belongs only to this mounted route, never to another URL.
  const admitted = useRef(false);
  const confirmed =
    state.session !== null &&
    (state.status === "AUTHENTICATED" || state.status === "RESTRICTED");
  const anonymous =
    state.status === "ANONYMOUS" || state.status === "EXPIRED_INVALID";
  const allowed = kind === "login" ? anonymous : confirmed;
  if (allowed) admitted.current = true;

  const operation = kind === "profile" ? "logout" : kind;
  const retain =
    admitted.current &&
    (state.pending === operation || state.status === "ERROR_UNAVAILABLE");
  const unknown =
    state.status === "INITIALIZING" || state.status === "ERROR_UNAVAILABLE";

  if (kind === "login" && confirmed)
    return (
      <Navigate
        replace
        to={state.status === "RESTRICTED" ? "/change-password" : "/dashboard"}
      />
    );
  if (kind !== "login" && anonymous) return <Navigate replace to="/login" />;

  const Component =
    kind === "login"
      ? SignInScreen
      : kind === "profile"
        ? ProfileScreen
        : ChangePasswordScreen;
  return (
    <>
      <div>{(allowed || retain) && <Component />}</div>
      {unknown && <Recovery />}
    </>
  );
}
