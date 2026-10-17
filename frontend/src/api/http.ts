import systemMessages from "./system-messages.json";
import { apiConfig } from "./config";
import type { ProblemDetails, Query } from "./types";

export type ErrorKind =
  | "unauthenticated"
  | "forbidden"
  | "notFound"
  | "conflict"
  | "validation"
  | "server"
  | "network"
  | "configuration";
export class ApiError extends Error {
  constructor(
    message: string,
    public status: number,
    public kind: ErrorKind,
    public problem: ProblemDetails = {},
    public outcomeUnknown = false,
  ) {
    super(message);
    this.name = "ApiError";
  }
  get fieldErrors() {
    return Object.fromEntries(
      Object.entries(this.problem.errors ?? {}).map(([key, value]) => [
        key,
        Array.isArray(value) ? value.join(" ") : value,
      ]),
    );
  }
}
let bearerToken: string | undefined;
let csrfToken: string | undefined;
let onUnauthorized = () => {};
let sessionRevision = 0;
export const invalidateSessionRequests = () => {
  sessionRevision++;
};
export const configureSession = (token?: string, csrf?: string) => {
  bearerToken = token;
  csrfToken = csrf;
};
export const setUnauthorizedHandler = (handler: () => void) => {
  onUnauthorized = handler;
};

export interface RequestOptions {
  method?: "GET" | "POST" | "PUT" | "PATCH" | "DELETE";
  query?: Query;
  body?: unknown;
  signal?: AbortSignal;
  headers?: HeadersInit;
  responseType?: "json" | "blob";
  public?: boolean;
}
export async function request<T>(
  path: string,
  options: RequestOptions = {},
): Promise<T> {
  const method = options.method ?? "GET";
  const write = method !== "GET";
  const revision = sessionRevision;
  const checkSession = () => {
    if (revision !== sessionRevision)
      throw new ApiError(
        "The session changed while this request was running. Reload before continuing.",
        401,
        "unauthenticated",
        {},
        write,
      );
  };
  const query = new URLSearchParams();
  for (const [key, value] of Object.entries(options.query ?? {}))
    if (value !== undefined && value !== null && value !== "")
      query.set(key, String(value));
  const url = `${apiConfig.baseUrl}${path}${query.size ? `?${query}` : ""}`;
  const headers = new Headers(options.headers);
  headers.set(
    "Accept",
    options.responseType === "blob" ? "*/*" : "application/json",
  );
  if (apiConfig.authMode === "bearer" && bearerToken)
    headers.set("Authorization", `Bearer ${bearerToken}`);
  if (write && csrfToken && apiConfig.authMode === "cookie")
    headers.set(apiConfig.csrfHeader, csrfToken);
  const isForm =
    typeof FormData !== "undefined" && options.body instanceof FormData;
  if (options.body !== undefined && !isForm)
    headers.set("Content-Type", "application/json");
  const controller = new AbortController();
  const abort = () => controller.abort(options.signal?.reason);
  options.signal?.addEventListener("abort", abort, { once: true });
  if (options.signal?.aborted) abort();
  const timeout = setTimeout(() => controller.abort(), apiConfig.timeoutMs);
  try {
    const response = await fetch(url, {
      method,
      headers,
      credentials: apiConfig.authMode === "cookie" ? "include" : "omit",
      body:
        options.body === undefined
          ? undefined
          : isForm
            ? (options.body as FormData)
            : JSON.stringify(options.body),
      signal: controller.signal,
    });
    checkSession();
    if (write && response.status === 202)
      throw new ApiError(
        "The result is not confirmed yet. Please reload the record before trying again.",
        202,
        "conflict",
        { code: "MSG07" },
        true,
      );
    if (!response.ok) {
      let problem: ProblemDetails = {};
      try {
        const parsed: unknown = await response.json();
        if (parsed && typeof parsed === "object" && !Array.isArray(parsed))
          problem = parsed as ProblemDetails;
      } catch {
        /* Empty or non-JSON errors still retain the status. */
      }
      checkSession();
      const kind: ErrorKind =
        response.status === 401
          ? "unauthenticated"
          : response.status === 403
            ? "forbidden"
            : response.status === 404
              ? "notFound"
              : [409, 412].includes(response.status)
                ? "conflict"
                : [400, 422].includes(response.status)
                  ? "validation"
                  : "server";
      const defaults = {
        unauthenticated: "Your session has expired. Please log in again.",
        forbidden: "You do not have permission to perform this action.",
        notFound: "Record not found.",
        conflict:
          "The record or business conditions have changed. Reload before trying again.",
        validation: "Please check the highlighted fields.",
        server: "Unable to complete the request. Please try again.",
        network: "Unable to reach the API.",
        configuration: "API configuration is incomplete.",
      };
      if (response.status === 401 && !options.public) onUnauthorized();
      const template = (systemMessages as Record<string, string>)[
        problem.code ?? ""
      ];
      const catalogMessage = template?.replace(
        /\{(\w+)\}/g,
        (match, key: string) => String(problem.parameters?.[key] ?? match),
      );
      throw new ApiError(
        problem.detail || catalogMessage || problem.title || defaults[kind],
        response.status,
        kind,
        problem,
        write &&
          (problem.code === "MSG07" ||
            (response.status >= 500 && problem.code !== "MSG06")),
      );
    }
    if (response.status === 204 || response.status === 205)
      return undefined as T;
    if (options.responseType === "blob") {
      const blob = await response.blob();
      checkSession();
      return blob as T;
    }
    const raw = await response.text();
    checkSession();
    if (!raw.trim()) return undefined as T;
    try {
      return JSON.parse(raw) as T;
    } catch {
      throw new ApiError(
        "The API returned an invalid JSON response.",
        response.status,
        "server",
        {},
        write,
      );
    }
  } catch (error) {
    if (error instanceof ApiError) throw error;
    if (options.signal?.aborted)
      throw new DOMException("Request cancelled", "AbortError");
    throw new ApiError(
      write
        ? "The result is not confirmed yet. Please reload the record before trying again."
        : "Unable to reach the API. Check the connection and try again.",
      0,
      "network",
      {},
      write,
    );
  } finally {
    clearTimeout(timeout);
    options.signal?.removeEventListener("abort", abort);
  }
}
