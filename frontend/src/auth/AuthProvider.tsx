import {
  createContext,
  useContext,
  useEffect,
  useMemo,
  useState,
  useRef,
  useSyncExternalStore,
  type ReactNode,
} from "react";
import {
  createAuthStore,
  type AuthSnapshot,
  type AuthStore,
} from "./authStore";

export type AuthActions = Readonly<
  Pick<
    AuthStore,
    | "initialize"
    | "revalidate"
    | "login"
    | "logout"
    | "changePassword"
    | "invalidateSession"
  >
>;

export interface AuthContextValue {
  readonly state: AuthSnapshot;
  readonly actions: AuthActions;
}

const AuthContext = createContext<AuthContextValue | null>(null);

export interface AuthProviderProps {
  children: ReactNode;
  /** Initial store only. A different instance requires an explicit provider remount. */
  store?: AuthStore;
  /** Recovery/login controls may use useAuth here; unknown identity never renders children. */
  fallback?: ReactNode;
}

/** Authentication state only: route permission enforcement belongs to a later approved task. */
export function AuthProvider({
  children,
  store: initialStore,
  fallback = null,
}: AuthProviderProps) {
  const [store] = useState(() => initialStore ?? createAuthStore());
  const snapshot = useSyncExternalStore(
    store.subscribe,
    store.getSnapshot,
    store.getSnapshot,
  );
  const [admitted, setAdmitted] = useState(false);
  const startup = useRef<ReturnType<AuthStore["initialize"]> | null>(null);
  // Even an explicitly reused initialized store cannot restore an old provider's admission.
  const state = useMemo<AuthSnapshot>(
    () =>
      admitted
        ? snapshot
        : Object.freeze({
            ...snapshot,
            status: "INITIALIZING",
            session: null,
            pending: "initialize",
            error: null,
          }),
    [admitted, snapshot],
  );
  const actions = useMemo<AuthActions>(
    () =>
      Object.freeze({
        initialize: store.initialize,
        invalidateSession: store.invalidateSession,
        revalidate: store.revalidate,
        login: store.login,
        logout: store.logout,
        changePassword: store.changePassword,
      }),
    [store],
  );
  const value = useMemo(
    () => Object.freeze({ state, actions }),
    [state, actions],
  );

  useEffect(() => {
    let alive = true;
    // StrictMode reuses this flight; a real provider remount explicitly revalidates a reused store.
    if (!startup.current) {
      const current = store.getSnapshot();
      if (current.status === "INITIALIZING" && current.pending === null)
        startup.current = store.initialize();
      else {
        store.invalidateSession(current.authorityGeneration);
        startup.current = store.revalidate();
      }
    }
    void startup.current.then(() => {
      if (alive) setAdmitted(true);
    });
    return () => {
      alive = false;
    };
  }, [store]);

  const unknown =
    state.status === "INITIALIZING" || state.status === "ERROR_UNAVAILABLE";
  return (
    <AuthContext.Provider value={value}>
      {unknown ? fallback : children}
    </AuthContext.Provider>
  );
}

export function useAuth(): AuthContextValue {
  const value = useContext(AuthContext);
  if (value === null)
    throw new Error("useAuth must be used within AuthProvider");
  return value;
}
