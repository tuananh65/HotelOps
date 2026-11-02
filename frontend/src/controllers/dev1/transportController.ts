/** Isolated DEV1 adapter. Existing auth, HTTP, services and Inventory files are unchanged. */
import { apiConfig } from "../../api/config";
import { ApiError, request } from "../../api/http";
import type { ProblemDetails } from "../../api/types";
import { currentSession, subscribe } from "../../services";

export interface Versioned<T> {
  data: T;
  /** Exact quoted header, not a DTO rowVersion or a fabricated revision. */
  etag: string | null;
  resource: string;
}
export interface CallOptions {
  signal?: AbortSignal;
}
type Fields = Record<string, unknown>;
export function id(value: number): string {
  if (!Number.isSafeInteger(value) || value <= 0)
    throw new ApiError(
      "A positive safe integer ID is required.",
      400,
      "validation",
    );
  return String(value);
}
export function branchPath(branchId: number): string {
  return `/api/branches/${id(branchId)}`;
}
export function queryPath(path: string, fields: object = {}): string {
  const query = new URLSearchParams();
  for (const [key, value] of Object.entries(fields)) {
    if (value === undefined || value === null || value === "") continue;
    if (
      !["string", "number", "boolean"].includes(typeof value) ||
      (typeof value === "number" && !Number.isSafeInteger(value))
    )
      throw new ApiError(`Invalid query field: ${key}`, 400, "validation");
    query.set(key, String(value));
  }
  return path + (query.size ? `?${query}` : "");
}
/** Wire allowlist: do not send UI fields to JsonUnmappedMemberHandling.Disallow DTOs. */
export function pick(input: object, fields: readonly string[]): Fields {
  const source = input as Fields;
  return Object.fromEntries(
    fields
      .filter((key) => source[key] !== undefined)
      .map((key) => [key, source[key]]),
  );
}
function identity() {
  const session = currentSession();
  return session
    ? `${session.userId}:${session.expires}:${session.restricted}`
    : null;
}
async function guarded<T>(
  work: (signal: AbortSignal, check: () => void) => Promise<T>,
  options: CallOptions = {},
  write = false,
): Promise<T> {
  if (apiConfig.authMode !== "cookie")
    throw new ApiError(
      "DEV1 requires the existing cookie authentication configuration.",
      0,
      "configuration",
    );
  const session = currentSession();
  if (!session)
    throw new ApiError(
      "Sign in before using DEV1 controllers.",
      401,
      "unauthenticated",
    );
  if (session.restricted)
    throw new ApiError(
      "Change your temporary password first.",
      403,
      "forbidden",
    );
  const before = identity();
  const controller = new AbortController();
  let changed = false;
  const unsubscribe = subscribe(() => {
    if (identity() !== before) {
      changed = true;
      controller.abort();
    }
  });
  const abort = () => controller.abort(options.signal?.reason);
  options.signal?.addEventListener("abort", abort, { once: true });
  if (options.signal?.aborted) abort();
  const check = () => {
    if (changed || identity() !== before)
      throw new ApiError(
        "Session changed. Reload before continuing.",
        401,
        "unauthenticated",
        {},
        write,
      );
    if (options.signal?.aborted)
      throw new DOMException("Request cancelled", "AbortError");
  };
  try {
    check();
    const result = await work(controller.signal, check);
    check();
    return result;
  } catch (error) {
    check();
    throw error;
  } finally {
    unsubscribe();
    options.signal?.removeEventListener("abort", abort);
  }
}
export function read<T>(path: string, options?: CallOptions): Promise<T> {
  return guarded((signal) => request<T>(path, { signal }), options);
}
/** The old request() discards headers. Only metadata reads use fetch, never auth writes.
 * 401 is surfaced as ApiError; the consuming view must show a sign-in action.
 * No global fetch interception, token storage, login/logout or session mutation here. */
export function readVersioned<T>(
  path: string,
  options?: CallOptions,
): Promise<Versioned<T>> {
  return guarded(async (signal, check) => {
    const controller = new AbortController();
    const abort = () => controller.abort(signal.reason);
    signal.addEventListener("abort", abort, { once: true });
    if (signal.aborted) abort();
    const timer = setTimeout(() => controller.abort(), apiConfig.timeoutMs);
    try {
      const response = await fetch(`${apiConfig.baseUrl}${path}`, {
        credentials: "include",
        cache: "no-store",
        headers: { Accept: "application/json" },
        signal: controller.signal,
      });
      check();
      if (!response.ok) {
        let problem: ProblemDetails = {};
        try {
          const value: unknown = await response.json();
          if (value && typeof value === "object" && !Array.isArray(value))
            problem = value as ProblemDetails;
        } catch {
          /* retain HTTP status */
        }
        check();
        throw new ApiError(
          problem.detail || problem.title || "DEV1 read failed.",
          response.status,
          response.status === 401
            ? "unauthenticated"
            : response.status === 403
              ? "forbidden"
              : response.status === 404
                ? "notFound"
                : [409, 412].includes(response.status)
                  ? "conflict"
                  : [400, 422, 428].includes(response.status)
                    ? "validation"
                    : "server",
          problem,
        );
      }
      let data: T;
      try {
        data = (await response.json()) as T;
      } catch {
        throw new ApiError(
          "Invalid DEV1 JSON response.",
          response.status,
          "server",
        );
      }
      check();
      return { data, etag: response.headers.get("ETag"), resource: path };
    } catch (error) {
      check();
      if (error instanceof ApiError) throw error;
      if (signal.aborted)
        throw new DOMException("Request cancelled", "AbortError");
      throw new ApiError(
        "Unable to read DEV1 API. Check HTTPS, proxy and connection.",
        0,
        "network",
      );
    } finally {
      clearTimeout(timer);
      signal.removeEventListener("abort", abort);
    }
  }, options);
}
export function match(version: Versioned<unknown>, resource: string): string {
  if (version.resource !== resource)
    throw new ApiError(
      "This revision belongs to another resource or branch. Reload the detail.",
      412,
      "conflict",
    );
  if (!version.etag || !/^"[^"\r\n]+"$/.test(version.etag))
    throw new ApiError(
      "Missing strong ETag. Reload the detail; check proxy/CORS header exposure.",
      428,
      "validation",
    );
  return version.etag;
}
/** No automatic retry or automatic re-read before writes: stale forms must fail with 412.
 * Re-read the detail AFTER success to obtain its next ETag. */
export function write<T>(
  path: string,
  method: "POST" | "PUT",
  body: object,
  options?: CallOptions,
  etag?: string,
): Promise<T> {
  return guarded(
    (signal) =>
      request<T>(path, {
        method,
        body,
        signal,
        headers: etag ? { "If-Match": etag } : undefined,
      }),
    options,
    true,
  );
}
export function download(path: string, options?: CallOptions): Promise<Blob> {
  return guarded(
    (signal) => request<Blob>(path, { signal, responseType: "blob" }),
    options,
  );
}
