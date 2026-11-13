import type {
  AssignUserRoleResult,
  UserRoleAssignmentApi,
  UserRoleAssignmentError,
} from "./userRoleAssignmentContracts";

const object = (value: unknown): value is Record<string, unknown> =>
  typeof value === "object" && value !== null && !Array.isArray(value);
const id = (value: unknown): value is number =>
  typeof value === "number" && Number.isSafeInteger(value) && value > 0;
const strongEtag = (value: unknown): value is string =>
  typeof value === "string" &&
  value.length === 18 &&
  /^"[0-9A-F]{16}"$/.test(value);

const allowedRoles: readonly string[] = [
  "HK_STAFF",
  "HK_SUPERVISOR",
  "MAINTENANCE_STAFF",
  "MAINTENANCE_LEAD",
  "STOREKEEPER",
];
// Match .NET String.Trim, including U+0085 and excluding U+FEFF.
const trimRoleCode = (value: string) =>
  value.replace(
    /^[\u0009-\u000D\u0020\u0085\u00A0\u1680\u2000-\u200A\u2028\u2029\u202F\u205F\u3000]+|[\u0009-\u000D\u0020\u0085\u00A0\u1680\u2000-\u200A\u2028\u2029\u202F\u205F\u3000]+$/g,
    "",
  );
function utcTimestamp(value: unknown): value is string {
  if (typeof value !== "string") return false;
  const match =
    /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2}):(\d{2})(?:\.\d{1,7})?(?:Z|\+00:00)$/.exec(
      value,
    );
  if (!match || match[0] !== value) return false;
  const [year, month, day, hour, minute, second] = match
    .slice(1, 7)
    .map(Number);
  if (
    year < 1 ||
    month < 1 ||
    month > 12 ||
    hour > 23 ||
    minute > 59 ||
    second > 59
  )
    return false;
  const leap = year % 4 === 0 && (year % 100 !== 0 || year % 400 === 0);
  const days = [31, leap ? 29 : 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31];
  return day >= 1 && day <= days[month - 1];
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

function httpError(status: number, value: unknown): UserRoleAssignmentError {
  const p =
    object(value) && (value.status === undefined || value.status === status)
      ? value
      : {};
  const matches = (code: string) => p.code === code || p.title === code;
  let reason: UserRoleAssignmentError["reason"] = "UNEXPECTED_HTTP";
  switch (status) {
    case 400:
      reason = matches("INVALID_IF_MATCH")
        ? "INVALID_IF_MATCH"
        : matches("CSRF_VALIDATION_FAILED")
          ? "CSRF_VALIDATION_FAILED"
          : matches("HTTPS_REQUIRED") || p.title === "HTTPS is required"
            ? "HTTPS_REQUIRED"
            : "VALIDATION_FAILED";
      break;
    case 401:
      reason = "SESSION_INVALID";
      break;
    case 403:
      reason = matches("PASSWORD_CHANGE_REQUIRED")
        ? "PASSWORD_CHANGE_REQUIRED"
        : "ROLE_ASSIGNMENT_NOT_ALLOWED";
      break;
    case 404:
      reason = "USER_NOT_FOUND";
      break;
    case 409: {
      const hasCode = Object.prototype.hasOwnProperty.call(p, "code");
      const hasTitle = Object.prototype.hasOwnProperty.call(p, "title");
      if (hasCode && hasTitle && p.code !== p.title) break;
      const identifier = hasCode ? p.code : p.title;
      if (identifier === "TARGET_NOT_ACTIVE") reason = "TARGET_NOT_ACTIVE";
      else if (identifier === "PRIMARY_SCOPE_CONFLICT")
        reason = "PRIMARY_SCOPE_CONFLICT";
      else if (identifier === "EXISTING_ROLE_PATH_CONFLICT")
        reason = "EXISTING_ROLE_PATH_CONFLICT";
      else if (identifier === "LAST_DURABLE_ADMIN")
        reason = "LAST_DURABLE_ADMIN";
      break;
    }
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
      reason = matches("GLOBAL_ADMIN_SERIALIZATION_UNAVAILABLE")
        ? "GLOBAL_ADMIN_SERIALIZATION_UNAVAILABLE"
        : "AUTH_SERVICE_UNAVAILABLE";
      break;
  }
  return { kind: "http", reason, status };
}

