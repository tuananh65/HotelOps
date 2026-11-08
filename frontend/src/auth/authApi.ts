import type {
  AuthApi,
  ApiResult,
  CsrfBootstrapDto,
  SessionDto,
} from "./contracts";
import { createHttpClient, isObject, type FetchImplementation } from "./http";

function token(value: unknown): value is string {
  return typeof value === "string" && /^[\x21-\x7e]+$/.test(value);
}
function decodeCsrf(value: unknown): CsrfBootstrapDto | undefined {
  if (
    !isObject(value) ||
    !token(value.csrfToken) ||
    value.headerName !== "X-CSRF-TOKEN"
  )
    return;
  return { csrfToken: value.csrfToken, headerName: value.headerName };
}
function date(value: unknown): value is string {
  return (
    typeof value === "string" &&
    /^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?Z$/.test(value) &&
    Number.isFinite(Date.parse(value))
  );
}
function decodeSession(value: unknown): SessionDto | undefined {
  if (!isObject(value) || !isObject(value.user)) return;
  const user = value.user;
  if (
    typeof user.id !== "string" ||
    !/^[1-9]\d*$/.test(user.id) ||
    typeof user.username !== "string" ||
    !user.username ||
    typeof user.name !== "string" ||
    !user.name ||
    !date(value.expiresAt) ||
    !date(value.absoluteExpiresAt) ||
    Date.parse(value.expiresAt) > Date.parse(value.absoluteExpiresAt) ||
    typeof value.mustChangePassword !== "boolean" ||
    !token(value.csrfToken)
  )
    return;
  // Copy known fields only. Never manufacture profile or permission information.
  return {
    user: { id: user.id, username: user.username, name: user.name },
    expiresAt: value.expiresAt,
    absoluteExpiresAt: value.absoluteExpiresAt,
    mustChangePassword: value.mustChangePassword,
    csrfToken: value.csrfToken,
  };
}

function decodeNormalSession(value: unknown): SessionDto | undefined {
  const session = decodeSession(value);
  return session?.mustChangePassword === false ? session : undefined;
}

/** One isolated instance per browser auth context. Reads never start during identity mutation. */
export function createAuthApi(
  fetchImplementation?: FetchImplementation,
): AuthApi {
  const http = createHttpClient(fetchImplementation);
  let csrf: string | null = null;
  let session: SessionDto | null = null;
  let identityEpoch = 0;
  const readGeneration = { session: 0, csrf: 0 };
  let readSequence = 0;
  let tokenSequence = 0;
  let mutation = false;
  const stale = (): ApiResult<never> => ({
    ok: false,
    error: { kind: "stale" },
  });
  const busy = (): ApiResult<never> => ({ ok: false, error: { kind: "busy" } });

  // Token freshness follows request start order across both read kinds, not arrival order.
  // A session read still applies identity when a newer CSRF read has installed its token.
  function applyToken(value: string | null, sequence: number) {
    if (sequence < tokenSequence) return;
    csrf = value;
    tokenSequence = sequence;
  }

  async function read<T>(
    kind: keyof typeof readGeneration,
    url: string,
    decode: (value: unknown) => T | undefined,
    apply: (result: ApiResult<T>, sequence: number) => void,
    signal?: AbortSignal,
  ): Promise<ApiResult<T>> {
    if (mutation) return busy();
    const epoch = identityEpoch;
    // Latest-started read wins within its kind, even if that newer read fails.
    const generation = ++readGeneration[kind];
    const sequence = ++readSequence;
    const result = await http({
      method: "GET",
      url,
      expectedStatus: 200,
      decode,
      signal,
    });
    if (epoch !== identityEpoch || generation !== readGeneration[kind])
      return stale();
    apply(result, sequence);
    return result;
  }

  async function mutate<T>(
    url: string,
    body: unknown,
    decode: (value: unknown) => T | undefined,
    expectedStatus: number,
    signal?: AbortSignal,
  ): Promise<ApiResult<T>> {
    if (mutation) return busy();
    mutation = true;
    ++identityEpoch; // Invalidate all reads issued before this identity operation.
    try {
      let currentToken = csrf;
      if (!currentToken) {
        const bootstrap = await http({
          method: "GET",
          url: "/api/auth/csrf",
          expectedStatus: 200,
          decode: decodeCsrf,
          signal,
        });
        if (!bootstrap.ok) return bootstrap;
        currentToken = bootstrap.data.csrfToken;
      }
      // A failed/indeterminate identity mutation requires revalidation/bootstrap, never replay.
      csrf = null;
      session = null;
      const result = await http({
        method: "POST",
        url,
        body,
        csrfToken: currentToken,
        expectedStatus,
        decode,
        signal,
      });
      if (result.ok && expectedStatus === 200) {
        session = structuredClone(result.data as SessionDto);
        csrf = session.csrfToken;
      }
      return result;
    } finally {
      mutation = false;
    }
  }

  return {
    async getCsrf(signal) {
      return read(
        "csrf",
        "/api/auth/csrf",
        decodeCsrf,
        (result, sequence) => {
          if (result.ok) applyToken(result.data.csrfToken, sequence);
        },
        signal,
      );
    },
    login: (request, signal) =>
      mutate(
        "/api/auth/login",
        { username: request.username, password: request.password },
        decodeSession,
        200,
        signal,
      ),
    async getSession(signal) {
      return read(
        "session",
        "/api/auth/session",
        decodeSession,
        (result, sequence) => {
          if (result.ok) {
            session = structuredClone(result.data);
            applyToken(session.csrfToken, sequence);
          } else if (
            result.error.kind === "http" &&
            result.error.status === 401
          ) {
            session = null;
            applyToken(null, sequence);
          }
        },
        signal,
      );
    },
    logout: (signal) =>
      mutate<void>("/api/auth/logout", undefined, () => undefined, 204, signal),
    changePassword: (request, signal) =>
      mutate(
        "/api/auth/change-password",
        {
          currentPassword: request.currentPassword,
          newPassword: request.newPassword,
          confirmNewPassword: request.confirmNewPassword,
        },
        decodeNormalSession,
        200,
        signal,
      ),
    getSessionSnapshot: () =>
      session === null ? null : structuredClone(session),
  };
}
