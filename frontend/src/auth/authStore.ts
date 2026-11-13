import { createAuthApi } from "./authApi";
import type {
  ApiFailure,
  ApiResult,
  AuthApi,
  ChangePasswordRequest,
  LoginRequest,
  SessionDto,
} from "./contracts";

export type AuthStatus =
  | "INITIALIZING"
  | "ANONYMOUS"
  | "AUTHENTICATED"
  | "RESTRICTED"
  | "EXPIRED_INVALID"
  | "ERROR_UNAVAILABLE";

export interface PublicSession {
  readonly user: Readonly<{ id: string; username: string; name: string }>;
  readonly expiresAt: string;
  readonly absoluteExpiresAt: string;
  readonly mustChangePassword: boolean;
}

/** Deliberately excludes raw ProblemDetails, tokens, submitted values and exception text. */
export interface AuthError {
  readonly kind: ApiFailure["kind"];
  readonly status?: number;
  readonly code?: string;
}

export type AuthResult<T> =
  | { readonly ok: true; readonly data: T }
  | { readonly ok: false; readonly error: AuthError };

export interface AuthSnapshot {
  readonly status: AuthStatus;
  readonly session: PublicSession | null;
  readonly pending:
    "initialize" | "revalidate" | "login" | "logout" | "changePassword" | null;
  readonly error: AuthError | null;
}

export interface AuthStore {
  initialize(): Promise<AuthResult<PublicSession>>;
  revalidate(signal?: AbortSignal): Promise<AuthResult<PublicSession>>;
  login(
    request: LoginRequest,
    signal?: AbortSignal,
  ): Promise<AuthResult<PublicSession>>;
  logout(signal?: AbortSignal): Promise<AuthResult<void>>;
  changePassword(
    request: ChangePasswordRequest,
    signal?: AbortSignal,
  ): Promise<AuthResult<PublicSession>>;
  subscribe(listener: () => void): () => void;
  getSnapshot(): AuthSnapshot;
}

const knownCodes = new Set([
  "VALIDATION_FAILED",
  "CSRF_VALIDATION_FAILED",
  "INVALID_CREDENTIALS",
  "TEMPORARY_PASSWORD_EXPIRED",
  "ACCOUNT_LOCKED",
  "AUTHENTICATED_SESSION_EXISTS",
  "SESSION_INVALID",
  "AUTHENTICATION_REQUIRED",
  "FORBIDDEN",
  "PASSWORD_CHANGE_REQUIRED",
  "PASSWORD_POLICY_VIOLATION",
  "CURRENT_PASSWORD_INVALID",
  "AUTH_STATE_CHANGED",
  "AUTH_SERVICE_UNAVAILABLE",
  "INTERNAL_SERVER_ERROR",
]);

function publicError(error: ApiFailure): AuthError {
  if (error.kind !== "http") return Object.freeze({ kind: error.kind });
  const code = error.problem?.code;
  return Object.freeze({
    kind: "http",
    status: error.status,
    ...(code && knownCodes.has(code) ? { code } : {}),
  });
}

function publicSession(dto: SessionDto): PublicSession {
  return Object.freeze({
    user: Object.freeze({
      id: dto.user.id,
      username: dto.user.username,
      name: dto.user.name,
    }),
    expiresAt: dto.expiresAt,
    absoluteExpiresAt: dto.absoluteExpiresAt,
    mustChangePassword: dto.mustChangePassword,
  });
}

const failure = (kind: "busy" | "stale"): AuthResult<never> => ({
  ok: false,
  error: Object.freeze({ kind }),
});

