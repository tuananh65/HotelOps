import { createHttpClient } from "../auth/http";
import {
  createRequest,
  createdBranch,
  type CreateBranchRequest,
  type CreateBranchResult,
} from "./createBranchContracts";
/** POST only after explicit confirmation; no retry, logging, browser storage or public tokens. */
export function createBranchApi(
  fetchImplementation: typeof fetch = globalThis.fetch.bind(globalThis),
) {
  const http = createHttpClient(fetchImplementation);
  return async (
    input: CreateBranchRequest,
    stillCurrent: () => boolean,
    signal?: AbortSignal,
  ): Promise<CreateBranchResult> => {
    const request = createRequest(input);
    if (!request || signal?.aborted || !stillCurrent())
      return { outcome: "NOT_DISPATCHED", error: { kind: "invalid-request" } };
    const body = JSON.stringify(request);
    const csrf = await http({
      method: "GET",
      url: "/api/auth/csrf",
      expectedStatus: 200,
      signal,
      decode(v) {
        if (
          typeof v !== "object" ||
          v === null ||
          !("headerName" in v) ||
          v.headerName !== "X-CSRF-TOKEN" ||
          !("csrfToken" in v) ||
          typeof v.csrfToken !== "string" ||
          !/^[\x21-\x7e]{1,8192}$/.test(v.csrfToken)
        )
          return;
        return v.csrfToken;
      },
    });
    if (!csrf.ok)
      return {
        outcome: "NOT_DISPATCHED",
        error:
          csrf.error.kind === "http"
            ? { kind: "http", status: csrf.error.status }
            : { kind: "invalid-response" },
      };
    if (signal?.aborted || !stillCurrent())
      return { outcome: "NOT_DISPATCHED", error: { kind: "stale" } };
    try {
      const response = await fetchImplementation("/api/branches", {
        method: "POST",
        credentials: "include",
        cache: "no-store",
        redirect: "error",
        signal,
        headers: new Headers({
          Accept: "application/json",
          "Content-Type": "application/json",
          "X-CSRF-TOKEN": csrf.data,
        }),
        body,
      });
      if (signal?.aborted || !stillCurrent())
        return { outcome: "OUTCOME_UNKNOWN", error: { kind: "stale" } };
      if (response.status !== 201)
        return {
          outcome: [400, 401, 403, 404, 409].includes(response.status)
            ? "REJECTED"
            : "OUTCOME_UNKNOWN",
          error: { kind: "http", status: response.status },
        };
      const type = response.headers
        .get("Content-Type")
        ?.split(";")[0]
        .trim()
        .toLowerCase();
      if (type !== "application/json")
        return {
          outcome: "OUTCOME_UNKNOWN",
          error: { kind: "invalid-response" },
        };
      let value: unknown;
      try {
        value = JSON.parse(await response.text());
      } catch {
        return {
          outcome: "OUTCOME_UNKNOWN",
          error: { kind: "invalid-response" },
        };
      }
      const data = createdBranch(
        value,
        request.code,
        response.headers.get("ETag"),
      );
      if (signal?.aborted || !stillCurrent())
        return { outcome: "OUTCOME_UNKNOWN", error: { kind: "stale" } };
      return data
        ? { outcome: "CONFIRMED", data }
        : { outcome: "OUTCOME_UNKNOWN", error: { kind: "invalid-response" } };
    } catch {
      return { outcome: "OUTCOME_UNKNOWN", error: { kind: "network" } };
    }
  };
}
