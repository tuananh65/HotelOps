import type {
  GetProfileResult,
  UpdateProfileResult,
  UserProfileApi,
  UserProfileDto,
  UserProfileError,
} from "./userProfileContracts";

const object = (v: unknown): v is Record<string, unknown> =>
  typeof v === "object" && v !== null && !Array.isArray(v);
const id = (v: unknown): v is number =>
  typeof v === "number" && Number.isSafeInteger(v) && v > 0;
const strongEtag = (v: unknown): v is string =>
  typeof v === "string" && v.length === 18 && /^"[0-9A-F]{16}"$/.test(v);

function profile(value: unknown, target: number): UserProfileDto | undefined {
  if (
    !object(value) ||
    !id(value.id) ||
    value.id !== target ||
    typeof value.fullName !== "string" ||
    typeof value.email !== "string" ||
    !(value.phone === null || typeof value.phone === "string")
  )
    return;
  return {
    id: value.id,
    fullName: value.fullName,
    email: value.email,
    phone: value.phone,
  };
}

/** Preserve property presence; never evaluate input accessors or caller serialization hooks. */
function snapshot(value: unknown): string | undefined {
  try {
    if (
      !object(value) ||
      ![Object.prototype, null].includes(Object.getPrototypeOf(value))
    )
      return;
    const keys = Reflect.ownKeys(value);
    if (
      !keys.length ||
      keys.some(
        (key) =>
          typeof key !== "string" ||
          !["fullName", "email", "phone"].includes(key),
      )
    )
      return;
    const patch: Record<string, string | null> = {};
    for (const key of keys as string[]) {
      const descriptor = Object.getOwnPropertyDescriptor(value, key);
      if (!descriptor || !("value" in descriptor)) return;
      const field: unknown = descriptor.value;
      if (key === "phone" && field === null) {
        patch.phone = null;
        continue;
      }
      if (typeof field !== "string") return;
      const text = field.trim();
      if (
        text.length > (key === "fullName" ? 200 : key === "email" ? 150 : 20) ||
        (key !== "phone" && !text)
      )
        return;
      patch[key] = key === "phone" && !text ? null : text;
    }
    // Only primitive copied values can reach JSON serialization, before any await.
    return JSON.stringify(patch);
  } catch {
    return undefined;
  }
}

async function json(response: Response): Promise<unknown> {
  const type = response.headers
    .get("Content-Type")
    ?.split(";")[0]
    .trim()
    .toLowerCase();
  if (type !== "application/json" && type !== "application/problem+json")
    return undefined;
  const body = await response.text();
  try {
    return JSON.parse(body);
  } catch {
    return undefined;
  }
}

function httpError(status: number, value: unknown): UserProfileError {
  const p =
    object(value) && (value.status === undefined || value.status === status)
      ? value
      : {};
  let reason: UserProfileError["reason"] = "UNEXPECTED_HTTP";
  switch (status) {
    case 400:
      reason =
        p.code === "CSRF_VALIDATION_FAILED"
          ? "CSRF_VALIDATION_FAILED"
          : "VALIDATION_FAILED";
      break;
    case 401:
      reason = "SESSION_INVALID";
      break;
    case 403:
      reason =
        p.code === "PASSWORD_CHANGE_REQUIRED"
          ? "PASSWORD_CHANGE_REQUIRED"
          : "FORBIDDEN";
      break;
    case 404:
      reason = "NOT_FOUND";
      break;
    case 409:
      if (p.title === "Email is already in use") reason = "EMAIL_IN_USE";
      if (p.title === "LAST_DURABLE_ADMIN") reason = "LAST_DURABLE_ADMIN";
      break;
    case 412:
      reason = "STALE_VERSION";
      break;
    case 428:
      reason = "IF_MATCH_REQUIRED";
      break;
    case 415:
      reason = "UNSUPPORTED_MEDIA_TYPE";
      break;
    case 503:
      reason =
        p.title === "GLOBAL_ADMIN_SERIALIZATION_UNAVAILABLE"
          ? "GLOBAL_ADMIN_SERIALIZATION_UNAVAILABLE"
          : "SERVICE_UNAVAILABLE";
      break;
  }
  return { kind: "http", reason, status };
}

function caught(
  error: unknown,
  signal?: AbortSignal,
  status?: number,
): UserProfileError {
  const cancelled =
    signal?.aborted || (object(error) && error.name === "AbortError");
  return {
    kind: cancelled ? "cancelled" : "network",
    reason: cancelled ? "CANCELLED" : "NETWORK",
    ...(status === undefined ? {} : { status }),
  };
}

