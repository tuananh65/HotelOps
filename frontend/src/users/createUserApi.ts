import type {
  CreateUserApi,
  CreateUserFailure,
  CreateUserRequest,
  CreateUserResponse,
  CreateUserResult,
  DuplicateField,
  InitialRoleCode,
} from "./createUserContracts";

const roles: readonly string[] = [
  "HK_STAFF",
  "HK_SUPERVISOR",
  "MAINTENANCE_STAFF",
  "MAINTENANCE_LEAD",
  "STOREKEEPER",
  "RECEPTION",
];
const fields = [
  "employeeCode",
  "username",
  "fullName",
  "email",
  "phone",
  "branchId",
  "departmentId",
  "roleCode",
];
const duplicates: readonly string[] = ["employeeCode", "username", "email"];
const object = (v: unknown): v is Record<string, unknown> =>
  typeof v === "object" && v !== null && !Array.isArray(v);
const id = (v: unknown): v is number =>
  typeof v === "number" && Number.isSafeInteger(v) && v > 0;
const text = (v: unknown, max: number): v is string =>
  typeof v === "string" && v.trim().length > 0 && v.length <= max;
const role = (v: unknown): v is InitialRoleCode =>
  typeof v === "string" && roles.includes(v);

// Copy only own data properties. Reject aliases, accessors and caller-supplied serialization hooks.
function snapshot(input: unknown): CreateUserRequest | undefined {
  if (
    !object(input) ||
    ![Object.prototype, null].includes(Object.getPrototypeOf(input))
  )
    return;
  if (
    Reflect.ownKeys(input).some(
      (key) => typeof key !== "string" || !fields.includes(key),
    )
  )
    return;
  const values: Record<string, unknown> = {};
  for (const key of fields) {
    const descriptor = Object.getOwnPropertyDescriptor(input, key);
    if (descriptor && !("value" in descriptor)) return;
    const value: unknown = descriptor?.value;
    values[key] = typeof value === "string" ? value.trim() : value;
  }
  if (
    !text(values.employeeCode, 40) ||
    !text(values.username, 100) ||
    !text(values.fullName, 200) ||
    !text(values.email, 150) ||
    !id(values.branchId) ||
    !id(values.departmentId) ||
    !role(values.roleCode)
  )
    return;
  const phone =
    values.phone === undefined || values.phone === null || values.phone === ""
      ? null
      : values.phone;
  if (phone !== null && !text(phone, 20)) return;
  // Email format/collation and master relationships remain server-authoritative.
  return {
    employeeCode: values.employeeCode,
    username: values.username,
    fullName: values.fullName,
    email: values.email,
    phone,
    branchId: values.branchId,
    departmentId: values.departmentId,
    roleCode: values.roleCode,
  };
}

function timestamp(v: unknown): v is string {
  if (typeof v !== "string") return false;
  const match =
    /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2}):(\d{2})(?:\.\d{1,7})?Z$/.exec(v);
  if (!match || Number(match[1]) < 1) return false;
  const date = new Date(v);
  return (
    Number.isFinite(date.getTime()) &&
    date.getUTCFullYear() === Number(match[1]) &&
    date.getUTCMonth() + 1 === Number(match[2]) &&
    date.getUTCDate() === Number(match[3]) &&
    date.getUTCHours() === Number(match[4]) &&
    date.getUTCMinutes() === Number(match[5]) &&
    date.getUTCSeconds() === Number(match[6])
  );
}

function decode(v: unknown): CreateUserResponse | undefined {
  if (!object(v) || !object(v.user) || !object(v.temporaryCredential)) return;
  const u = v.user,
    c = v.temporaryCredential;
  if (
    !id(u.id) ||
    !text(u.employeeCode, 40) ||
    !text(u.username, 100) ||
    !text(u.fullName, 200) ||
    !text(u.email, 150) ||
    !(u.phone === null || text(u.phone, 20)) ||
    !id(u.branchId) ||
    !id(u.departmentId) ||
    !role(u.roleCode) ||
    u.accountStatus !== "ACTIVE" ||
    typeof c.password !== "string" ||
    c.password.trim().length === 0 ||
    !timestamp(c.expiresAtUtc)
  )
    return;
  return {
    user: {
      id: u.id,
      employeeCode: u.employeeCode,
      username: u.username,
      fullName: u.fullName,
      email: u.email,
      phone: u.phone,
      branchId: u.branchId,
      departmentId: u.departmentId,
      roleCode: u.roleCode,
      accountStatus: u.accountStatus,
    },
    temporaryCredential: { password: c.password, expiresAtUtc: c.expiresAtUtc },
  };
}

async function json(response: Response): Promise<unknown> {
  const type = response.headers
    .get("Content-Type")
    ?.split(";")[0]
    .trim()
    .toLowerCase();
  if (type !== "application/json" && type !== "application/problem+json")
    return undefined;
  const body = await response.text(); // Read failures propagate; never retain the raw body in results.
  try {
    return JSON.parse(body);
  } catch {
    return undefined;
  }
}

