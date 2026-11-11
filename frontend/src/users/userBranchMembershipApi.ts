import type {
  AddBranchMembershipResult,
  UserBranchMembershipApi,
  UserBranchMembershipError,
} from "./userBranchMembershipContracts";

const object = (value: unknown): value is Record<string, unknown> =>
  typeof value === "object" && value !== null && !Array.isArray(value);
const id = (value: unknown): value is number =>
  typeof value === "number" && Number.isSafeInteger(value) && value > 0;
const strongEtag = (value: unknown): value is string =>
  typeof value === "string" &&
  value.length === 18 &&
  /^"[0-9A-F]{16}"$/.test(value);

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

function httpError(status: number, value: unknown): UserBranchMembershipError {
  const p =
    object(value) && (value.status === undefined || value.status === status)
      ? value
      : {};
  const matches = (code: string) => p.code === code || p.title === code;
  let reason: UserBranchMembershipError["reason"] = "UNEXPECTED_HTTP";
  switch (status) {
    case 400:
      reason = matches("INVALID_IF_MATCH")
        ? "INVALID_IF_MATCH"
        : matches("BRANCH_UNAVAILABLE")
          ? "BRANCH_UNAVAILABLE"
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
        : "BRANCH_ASSIGNMENT_NOT_ALLOWED";
      break;
    case 404:
      reason = "USER_NOT_FOUND";
      break;
    case 409:
      // Conflicting or malformed identifiers cannot prove a rejected mutation.
      if (
        Object.hasOwn(p, "code") &&
        Object.hasOwn(p, "title") &&
        p.code !== p.title
      )
        break;
      if (matches("TARGET_NOT_ACTIVE")) reason = "TARGET_NOT_ACTIVE";
      else if (matches("MEMBERSHIP_EXISTS")) reason = "MEMBERSHIP_EXISTS";
      else if (matches("EXISTING_ROLE_PATH_CONFLICT"))
        reason = "EXISTING_ROLE_PATH_CONFLICT";
      else if (matches("OPERATIONS_MANAGER_SCOPE_CONFLICT"))
        reason = "OPERATIONS_MANAGER_SCOPE_CONFLICT";
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
): UserBranchMembershipError {
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

/** Stateless add-only transport. IDs and ETags are selectors/preconditions, never authority. */
export function createUserBranchMembershipApi(
  fetchImplementation: typeof fetch = globalThis.fetch.bind(globalThis),
): UserBranchMembershipApi {
  return {
    async addBranchMembership(
      userId,
      etag,
      branchId,
      signal,
    ): Promise<AddBranchMembershipResult> {
      const invalid = (
        reason: UserBranchMembershipError["reason"],
      ): AddBranchMembershipResult => ({
        outcome: "NOT_DISPATCHED",
        error: { kind: "invalid-request", reason },
      });
      // Validate primitives and freeze serialization before the first await.
      if (!id(userId) || !id(branchId)) return invalid("INVALID_REQUEST");
      if (!strongEtag(etag)) return invalid("INVALID_IF_MATCH");
      const body = JSON.stringify({ branchId });
      if (new TextEncoder().encode(body).byteLength > 16384)
        return invalid("REQUEST_TOO_LARGE");
      let dispatched = false;
      let status: number | undefined;
      const fail = (
        error: UserBranchMembershipError,
      ): AddBranchMembershipResult => ({
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
          `/api/users/${userId}/branches`,
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
          !id(value.accessId) ||
          !id(value.userId) ||
          value.userId !== userId ||
          !id(value.branchId) ||
          value.branchId !== branchId ||
          value.accessLevel !== "STANDARD" ||
          value.isPrimary !== false ||
          !strongEtag(nextEtag) ||
          nextEtag === etag
        )
          return fail({
            kind: "invalid-response",
            reason: "INVALID_RESPONSE",
            status,
          });
        return {
          outcome: "CONFIRMED_ADDED",
          status: 201,
          data: {
            accessId: value.accessId,
            userId: value.userId,
            branchId: value.branchId,
            accessLevel: "STANDARD",
            isPrimary: false,
          },
          etag: nextEtag,
        };
      } catch (error) {
        return fail(caught(error, signal, status));
      }
    },
  };
}
