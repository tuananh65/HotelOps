import type {
  ActivateUserResult,
  UserActivationApi,
  UserActivationError,
} from "./userActivationContracts";

const object = (value: unknown): value is Record<string, unknown> =>
  typeof value === "object" && value !== null && !Array.isArray(value);
const validId = (value: unknown): value is number =>
  typeof value === "number" && Number.isSafeInteger(value) && value > 0;
const strongEtag = (value: unknown): value is string =>
  typeof value === "string" &&
  value.length === 18 &&
  /^"[0-9A-F]{16}"$/.test(value);

// .NET String.Trim whitespace; JS trim additionally removes FEFF and misses 0085.
const trimReason = (value: string) =>
  value.replace(
    /^[\u0009-\u000D\u0020\u0085\u00A0\u1680\u2000-\u200A\u2028\u2029\u202F\u205F\u3000]+|[\u0009-\u000D\u0020\u0085\u00A0\u1680\u2000-\u200A\u2028\u2029\u202F\u205F\u3000]+$/g,
    "",
  );

async function json(response: Response): Promise<unknown> {
  const type = response.headers
    .get("Content-Type")
    ?.split(";")[0]
    .trim()
    .toLowerCase();
  if (type !== "application/json" && type !== "application/problem+json")
    return undefined;
  const text = await response.text();
  try {
    return JSON.parse(text);
  } catch {
    return undefined;
  }
}

function httpError(status: number, value: unknown): UserActivationError {
  const p =
    object(value) && (value.status === undefined || value.status === status)
      ? value
      : {};
  const matches = (code: string) => p.code === code || p.title === code;
  let reason: UserActivationError["reason"] = "UNEXPECTED_HTTP";
  switch (status) {
    case 400:
      reason = matches("CSRF_VALIDATION_FAILED")
        ? "CSRF_VALIDATION_FAILED"
        : matches("INVALID_IF_MATCH")
          ? "INVALID_IF_MATCH"
          : p.title === "HTTPS is required"
            ? "HTTPS_REQUIRED"
            : "VALIDATION_FAILED";
      break;
    case 401:
      reason = "SESSION_INVALID";
      break;
    case 403:
      reason = matches("PASSWORD_CHANGE_REQUIRED")
        ? "PASSWORD_CHANGE_REQUIRED"
        : "ACTIVATION_NOT_ALLOWED";
      break;
    case 404:
      reason = "USER_NOT_FOUND";
      break;
    case 409:
      reason = "INVALID_STATUS_TRANSITION";
      break;
    case 412:
      reason = "STALE_USER_VERSION";
      break;
    case 428:
      reason = "IF_MATCH_REQUIRED";
      break;
    case 413:
      reason = "REQUEST_TOO_LARGE";
      break;
    case 415:
      reason = "UNSUPPORTED_MEDIA_TYPE";
      break;
    case 500:
      reason = "INTERNAL_SERVER_ERROR";
      break;
    case 503:
      reason = "AUTH_SERVICE_UNAVAILABLE";
      break;
  }
  return { kind: "http", reason, status };
}

function caught(
  error: unknown,
  signal?: AbortSignal,
  status?: number,
): UserActivationError {
  const name = object(error) ? error.name : undefined;
  const kind =
    signal?.aborted || name === "AbortError"
      ? "cancelled"
      : name === "TimeoutError"
        ? "timeout"
        : "network";
  return {
    kind,
    reason:
      kind === "cancelled"
        ? "CANCELLED"
        : kind === "timeout"
          ? "TIMEOUT"
          : "NETWORK",
    ...(status === undefined ? {} : { status }),
  };
}

/** Per-attempt CSRF only. ETag is not authority; activation does not guarantee login. */
export function createUserActivationApi(
  fetchImplementation: typeof fetch = globalThis.fetch.bind(globalThis),
): UserActivationApi {
  return {
    async activateUser(
      userId,
      etag,
      reason,
      signal,
    ): Promise<ActivateUserResult> {
      const invalid = (
        code: UserActivationError["reason"],
      ): ActivateUserResult => ({
        outcome: "NOT_DISPATCHED",
        error: { kind: "invalid-request", reason: code },
      });
      // Primitive validation and immutable body snapshot all precede the first await.
      if (!validId(userId) || typeof reason !== "string")
        return invalid("INVALID_REQUEST");
      if (!strongEtag(etag)) return invalid("INVALID_IF_MATCH");
      const trimmed = trimReason(reason);
      if (trimmed.length < 10) return invalid("VALIDATION_FAILED");
      const body = JSON.stringify({ reason: trimmed });
      if (new TextEncoder().encode(body).byteLength > 16 * 1024)
        return invalid("REQUEST_TOO_LARGE");
      let dispatched = false;
      let status: number | undefined;
      const fail = (error: UserActivationError): ActivateUserResult => ({
        outcome: dispatched ? "OUTCOME_UNKNOWN" : "NOT_DISPATCHED",
        error,
      });
      const options = {
        credentials: "include",
        cache: "no-store",
        redirect: "error",
        signal,
      } as const;
      try {
        if (signal?.aborted) return fail(caught(undefined, signal));
        const bootstrap = await fetchImplementation("/api/auth/csrf", {
          ...options,
          method: "GET",
          headers: { Accept: "application/json" },
        });
        status = bootstrap.status;
        const csrf = await json(bootstrap);
        if (signal?.aborted) return fail(caught(undefined, signal, status));
        if (status !== 200) return fail(httpError(status, csrf));
        if (
          !object(csrf) ||
          csrf.headerName !== "X-CSRF-TOKEN" ||
          typeof csrf.csrfToken !== "string" ||
          !csrf.csrfToken.trim() ||
          /[\r\n\u0000]/.test(csrf.csrfToken)
        )
          return fail({
            kind: "invalid-response",
            reason: "INVALID_RESPONSE",
            status,
          });
        // Validate header values before marking PATCH as potentially dispatched.
        const headers = new Headers({
          Accept: "application/json",
          "Content-Type": "application/json",
          "X-CSRF-TOKEN": csrf.csrfToken,
          "If-Match": etag,
        });
        if (signal?.aborted) return fail(caught(undefined, signal, status));
        status = undefined;
        dispatched = true;
        const response = await fetchImplementation(
          `/api/users/${userId}/activate`,
          {
            ...options,
            method: "PATCH",
            headers,
            body,
          },
        );
        status = response.status;
        const value = await json(response);
        if (signal?.aborted) return fail(caught(undefined, signal, status));
        if (status !== 200) {
          const error = httpError(status, value);
          return [400, 401, 403, 404, 409, 412, 428, 413, 415].includes(status)
            ? { outcome: "REJECTED", error }
            : fail(error);
        }
        const nextEtag = response.headers.get("ETag");
        if (
          !object(value) ||
          !validId(value.userId) ||
          value.userId !== userId ||
          value.accountStatus !== "ACTIVE" ||
          !strongEtag(nextEtag) ||
          nextEtag === etag
        )
          return fail({
            kind: "invalid-response",
            reason: "INVALID_RESPONSE",
            status,
          });
        return {
          outcome: "CONFIRMED_ACTIVATED",
          status: 200,
          data: { userId: value.userId, accountStatus: "ACTIVE" },
          etag: nextEtag,
        };
      } catch (error) {
        return fail(caught(error, signal, status));
      }
    },
  };
}