/** Stateless management client. ETags are preconditions, never permission or identity evidence. */
export function createUserProfileApi(
  fetchImplementation: typeof fetch = globalThis.fetch.bind(globalThis),
): UserProfileApi {
  const options = (signal?: AbortSignal) =>
    ({
      credentials: "include",
      cache: "no-store",
      redirect: "error",
      signal,
    }) as const;
  return {
    async getProfile(userId, signal): Promise<GetProfileResult> {
      if (!id(userId))
        return {
          ok: false,
          error: { kind: "invalid-request", reason: "INVALID_REQUEST" },
        };
      if (signal?.aborted)
        return { ok: false, error: { kind: "cancelled", reason: "CANCELLED" } };
      let status: number | undefined;
      try {
        const response = await fetchImplementation(
          `/api/users/${userId}/profile`,
          {
            ...options(signal),
            method: "GET",
            headers: { Accept: "application/json" },
          },
        );
        status = response.status;
        const value = await json(response);
        if (signal?.aborted)
          return {
            ok: false,
            error: { kind: "cancelled", reason: "CANCELLED", status },
          };
        if (!response.ok) return { ok: false, error: httpError(status, value) };
        const data = profile(value, userId),
          etag = response.headers.get("ETag");
        if (status !== 200 || !data || !strongEtag(etag))
          return {
            ok: false,
            error: {
              kind: "invalid-response",
              reason: "INVALID_RESPONSE",
              status,
            },
          };
        return { ok: true, status: 200, data, etag };
      } catch (error) {
        return { ok: false, error: caught(error, signal, status) };
      }
    },
    async updateProfile(
      userId,
      etag,
      patch,
      signal,
    ): Promise<UpdateProfileResult> {
      if (!id(userId))
        return {
          outcome: "NOT_DISPATCHED",
          error: { kind: "invalid-request", reason: "INVALID_REQUEST" },
        };
      if (!strongEtag(etag))
        return {
          outcome: "NOT_DISPATCHED",
          error: { kind: "invalid-request", reason: "INVALID_ETAG" },
        };
      const body = snapshot(patch);
      if (body === undefined)
        return {
          outcome: "NOT_DISPATCHED",
          error: { kind: "invalid-request", reason: "INVALID_REQUEST" },
        };
      if (signal?.aborted)
        return {
          outcome: "NOT_DISPATCHED",
          error: { kind: "cancelled", reason: "CANCELLED" },
        };
      let dispatched = false;
      let status: number | undefined;
      try {
        const bootstrap = await fetchImplementation("/api/auth/csrf", {
          ...options(signal),
          method: "GET",
          headers: { Accept: "application/json" },
        });
        const csrf = await json(bootstrap);
        if (signal?.aborted)
          return {
            outcome: "NOT_DISPATCHED",
            error: { kind: "cancelled", reason: "CANCELLED" },
          };
        if (bootstrap.status !== 200)
          return {
            outcome: "NOT_DISPATCHED",
            error: httpError(bootstrap.status, csrf),
          };
        if (
          !object(csrf) ||
          csrf.headerName !== "X-CSRF-TOKEN" ||
          typeof csrf.csrfToken !== "string" ||
          !csrf.csrfToken.length ||
          /[^\x21-\x7e]/.test(csrf.csrfToken)
        )
          return {
            outcome: "NOT_DISPATCHED",
            error: { kind: "invalid-response", reason: "INVALID_RESPONSE" },
          };
        if (signal?.aborted)
          return {
            outcome: "NOT_DISPATCHED",
            error: { kind: "cancelled", reason: "CANCELLED" },
          };
        // Fetch invocation is the conservative dispatch boundary, not a SQL commit receipt.
        dispatched = true;
        const response = await fetchImplementation(`/api/users/${userId}`, {
          ...options(signal),
          method: "PATCH",
          headers: {
            Accept: "application/json",
            "Content-Type": "application/json",
            "X-CSRF-TOKEN": csrf.csrfToken,
            "If-Match": etag,
          },
          body,
        });
        status = response.status;
        const value = await json(response);
        if (signal?.aborted)
          return {
            outcome: "OUTCOME_UNKNOWN",
            error: { kind: "cancelled", reason: "CANCELLED", status },
          };
        if (status === 200) {
          const data = profile(value, userId),
            nextEtag = response.headers.get("ETag");
          if (data && strongEtag(nextEtag))
            return {
              outcome: "CONFIRMED_UPDATED",
              status: 200,
              data,
              etag: nextEtag,
            };
          return {
            outcome: "OUTCOME_UNKNOWN",
            error: {
              kind: "invalid-response",
              reason: "INVALID_RESPONSE",
              status,
            },
          };
        }
        const error = httpError(status, value);
        const rejected =
          [400, 401, 403, 404, 412, 428, 415].includes(status) ||
          (status === 409 &&
            ["EMAIL_IN_USE", "LAST_DURABLE_ADMIN"].includes(error.reason));
        return { outcome: rejected ? "REJECTED" : "OUTCOME_UNKNOWN", error };
      } catch (error) {
        return {
          outcome: dispatched ? "OUTCOME_UNKNOWN" : "NOT_DISPATCHED",
          error: caught(error, signal, status),
        };
      }
    },
  };
}
