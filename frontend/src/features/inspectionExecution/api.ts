import {
  decodeInspection,
  positiveId,
  reason,
  strongTag,
  validateResults,
  trustedSeverity,
  type LoadedInspection,
  type CriterionResult,
  type EvidenceUpload,
  type SeverityOption,
} from "./model";
export class InspectionApiError extends Error {
  constructor(
    public status: number,
    message: string,
    public uncertain = false,
  ) {
    super(message);
  }
}
type Fetcher = typeof fetch;
export interface RequestAuthority {
  signal: AbortSignal;
  onSessionInvalid?: () => void;
}
/** Operation-level transport. No retry after an unknown mutation outcome. */
export function createInspectionApi(
  authority: RequestAuthority,
  send: Fetcher = (...args) => fetch(...args),
) {
  const { signal: lifetime } = authority;
  const root = (branch: number, id: number) =>
    `/api/branches/${positiveId(branch)}/inspections/${positiveId(id)}`;
  async function request(
    method: string,
    url: string,
    body?: unknown,
    etag?: string,
    signal?: AbortSignal,
  ): Promise<Response> {
    const active = signal ? AbortSignal.any([signal, lifetime]) : lifetime;
    active.throwIfAborted();
    if (method !== "GET" && !strongTag(etag || ""))
      throw new InspectionApiError(428, "Strong If-Match required");
    const headers: Record<string, string> = { Accept: "application/json" };
    if (body !== undefined) headers["Content-Type"] = "application/json";
    if (etag) headers["If-Match"] = etag;
    if (method !== "GET") {
      const csrf = await request(
        "GET",
        "/api/auth/csrf",
        undefined,
        undefined,
        active,
      );
      let token: unknown;
      try {
        token = await csrf.json();
      } catch {
        throw new InspectionApiError(503, "CSRF unavailable");
      }
      const t = token as { csrfToken?: string; headerName?: string };
      if (
        !t ||
        typeof t.csrfToken !== "string" ||
        !t.csrfToken ||
        t.headerName !== "X-CSRF-TOKEN"
      )
        throw new InspectionApiError(503, "CSRF unavailable");
      headers[t.headerName] = t.csrfToken;
    }
    active.throwIfAborted(); // Protect CSRF -> mutation dispatch gap.
    let response: Response;
    try {
      response = await send(url, {
        method,
        headers,
        body: body === undefined ? undefined : JSON.stringify(body),
        credentials: "include",
        redirect: "error",
        cache: "no-store",
        signal: active,
      });
    } catch (e) {
      if (active.aborted) throw active.reason;
      throw new InspectionApiError(
        0,
        method === "GET"
          ? "Network unavailable"
          : "Write outcome unknown; verify before any retry",
        method !== "GET",
      );
    }
    active.throwIfAborted();
    if (!response.ok) {
      if (response.status === 401) authority.onSessionInvalid?.(); // Synchronous, while operation remains live.
      throw new InspectionApiError(
        response.status,
        response.status === 401
          ? "Session invalid"
          : response.status === 403
            ? "Resource access denied"
            : response.status === 412
              ? "Stale inspection revision"
              : response.status === 428
                ? "Missing revision"
                : `Request failed (${response.status})`,
        method !== "GET" && response.status >= 500,
      );
    }
    return response;
  }
  async function loaded(
    response: Response,
    branch: number,
    id: number,
    writing = false,
  ): Promise<LoadedInspection> {
    let raw: unknown;
    try {
      raw = await response.json();
    } catch {
      throw new InspectionApiError(
        503,
        "Malformed execution response",
        writing,
      );
    }
    lifetime.throwIfAborted();
    try {
      return decodeInspection(
        raw,
        branch,
        id,
        response.headers.get("ETag") || "",
      );
    } catch {
      throw new InspectionApiError(503, "Untrusted inspection source", writing);
    }
  }
  return {
    read: async (branch: number, id: number, signal?: AbortSignal) =>
      loaded(
        await request(
          "GET",
          `${root(branch, id)}/execution`,
          undefined,
          undefined,
          signal,
        ),
        branch,
        id,
      ),
    start: async (
      branch: number,
      id: number,
      etag: string,
      inputReason: string,
      signal?: AbortSignal,
    ) =>
      loaded(
        await request(
          "POST",
          `${root(branch, id)}/start`,
          { reason: reason(inputReason) },
          etag,
          signal,
        ),
        branch,
        id,
        true,
      ),
    save: async (
      branch: number,
      id: number,
      loadedSource: LoadedInspection,
      results: CriterionResult[],
      inputReason: string,
      signal?: AbortSignal,
    ) => {
      if (
        loadedSource.value.branchId !== branch ||
        loadedSource.value.id !== id
      )
        throw new InspectionApiError(400, "Invalid source scope");
      return loaded(
        await request(
          "PUT",
          `${root(branch, id)}/results`,
          {
            results: validateResults(loadedSource.value, results),
            reason: reason(inputReason),
          },
          loadedSource.etag,
          signal,
        ),
        branch,
        id,
        true,
      );
    },
    finish: async (
      branch: number,
      id: number,
      etag: string,
      inputReason: string,
      signal?: AbortSignal,
    ) => {
      const result = await loaded(
        await request(
          "POST",
          `${root(branch, id)}/finish`,
          { reason: reason(inputReason) },
          etag,
          signal,
        ),
        branch,
        id,
        true,
      );
      if (result.value.state !== "PENDING_CONCLUSION")
        throw new InspectionApiError(
          503,
          "Unexpected finish state; do not retry",
          true,
        );
      return result;
    },
    defect: async (
      branch: number,
      id: number,
      etag: string,
      options: SeverityOption[] | undefined,
      details: {
        itemId: number;
        severityId: number;
        description: string;
        location: string;
        photos?: EvidenceUpload[];
      },
      signal?: AbortSignal,
    ) => {
      if (!trustedSeverity(options, details.severityId))
        throw new InspectionApiError(
          403,
          "Authoritative severity catalog required",
        );
      if (
        !positiveId(details.itemId) ||
        !details.description.trim() ||
        !details.location.trim() ||
        details.description.length > 4000 ||
        details.location.length > 250 ||
        (details.photos?.length ?? 0) > 5
      )
        throw new InspectionApiError(400, "Invalid defect");
      return loaded(
        await request(
          "POST",
          `${root(branch, id)}/defects`,
          details,
          etag,
          signal,
        ),
        branch,
        id,
        true,
      );
    },
  };
}
