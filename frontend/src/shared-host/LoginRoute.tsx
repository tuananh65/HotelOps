import { useCallback, useEffect, useRef, useState } from "react";
import { Navigate, useLocation } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import SignInScreen from "../screens/auth/SignInScreen";
import { approvedReturnDestination, returnUrlFromSearch } from "./returnUrl";
/** Revalidate after login/known-session arrival, then recheck destination capability. */
export function LoginRoute() {
  const { state, actions } = useAuth();
  const location = useLocation();
  const requested = useRef(returnUrlFromSearch(location.search));
  const started = useRef(false);
  const mounted = useRef(false);
  const [target, setTarget] = useState<{
    path: string;
    identity: string;
    expiry: string;
  } | null>(null);
  const [failed, setFailed] = useState(false);
  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
    };
  }, []);
  const restore = useCallback(() => {
    setFailed(false);
    started.current = true;
    void actions
      .revalidate()
      .then((result) => {
        if (!mounted.current) return;
        if (!result.ok) {
          setFailed(true);
          return;
        }
        setTarget({
          path: result.data.mustChangePassword
            ? "/change-password"
            : (approvedReturnDestination(requested.current, true) ??
              "/dashboard"),
          identity: result.data.user.id,
          expiry: result.data.expiresAt,
        });
      })
      .catch(() => {
        if (mounted.current) setFailed(true);
      });
  }, [actions]);
  useEffect(() => {
    if (
      !started.current &&
      state.session &&
      ["AUTHENTICATED", "RESTRICTED"].includes(state.status)
    )
      restore();
  }, [restore, state.status, state.session]);
  if (
    target &&
    state.session?.user.id === target.identity &&
    state.session.expiresAt === target.expiry &&
    (state.status === "AUTHENTICATED" || state.status === "RESTRICTED")
  )
    return <Navigate replace to={target.path} />;
  if (failed || state.status === "ERROR_UNAVAILABLE")
    return (
      <section aria-label="Session recovery">
        <p role="alert">
          Your session could not be confirmed. No destination has been restored.
        </p>
        <button type="button" onClick={restore}>
          Check session
        </button>
      </section>
    );
  if (started.current || state.status === "INITIALIZING")
    return <p role="status">Checking your session...</p>;
  return <SignInScreen />;
}
