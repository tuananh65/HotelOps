import type { ApiProblem, ApiResult } from "./contracts";

export type FetchImplementation = typeof fetch;
export interface HttpRequest<T> {
  method: "GET" | "POST" | "PUT" | "PATCH" | "DELETE";
  url: string;
  expectedStatus: number;
  decode: (value: unknown) => T | undefined;
  body?: unknown;
  csrfToken?: string;
  signal?: AbortSignal;
}

export function isObject(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

function problem(value: unknown, status: number): ApiProblem | null {
  if (!isObject(value)) return null;
  if (value.status !== undefined && value.status !== status) return null;
  const result: ApiProblem = {};
  for (const key of [
    "type",
    "title",
    "detail",
    "instance",
    "code",
    "traceId",
  ] as const) {
    if (value[key] !== undefined) {
      if (typeof value[key] !== "string") return null;
      result[key] = value[key];
    }
  }
  if (value.status !== undefined) result.status = status;
  if (
    isObject(value.parameters) &&
    typeof value.parameters.lockedUntil === "string"
  ) {
    result.parameters = { lockedUntil: value.parameters.lockedUntil };
  }
  return Object.keys(result).length ? result : null;
}

/** No retries, logging, storage or caller-controlled credentials/Authorization. */
export function createHttpClient(
  fetchImplementation: FetchImplementation = globalThis.fetch.bind(globalThis),
) {
  return async function request<T>(
    input: HttpRequest<T>,
  ): Promise<ApiResult<T>> {
    // Reject absolute/protocol-relative URLs, path escapes and encoded separators.
    if (
      !/^\/api\/[a-zA-Z0-9/_-]+$/.test(input.url) ||
      input.url.includes("//")
    ) {
      return { ok: false, error: { kind: "invalid-response" } };
    }
    const headers = new Headers({ Accept: "application/json" });
    if (input.method !== "GET") {
      if (!input.csrfToken || !/^[\x21-\x7e]+$/.test(input.csrfToken)) {
        return { ok: false, error: { kind: "invalid-response" } };
      }
      headers.set("X-CSRF-TOKEN", input.csrfToken);
    }
    if (input.body !== undefined)
      headers.set("Content-Type", "application/json");
    try {
      const response = await fetchImplementation(input.url, {
        method: input.method,
        headers,
        credentials: "include",
        cache: "no-store",
        redirect: "error",
        signal: input.signal,
        body: input.body === undefined ? undefined : JSON.stringify(input.body),
      });
      if (response.status === 204) {
        return input.expectedStatus === 204
          ? { ok: true, status: 204, data: undefined as T }
          : { ok: false, error: { kind: "invalid-response" } };
      }
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
        // Read failures remain network/cancellation errors; syntactically invalid JSON is distinct.
        const text = await response.text();
        try {
          value = JSON.parse(text);
        } catch {
          value = undefined;
        }
      }
      if (!response.ok) {
        return {
          ok: false,
          error: {
            kind: "http",
            status: response.status,
            problem: problem(value, response.status),
          },
        };
      }
      if (response.status !== input.expectedStatus)
        return { ok: false, error: { kind: "invalid-response" } };
      let data: T | undefined;
      try {
        data = input.decode(value);
      } catch {
        return { ok: false, error: { kind: "invalid-response" } };
      }
      return data === undefined
        ? { ok: false, error: { kind: "invalid-response" } }
        : { ok: true, status: response.status, data };
    } catch (error) {
      const cancelled =
        input.signal?.aborted ||
        (isObject(error) && error.name === "AbortError");
      return {
        ok: false,
        error: { kind: cancelled ? "cancelled" : "network" },
      };
    }
  };
}