/** Own one accepted AuthApi instance exclusively; do not share it with other stores/callers. */
export function createAuthStore(api: AuthApi = createAuthApi()): AuthStore {
  let snapshot: AuthSnapshot = Object.freeze({
    status: "INITIALIZING",
    session: null,
    pending: null,
    error: null,
  });
  const listeners = new Set<() => void>();
  let epoch = 0;
  let mutation = false;
  let hadIdentity = false;
  let initialization: Promise<AuthResult<PublicSession>> | undefined;
  let activeRead:
    { epoch: number; promise: Promise<AuthResult<PublicSession>> } | undefined;

  function publish(next: AuthSnapshot) {
    snapshot = Object.freeze(next);
    for (const listener of [...listeners]) listener();
  }

  async function invoke<T>(
    action: () => Promise<ApiResult<T>>,
  ): Promise<ApiResult<T>> {
    try {
      return await action();
    } catch {
      return { ok: false, error: { kind: "network" } };
    }
  }

  function accept(dto: SessionDto): AuthResult<PublicSession> {
    const session = publicSession(dto);
    hadIdentity = true;
    publish({
      status: session.mustChangePassword ? "RESTRICTED" : "AUTHENTICATED",
      session,
      pending: null,
      error: null,
    });
    return { ok: true, data: session };
  }

  // Single-flight reads share the first caller's signal; use revalidate after it settles to retry.
  function read(
    pending: "initialize" | "revalidate",
    signal?: AbortSignal,
  ): Promise<AuthResult<PublicSession>> {
    if (mutation) return Promise.resolve(failure("busy"));
    if (activeRead?.epoch === epoch) return activeRead.promise;
    const generation = epoch;
    // Defer execution until the single-flight record exists (including subscription callbacks).
    const promise = Promise.resolve()
      .then(async (): Promise<AuthResult<PublicSession>> => {
        if (generation !== epoch) return failure("stale");
        if (pending === "initialize")
          publish({
            status: "INITIALIZING",
            session: null,
            pending,
            error: null,
          });
        // A subscriber may start a mutation while initialization is published.
        if (generation !== epoch) return failure("stale");
        const result = await invoke(() => api.getSession(signal));
        if (generation !== epoch) return failure("stale");
        if (result.ok) return accept(result.data);
        const error = publicError(result.error);
        publish({
          status:
            error.kind === "http" && error.status === 401
              ? hadIdentity
                ? "EXPIRED_INVALID"
                : "ANONYMOUS"
              : "ERROR_UNAVAILABLE",
          session: null,
          pending: null,
          error,
        });
        return { ok: false, error };
      })
      .finally(() => {
        if (activeRead?.promise === promise) activeRead = undefined;
      });
    activeRead = { epoch: generation, promise };
    // Revoke local admission immediately, not one microtask after recovery begins.
    // Register the flight first so synchronous subscribers cannot create another GET.
    if (pending === "revalidate")
      publish({
        status: "INITIALIZING",
        session: null,
        pending,
        error: null,
      });
    return promise;
  }

  async function mutate<T extends SessionDto | void>(
    operation: "login" | "logout" | "changePassword",
    action: () => Promise<ApiResult<T>>,
  ): Promise<AuthResult<PublicSession | void>> {
    if (mutation) return failure("busy");
    mutation = true;
    ++epoch; // Store protection is independent of transport stale-response protection.
    const previous = snapshot;
    publish({
      status: "INITIALIZING",
      session: null,
      pending: operation,
      error: null,
    });
    try {
      const result = await invoke(action);
      if (result.ok) {
        if (operation !== "logout") return accept(result.data as SessionDto);
        hadIdentity = false;
        publish({
          status: "ANONYMOUS",
          session: null,
          pending: null,
          error: null,
        });
        return { ok: true, data: undefined };
      }
      const error = publicError(result.error);
      let status: AuthStatus = "ERROR_UNAVAILABLE";
      let session: PublicSession | null = null;
      if (error.kind === "http") {
        if (
          operation === "login" &&
          ((error.status === 400 && error.code === "VALIDATION_FAILED") ||
            (error.status === 401 && error.code === "INVALID_CREDENTIALS") ||
            (error.status === 403 &&
              error.code === "TEMPORARY_PASSWORD_EXPIRED") ||
            (error.status === 423 && error.code === "ACCOUNT_LOCKED"))
        )
          status = "ANONYMOUS";
        else if (operation !== "login" && error.status === 401)
          status = "EXPIRED_INVALID";
        else if (
          operation === "changePassword" &&
          error.status === 400 &&
          [
            "VALIDATION_FAILED",
            "PASSWORD_POLICY_VIOLATION",
            "CURRENT_PASSWORD_INVALID",
          ].includes(error.code ?? "") &&
          previous.session !== null
        ) {
          status = previous.session.mustChangePassword
            ? "RESTRICTED"
            : "AUTHENTICATED";
          session = previous.session;
        }
      }
      // Uncertain POST outcomes never imply commit/rollback or trigger an automatic retry.
      publish({ status, session, pending: null, error });
      return { ok: false, error };
    } finally {
      mutation = false;
    }
  }

  return {
    initialize() {
      // StrictMode effect replay shares this promise; explicit revalidate retries a failed read.
      initialization ??= read("initialize");
      return initialization;
    },
    revalidate: (signal) => read("revalidate", signal),
    login: (request, signal) =>
      mutate("login", () => api.login(request, signal)) as Promise<
        AuthResult<PublicSession>
      >,
    logout: (signal) =>
      mutate("logout", () => api.logout(signal)) as Promise<AuthResult<void>>,
    changePassword: (request, signal) =>
      mutate("changePassword", () =>
        api.changePassword(request, signal),
      ) as Promise<AuthResult<PublicSession>>,
    subscribe(listener) {
      listeners.add(listener);
      return () => {
        listeners.delete(listener);
      };
    },
    getSnapshot: () => snapshot,
  };
}
