import type {
  UserAccountDetailDto,
  UserAccessReviewDto,
  UserDirectoryResponse,
  UsersApi,
  UsersApiError,
  UsersApiResult,
  UsersProblemCode,
} from "./contracts";

export type UsersFetchImplementation = typeof fetch;
type Decoder<T> = (value: unknown) => T;
const int32Max = 2_147_483_647;
const invalidRequest: UsersApiResult<never> = Object.freeze({
  ok: false,
  error: Object.freeze({ kind: "invalid-request" }),
});
const failure = (
  kind: "invalid-response" | "network" | "cancelled",
): UsersApiResult<never> => ({
  ok: false,
  error: { kind },
});

function object(value: unknown): value is Record<string, unknown> {
  return value !== null && typeof value === "object" && !Array.isArray(value);
}

function requireValue<T>(
  value: unknown,
  accepts: (input: unknown) => input is T,
): T {
  if (!accepts(value)) throw new Error("Invalid users response");
  return value;
}
const text: Decoder<string> = (value) =>
  requireValue(value, (v): v is string => typeof v === "string");
const boolean: Decoder<boolean> = (value) =>
  requireValue(value, (v): v is boolean => typeof v === "boolean");
const id: Decoder<number> = (value) =>
  requireValue(
    value,
    (v): v is number =>
      typeof v === "number" && Number.isSafeInteger(v) && v > 0,
  );
const integer =
  (min: number, max: number): Decoder<number> =>
  (value) =>
    requireValue(
      value,
      (v): v is number =>
        typeof v === "number" &&
        Number.isSafeInteger(v) &&
        v >= min &&
        v <= max,
    );
const nullable =
  <T>(decode: Decoder<T>): Decoder<T | null> =>
  (value) =>
    value === null ? null : decode(value);
const array =
  <T>(decode: Decoder<T>): Decoder<T[]> =>
  (value) => {
    if (!Array.isArray(value)) throw new Error("Invalid users response");
    return value.map(decode);
  };

// Required fields are decoded and copied; arbitrary server extensions never escape.
function record<T>(shape: { [K in keyof T]: Decoder<T[K]> }): Decoder<T> {
  return (value) => {
    if (!object(value)) throw new Error("Invalid users response");
    const result = {} as T;
    for (const key of Object.keys(shape) as (keyof T)[])
      result[key] = shape[key](value[key as string]);
    return result;
  };
}

const utc: Decoder<string> = (value) => {
  const result = text(value);
  const parts =
    /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2}):(\d{2})(?:\.\d{1,7})?Z$/.exec(
      result,
    );
  if (!parts) throw new Error("Invalid users response");
  const [year, month, day, hour, minute, second] = parts.slice(1).map(Number);
  const leap = year % 4 === 0 && (year % 100 !== 0 || year % 400 === 0);
  const days = [31, leap ? 29 : 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31];
  if (
    year < 1 ||
    month < 1 ||
    month > 12 ||
    day < 1 ||
    day > days[month - 1] ||
    hour > 23 ||
    minute > 59 ||
    second > 59
  )
    throw new Error("Invalid users response");
  return result;
};

const branch = record({ id, code: text, name: text });
const department = record({ id, code: text, name: text });
const accountFields = {
  id,
  employeeCode: text,
  username: text,
  fullName: text,
  email: text,
  accountStatus: text,
  primaryBranch: nullable(branch),
  department: nullable(department),
};
const directory: Decoder<UserDirectoryResponse> = record({
  items: array(
    record({
      ...accountFields,
      roles: array(
        record({ id, code: text, name: text, branch: nullable(branch) }),
      ),
    }),
  ),
  page: integer(1, int32Max),
  pageSize: integer(1, 100),
  totalCount: integer(0, int32Max),
});
const detail: Decoder<UserAccountDetailDto> = record({
  ...accountFields,
  phone: nullable(text),
});
const review: Decoder<UserAccessReviewDto> = record({
  account: detail,
  evaluatedAtUtc: utc,
  reviewerScope: record({ isGlobal: boolean, branchIds: array(id) }),
  assignedRoles: array(
    record({
      assignmentId: id,
      roleId: id,
      code: text,
      name: text,
      branchId: nullable(id),
      assignmentActive: boolean,
      roleActive: boolean,
      assignedAtUtc: utc,
      expiresAtUtc: nullable(utc),
    }),
  ),
  branchMemberships: array(
    record({
      branchId: id,
      code: text,
      name: text,
      accessLevel: text,
      branchActive: boolean,
      grantedAtUtc: utc,
      revokedAtUtc: nullable(utc),
    }),
  ),
  configuredGrants: array(
    record({
      permissionCode: text,
      permissionName: text,
      feature: nullable(text),
      configuredScope: text,
      assignmentId: id,
      roleCode: text,
      branchId: nullable(id),
      isEffective: boolean,
    }),
  ),
  effectivePermissions: array(
    record({
      code: text,
      name: text,
      feature: nullable(text),
      sources: array(
        record({
          assignmentId: id,
          roleCode: text,
          scope: text,
          branchId: nullable(id),
          requiresResourceOwnership: boolean,
        }),
      ),
    }),
  ),
});

