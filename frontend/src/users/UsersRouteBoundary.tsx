import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
} from "react";
import { Link, Navigate, Outlet } from "react-router-dom";
import { useAuth } from "../auth/AuthProvider";
import type { PublicSession } from "../auth/authStore";
import type { UsersApi, UsersApiError } from "./contracts";
import { createUsersApi } from "./usersApi";

interface UsersContextValue {
  readonly api: UsersApi;
  readonly session: PublicSession;
  readonly sessionFailure: (error: UsersApiError) => void;
}
const UsersContext = createContext<UsersContextValue | null>(null);

export function useUsersContext(): UsersContextValue {
  const context = useContext(UsersContext);
  if (context === null)
    throw new Error("Users reads require UsersRouteBoundary");
  return context;
}

/** This layout survives provider fallback switches; it never retains account responses. */
export function UsersRouteBoundary() {
  const { state, actions } = useAuth();
  const [api] = useState(() => createUsersApi());
  const [blocked, setBlocked] = useState(false);
  const [checking, setChecking] = useState(false);
  const blockedRef = useRef(false);
  const inFlight = useRef(false);
  const mounted = useRef(false);
  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
    };
  }, []);

  const checkSession = useCallback(async () => {
    if (inFlight.current) return;
    inFlight.current = true;
    setChecking(true);
    try {
      await actions.revalidate();
    } finally {
      inFlight.current = false;
      if (mounted.current) setChecking(false);
    }
  }, [actions]);

  const sessionFailure = useCallback(
    (error: UsersApiError) => {
      if (
        error.kind !== "http" ||
        (error.status !== 401 &&
          !(error.status === 403 && error.code === "PASSWORD_CHANGE_REQUIRED"))
      )
        return;
      // Concurrent detail/review failures share one recovery flow. A successful GET
      // does not remount/retry Users automatically; only an explicit reload clears it.
      if (blockedRef.current) return;
      blockedRef.current = true;
      setBlocked(true);
      void checkSession();
    },
    [checkSession],
  );
  const context = useMemo(
    () =>
      state.session === null
        ? null
        : {
            api,
            session: state.session,
            sessionFailure,
          },
    [api, state.session, sessionFailure],
  );

  if (state.status === "ANONYMOUS" || state.status === "EXPIRED_INVALID")
    return <Navigate replace to="/login" />;
  if (state.status === "RESTRICTED")
    return <Navigate replace to="/change-password" />;
  if (state.status === "INITIALIZING" || checking)
    return <p role="status">Đang kiểm tra phiên đăng nhập của bạn...</p>;
  if (blocked || state.status === "ERROR_UNAVAILABLE" || context === null) {
    return (
      <main aria-label="Khôi phục phiên người dùng">
        <h1>Khôi phục phiên người dùng</h1>
        <p role="alert">
          Thông tin tài khoản đã ẩn do chưa xác minh được quyền truy cập hoặc phiên đăng nhập. Không tự gửi lại yêu cầu.
        </p>
        {state.status === "AUTHENTICATED" && context !== null ? (
          <button
            type="button"
            onClick={() => {
              blockedRef.current = false;
              setBlocked(false);
            }}
          >
            Tải lại người dùng
          </button>
        ) : (
          <button type="button" onClick={() => void checkSession()}>
            Kiểm tra phiên đăng nhập
          </button>
        )}
        <Link to="/profile">Hồ sơ cá nhân</Link>
      </main>
    );
  }
  // Confirmed authentication permits a request, never a USER_VIEW grant.
  return (
    <UsersContext.Provider value={context}>
      <Outlet />
    </UsersContext.Provider>
  );
}