function caught(
  error: unknown,
  signal?: AbortSignal,
  status?: number,
): UserRoleAssignmentError {
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

/** Assign V1 transport only; supported codes and ETags never establish grant authority. */
export function createUserRoleAssignmentApi(
  fetchImplementation: typeof fetch = globalThis.fetch.bind(globalThis),
): UserRoleAssignmentApi {
  return {
    async assignUserRole(
      userId,
      etag,
      roleCode,
      branchId,
      signal,
    ): Promise<AssignUserRoleResult> {
      const invalid = (
        reason: UserRoleAssignmentError["reason"],
      ): AssignUserRoleResult => ({
        outcome: "NOT_DISPATCHED",
        error: { kind: "invalid-request", reason },
      });
      // Validate primitives and freeze serialization before the first await.
      if (!id(userId) || !id(branchId)) return invalid("INVALID_REQUEST");
      if (!strongEtag(etag)) return invalid("INVALID_IF_MATCH");
      if (typeof roleCode !== "string") return invalid("INVALID_ROLE_CODE");
      const normalizedRole = trimRoleCode(roleCode);
      if (
        !normalizedRole ||
        normalizedRole.length > 60 ||
        !allowedRoles.includes(normalizedRole)
      )
        return invalid("INVALID_ROLE_CODE");
      const body = JSON.stringify({ roleCode: normalizedRole, branchId });
      if (new TextEncoder().encode(body).byteLength > 16384)
        return invalid("REQUEST_TOO_LARGE");
      let dispatched = false;
      let status: number | undefined;
      const fail = (error: UserRoleAssignmentError): AssignUserRoleResult => ({
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
          !csrf.csrfToken ||
          csrf.csrfToken.trim() !== csrf.csrfToken ||
          /[^\x20-\x7E]/.test(csrf.csrfToken)
        )
          return fail({
            kind: "invalid-response",
            reason: "INVALID_RESPONSE",
            status,
          });
        const headers = new Headers({
          Accept: "application/json",
          "Content-Type": "application/json",
          "X-CSRF-TOKEN": csrf.csrfToken,
          "If-Match": etag,
        });
        if (signal?.aborted) return fail(caught(undefined, signal, status));
        status = undefined;
        // Even a synchronous fetch failure can be indeterminate; do not wait for a response to mark dispatch.
        dispatched = true;
        const response = await fetchImplementation(
          `/api/users/${userId}/roles`,
          { ...options, method: "POST", headers, body },
        );
        status = response.status;
        const value = await json(response);
        if (signal?.aborted) return fail(caught(undefined, signal, status));
        if (status !== 201) {
          const error = httpError(status, value);
          const knownRejection =
            [400, 401, 403, 404, 412, 428, 413, 415].includes(status) ||
            (status === 409 && error.reason !== "UNEXPECTED_HTTP");
          return knownRejection ? { outcome: "REJECTED", error } : fail(error);
        }
        const nextEtag = response.headers.get("ETag");
        if (
          !object(value) ||
          !id(value.id) ||
          !id(value.userId) ||
          value.userId !== userId ||
          !id(value.branchId) ||
          value.branchId !== branchId ||
          value.roleCode !== normalizedRole ||
          !id(value.assignedBy) ||
          !utcTimestamp(value.assignedAtUtc) ||
          value.isActive !== true ||
          value.expiresAtUtc !== null ||
          !strongEtag(nextEtag) ||
          nextEtag === etag
        )
          return fail({
            kind: "invalid-response",
            reason: "INVALID_RESPONSE",
            status,
          });
        return {
          outcome: "CONFIRMED_ASSIGNED",
          status: 201,
          data: {
            id: value.id,
            userId: value.userId,
            branchId: value.branchId,
            roleCode: value.roleCode,
            assignedBy: value.assignedBy,
            assignedAtUtc: value.assignedAtUtc,
            isActive: true,
            expiresAtUtc: null,
          },
          etag: nextEtag,
        };
      } catch (error) {
        const failure = caught(error, signal, status);
        // Only bootstrap failures are known not to have dispatched a mutation.
        // An unreadable mutation response must retain OUTCOME_UNKNOWN.
        if (
          !dispatched &&
          failure.kind !== "cancelled" &&
          status !== undefined &&
          status >= 400 &&
          status <= 599
        )
          return fail(httpError(status, undefined));
        return fail(failure);
      }
    },
  };
}