const limitedText =
  (max: number): Decoder<string> =>
  (value) => {
    const result = text(value);
    if (result.length > max) throw new Error("Invalid users query");
    return result;
  };
const status: Decoder<string> = (value) => {
  if (value !== "ACTIVE" && value !== "INACTIVE")
    throw new Error("Invalid users query");
  return value;
};
const listQuery = {
  search: limitedText(200),
  branchId: id,
  departmentId: id,
  roleId: id,
  accountStatus: status,
  page: integer(1, int32Max),
  pageSize: integer(1, 100),
};
const reviewQuery = {
  feature: limitedText(60),
  permissionCode: limitedText(100),
};

function queryString(
  value: unknown,
  shape: Record<string, Decoder<string | number>>,
): string {
  if (
    !object(value) ||
    Object.keys(value).some((key) => !Object.hasOwn(shape, key))
  )
    throw new Error("Invalid users query");
  const params = new URLSearchParams();
  for (const key of Object.keys(shape))
    if (value[key] !== undefined)
      params.set(key, String(shape[key](value[key])));
  const query = params.toString();
  return query ? `?${query}` : "";
}

const codes = new Set<UsersProblemCode>([
  "VALIDATION_FAILED",
  "AUTHENTICATION_REQUIRED",
  "SESSION_INVALID",
  "FORBIDDEN",
  "PERMISSION_DENIED",
  "PASSWORD_CHANGE_REQUIRED",
  "AUTH_SERVICE_UNAVAILABLE",
  "INTERNAL_SERVER_ERROR",
]);
function httpError(status: number, value: unknown): UsersApiError {
  const code =
    object(value) &&
    (value.status === undefined || value.status === status) &&
    typeof value.code === "string" &&
    codes.has(value.code as UsersProblemCode)
      ? (value.code as UsersProblemCode)
      : undefined;
  return { kind: "http", status, ...(code ? { code } : {}) };
}

/** Stateless GET client. Authorization/revalidation and request freshness belong to B-2. */
export function createUsersApi(
  fetchImplementation: UsersFetchImplementation = globalThis.fetch.bind(
    globalThis,
  ),
): UsersApi {
  async function get<T>(
    url: string,
    decode: Decoder<T>,
    signal?: AbortSignal,
  ): Promise<UsersApiResult<T>> {
    if (signal?.aborted) return failure("cancelled");
    try {
      const response = await fetchImplementation(url, {
        method: "GET",
        credentials: "include",
        cache: "no-store",
        redirect: "error",
        headers: new Headers({ Accept: "application/json" }),
        signal,
      });
      const contentType = response.headers
        .get("Content-Type")
        ?.split(";")[0]
        .trim()
        .toLowerCase();
      let value: unknown;
      if (
        contentType === "application/json" ||
        contentType === "application/problem+json"
      ) {
        const body = await response.text();
        try {
          value = JSON.parse(body);
        } catch {
          value = undefined;
        }
      }
      if (signal?.aborted) return failure("cancelled");
      if (!response.ok)
        return { ok: false, error: httpError(response.status, value) };
      if (response.status !== 200 || contentType !== "application/json")
        return failure("invalid-response");
      try {
        return { ok: true, status: 200, data: decode(value) };
      } catch {
        return failure("invalid-response");
      }
    } catch (error) {
      return failure(
        signal?.aborted || (object(error) && error.name === "AbortError")
          ? "cancelled"
          : "network",
      );
    }
  }

  async function validated<T>(
    buildUrl: () => string,
    decode: Decoder<T>,
    signal?: AbortSignal,
  ): Promise<UsersApiResult<T>> {
    let url: string;
    try {
      url = buildUrl();
    } catch {
      return invalidRequest;
    }
    return get(url, decode, signal);
  }

  return Object.freeze({
    listUsers: (query = {}, signal) =>
      validated(
        () => {
          const suffix = queryString(query, listQuery);
          const page = query.page ?? 1,
            pageSize = query.pageSize ?? 20;
          if ((page - 1) * pageSize > int32Max)
            throw new Error("Invalid users query");
          return `/api/users${suffix}`;
        },
        directory,
        signal,
      ),
    getUser: (userId, signal) =>
      validated(() => `/api/users/${id(userId)}`, detail, signal),
    getEffectiveAccess: (userId, query = {}, signal) =>
      validated(
        () =>
          `/api/users/${id(userId)}/effective-access${queryString(query, reviewQuery)}`,
        review,
        signal,
      ),
  } satisfies UsersApi);
}
