import {
  createContext,
  useContext,
  useEffect,
  useMemo,
  useState,
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
    "initialize" | "revalidate" | "login" | "logout" | "changePassword" | "invalidateSession"
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
  const state = useSyncExternalStore(
    store.subscribe,
    store.getSnapshot,
    store.getSnapshot,
  );
  const actions = useMemo<AuthActions>(
    () =>
      Object.freeze({
        initialize: store.initialize,
        revalidate: store.revalidate,
        login: store.login,
        logout: store.logout,
        changePassword: store.changePassword,
        invalidateSession: store.invalidateSession,
      }),
    [store],
  );
  const value = useMemo(
    () => Object.freeze({ state, actions }),
    [state, actions],
  );

  useEffect(() => {
    // No abort on StrictMode cleanup: the stable store deduplicates the replayed setup.
    // useSyncExternalStore owns subscription cleanup; this effect adds no listener/timer.
    void store.initialize();
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
