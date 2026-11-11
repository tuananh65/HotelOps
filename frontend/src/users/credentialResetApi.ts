export interface ResetCredential {
  readonly password: string;
  readonly expiresAtUtc: string;
}
export type ResetResult<T> =
  | { outcome: "CONFIRMED"; data: T; etag?: string }
  | {
      outcome: "NOT_DISPATCHED" | "REJECTED" | "OUTCOME_UNKNOWN";
      status?: number;
      code?: "INVALID_CREDENTIALS";
    };
const object = (v: unknown): v is Record<string, unknown> =>
  typeof v === "object" && v !== null && !Array.isArray(v);
const tag = (v: unknown): v is string =>
  typeof v === "string" && /^"[A-F0-9]{16}"$/.test(v);
const utc = (v: unknown): v is string => {
  if (
    typeof v !== "string" ||
    !/^\d{4}-\d\d-\d\dT\d\d:\d\d:\d\d(?:\.\d{1,7})?Z$/.test(v) ||
    Number(v.slice(0, 4)) < 1
  )
    return false;
  const d = new Date(v);
  return (
    Number.isFinite(d.getTime()) &&
    d.toISOString().slice(0, 19) === v.slice(0, 19)
  );
};
async function json(r: Response): Promise<unknown> {
  const type = r.headers
    .get("Content-Type")
    ?.split(";")[0]
    .trim()
    .toLowerCase();
  if (type !== "application/json" && type !== "application/problem+json")
    return undefined;
  try {
    return JSON.parse(await r.text());
  } catch {
    return undefined;
  }
}
/** Uses the already active reset/proof contracts. No proof token, password generation or retry. */
export function createCredentialResetApi(
  fetcher: typeof fetch = globalThis.fetch.bind(globalThis),
) {
  let busy = false;
  async function post<T>(
    url: string,
    body: string,
    decode: (v: unknown, r: Response) => T | undefined,
    current: () => boolean,
    signal: AbortSignal,
    etag?: string,
  ): Promise<ResetResult<T>> {
    if (busy || signal.aborted || !current())
      return { outcome: "NOT_DISPATCHED" };
    busy = true;
    let dispatched = false,
      status: number | undefined;
    const failure = (): ResetResult<T> => ({
      outcome: dispatched ? "OUTCOME_UNKNOWN" : "NOT_DISPATCHED",
      ...(status === undefined ? {} : { status }),
    });
    const options = {
      credentials: "include",
      cache: "no-store",
      redirect: "error",
      signal,
    } as const;
    try {
      const csrf = await fetcher("/api/auth/csrf", {
        ...options,
        method: "GET",
        headers: { Accept: "application/json" },
      });
      status = csrf.status;
      const value = await json(csrf);
      if (status !== 200 || signal.aborted || !current()) return failure();
      if (
        !object(value) ||
        value.headerName !== "X-CSRF-TOKEN" ||
        typeof value.csrfToken !== "string" ||
        !/^[\x21-\x7e]+$/.test(value.csrfToken)
      )
        return failure();
      const headers = new Headers({
        Accept: "application/json",
        "Content-Type": "application/json",
        "X-CSRF-TOKEN": value.csrfToken,
      });
      if (etag) headers.set("If-Match", etag);
      status = undefined;
      dispatched = true;
      const response = await fetcher(url, {
        ...options,
        method: "POST",
        headers,
        body,
      });
      status = response.status;
      const data = await json(response);
      if (signal.aborted || !current()) return failure();
      if (status === 200) {
        const decoded = decode(data, response);
        return decoded === undefined
          ? failure()
          : {
              outcome: "CONFIRMED",
              data: decoded,
              ...(etag ? { etag: response.headers.get("ETag")! } : {}),
            };
      }
      // 409 requires a consistent, known server identifier, never arbitrary ProblemDetails.
      const known409 =
        object(data) &&
        (data.status === undefined || data.status === 409) &&
        (!Object.hasOwn(data, "code") ||
          !Object.hasOwn(data, "title") ||
          data.code === data.title) &&
        ["TARGET_NOT_ACTIVE", "LAST_DURABLE_ADMIN"].includes(
          String(data.code ?? data.title),
        );
      return [400, 401, 403, 404, 412, 413, 415, 423, 428].includes(status) ||
        (status === 409 && known409)
        ? {
            outcome: "REJECTED",
            status,
            ...(url === "/api/auth/reauthenticate" &&
            status === 401 &&
            object(data) &&
            (data.status === undefined || data.status === 401) &&
            data.code === "INVALID_CREDENTIALS" &&
            (!Object.hasOwn(data, "title") ||
              data.title === "INVALID_CREDENTIALS")
              ? { code: "INVALID_CREDENTIALS" as const }
              : {}),
          }
        : failure();
    } catch {
      return failure();
    } finally {
      busy = false;
    }
  }
  return {
    reauthenticate(
      password: string,
      current: () => boolean,
      signal: AbortSignal,
    ): Promise<ResetResult<{ expiresAtUtc: string }>> {
      if (
        typeof password !== "string" ||
        !password.trim() ||
        password.length > 256
      )
        return Promise.resolve({ outcome: "NOT_DISPATCHED" });
      return post(
        "/api/auth/reauthenticate",
        JSON.stringify({ currentPassword: password }),
        (v) =>
          object(v) && utc(v.expiresAtUtc)
            ? { expiresAtUtc: v.expiresAtUtc }
            : undefined,
        current,
        signal,
      );
    },
    reset(
      userId: number,
      etag: string,
      confirmed: boolean,
      current: () => boolean,
      signal: AbortSignal,
    ): Promise<ResetResult<ResetCredential>> {
      if (
        !Number.isSafeInteger(userId) ||
        userId <= 0 ||
        !tag(etag) ||
        confirmed !== true
      )
        return Promise.resolve({ outcome: "NOT_DISPATCHED" });
      return post(
        `/api/users/${userId}/reset-credential`,
        JSON.stringify({ identityVerificationConfirmed: true }),
        (v, r) => {
          const next = r.headers.get("ETag");
          if (
            !tag(next) ||
            next === etag ||
            !object(v) ||
            !object(v.temporaryCredential)
          )
            return;
          const c = v.temporaryCredential;
          if (
            typeof c.password !== "string" ||
            !c.password.trim() ||
            !utc(c.expiresAtUtc)
          )
            return;
          return { password: c.password, expiresAtUtc: c.expiresAtUtc };
        },
        current,
        signal,
        etag,
      );
    },
  };
}
