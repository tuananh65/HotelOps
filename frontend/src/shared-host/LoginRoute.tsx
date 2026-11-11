import {
  useCallback,
  useEffect,
  useRef,
  useState,
  type MutableRefObject,
} from "react";
import { Navigate, useLocation } from "react-router-dom";
import type { AuthResult, PublicSession } from "../auth/authStore";
import { useAuth } from "../auth/AuthProvider";
import SignInScreen from "../screens/auth/SignInScreen";
import { approvedReturnDestination, returnUrlFromSearch } from "./returnUrl";
/** Revalidate after login/known-session arrival, then recheck destination capability. */
export function LoginRoute() {
  const location = useLocation();
  const flight = useRef<Promise<AuthResult<PublicSession> | undefined> | null>(
    null,
  );
  return (
    <LoginNavigation
      key={location.key + location.pathname + location.search}
      flight={flight}
    />
  );
}
function LoginNavigation({
  flight,
}: {
  flight: MutableRefObject<Promise<
    AuthResult<PublicSession> | undefined
  > | null>;
}) {
  const { state, actions } = useAuth();
  const location = useLocation();
  const requested = useRef(returnUrlFromSearch(location.search));
  const started = useRef(false);
  const [waiting, setWaiting] = useState(false);
  const mounted = useRef(false);
  const [target, setTarget] = useState<{
    path: string;
    session: PublicSession;
  } | null>(null);
  const [failed, setFailed] = useState(false);
  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
    };
  }, []);
  const attempt = useRef(0);
  const restore = useCallback(() => {
    const generation = ++attempt.current;
    setFailed(false);
    started.current = true;
    setWaiting(true);
    const previous = flight.current;
    const current = (async () => {
      // A GET admitted for an older navigation cannot validate this destination.
      if (previous) await previous.catch(() => undefined);
      if (!mounted.current || attempt.current !== generation) return;
      return actions.revalidate();
    })();
    flight.current = current;
    void current
      .then((result) => {
        if (!mounted.current || attempt.current !== generation || !result)
          return;
        if (!result.ok) {
          setFailed(true);
          return;
        }
        setTarget({
          path: result.data.mustChangePassword
            ? "/change-password"
            : (approvedReturnDestination(requested.current, true) ??
              "/dashboard"),
          session: result.data,
        });
      })
      .catch(() => {
        if (mounted.current && attempt.current === generation) setFailed(true);
      });
  }, [actions, flight]);
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
    state.session === target.session &&
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
  if (waiting || state.status === "INITIALIZING")
    return <p role="status">Checking your session...</p>;
  return <SignInScreen />;
}