function failure(status: number, value: unknown): CreateUserFailure {
  const p =
    object(value) && (value.status === undefined || value.status === status)
      ? value
      : {};
  if (status === 400)
    return {
      status,
      reason:
        p.code === "CSRF_VALIDATION_FAILED"
          ? "CSRF_VALIDATION_FAILED"
          : "VALIDATION_FAILED",
    };
  if (status === 401) return { status, reason: "SESSION_INVALID" };
  if (status === 403)
    return {
      status,
      reason:
        p.code === "PASSWORD_CHANGE_REQUIRED"
          ? "PASSWORD_CHANGE_REQUIRED"
          : "FORBIDDEN",
    };
  if (status === 409 && p.title === "LAST_DURABLE_ADMIN")
    return { status, reason: "LAST_DURABLE_ADMIN" };
  if (
    status === 409 &&
    p.title === "Employee code, username or email already exists" &&
    Array.isArray(p.duplicateFields) &&
    p.duplicateFields.every(
      (f) => typeof f === "string" && duplicates.includes(f),
    )
  )
    return {
      status,
      reason: "DUPLICATE",
      duplicateFields: [...new Set(p.duplicateFields)] as DuplicateField[],
    };
  if (status === 415) return { status, reason: "UNSUPPORTED_MEDIA_TYPE" };
  if (status === 503)
    return {
      status,
      reason:
        p.title === "GLOBAL_ADMIN_SERIALIZATION_UNAVAILABLE"
          ? "GLOBAL_ADMIN_SERIALIZATION_UNAVAILABLE"
          : "SERVICE_UNAVAILABLE",
    };
  return { status, reason: "UNEXPECTED_HTTP" };
}

/** Per-attempt CSRF only. No identity/session mutation, storage, logging or POST retry. */
export function createCreateUserApi(
  fetchImplementation: typeof fetch = globalThis.fetch.bind(globalThis),
): CreateUserApi {
  return {
    async createUser(input, signal): Promise<CreateUserResult> {
      let dispatched = false;
      let status: number | undefined;
      try {
        const request = snapshot(input);
        if (!request)
          return {
            outcome: "NOT_DISPATCHED",
            error: { reason: "INVALID_REQUEST" },
          };
        const body = JSON.stringify(request); // Snapshot serialization precedes the first await.
        if (signal?.aborted)
          return { outcome: "NOT_DISPATCHED", error: { reason: "CANCELLED" } };
        const options = {
          credentials: "include",
          cache: "no-store",
          redirect: "error",
          signal,
        } as const;
        const bootstrap = await fetchImplementation("/api/auth/csrf", {
          ...options,
          method: "GET",
          headers: { Accept: "application/json" },
        });
        const csrf = await json(bootstrap);
        if (bootstrap.status !== 200)
          return {
            outcome: "NOT_DISPATCHED",
            error: failure(bootstrap.status, csrf),
          };
        if (
          !object(csrf) ||
          csrf.headerName !== "X-CSRF-TOKEN" ||
          typeof csrf.csrfToken !== "string" ||
          !/^[\x21-\x7e]+$/.test(csrf.csrfToken)
        )
          return {
            outcome: "NOT_DISPATCHED",
            error: { reason: "INVALID_RESPONSE" },
          };
        if (signal?.aborted)
          return { outcome: "NOT_DISPATCHED", error: { reason: "CANCELLED" } };
        // Once fetch is invoked, a thrown error/abort is not proof that SQL rolled back.
        dispatched = true;
        const response = await fetchImplementation("/api/users", {
          ...options,
          method: "POST",
          headers: {
            Accept: "application/json",
            "Content-Type": "application/json",
            "X-CSRF-TOKEN": csrf.csrfToken,
          },
          body,
        });
        status = response.status;
        const value = await json(response);
        if (signal?.aborted)
          return {
            outcome: "OUTCOME_UNKNOWN",
            error: { reason: "CANCELLED", status },
          };
        if (status === 201) {
          const data = decode(value);
          const location = response.headers.get("Location");
          if (data && location === `/api/users/${data.user.id}`)
            return {
              outcome: "CONFIRMED_CREATED",
              status: 201,
              location,
              data,
            };
          return {
            outcome: "OUTCOME_UNKNOWN",
            error: { reason: "INVALID_RESPONSE", status },
          };
        }
        const error = failure(status, value);
        const rejected =
          [400, 401, 403, 415].includes(status) ||
          (status === 409 &&
            ["DUPLICATE", "LAST_DURABLE_ADMIN"].includes(error.reason));
        return { outcome: rejected ? "REJECTED" : "OUTCOME_UNKNOWN", error };
      } catch (error) {
        return {
          outcome: dispatched ? "OUTCOME_UNKNOWN" : "NOT_DISPATCHED",
          error: {
            reason:
              signal?.aborted || (object(error) && error.name === "AbortError")
                ? "CANCELLED"
                : "NETWORK",
            ...(status === undefined ? {} : { status }),
          },
        };
      }
    },
  };
}
