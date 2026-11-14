import { createHttpClient, isObject } from "../auth/http";
import { createAuthApi } from "../auth/authApi";
import type { ApiResult } from "../auth/contracts";
import type { Field, Operation, Values } from "./contracts";

export function positiveId(value: unknown): number {
  if (typeof value !== "string" && typeof value !== "number")
    throw Error("A positive numeric ID is required.");
  if (!/^[1-9]\d*$/.test(String(value)) || !Number.isSafeInteger(Number(value)))
    throw Error("A safe positive numeric ID is required.");
  return Number(value);
}
export function payload(
  fields: Field[],
  values: Values,
  actor: string,
): Values {
  const result: Values = {};
  for (const f of fields) {
    if (f.name === "actorUserId" || f.name === "reportedBy") {
      result[f.name] = positiveId(actor);
      continue;
    }
    const value = values[f.name] ?? f.default;
    if (value === "" || value === null || value === undefined) {
      if (!f.optional) throw Error(`${f.name} is required.`);
      continue;
    }
    if (f.fields) {
      if (!Array.isArray(value))
        throw Error(`${f.name} must contain line entries.`);
      if (!f.optional && value.length === 0)
        throw Error(`${f.name} needs at least one line.`);
      result[f.name] = value.map((v) => {
        if (!isObject(v)) throw Error("Invalid line.");
        return payload(f.fields!, v, actor);
      });
    } else if (/^(long|int|decimal)/.test(f.type)) {
      const number = Number(value);
      if (
        !Number.isFinite(number) ||
        (/^(long|int)/.test(f.type) && !Number.isSafeInteger(number))
      )
        throw Error(`${f.name} must be a valid number.`);
      if ((f.name.endsWith("Id") || f.name === "id") && number <= 0)
        throw Error(`${f.name} must be positive.`);
      result[f.name] = number;
    } else if (f.type.startsWith("bool")) result[f.name] = value === true;
    else if (f.type.startsWith("DateTime")) {
      const date = new Date(String(value));
      if (Number.isNaN(date.getTime()))
        throw Error(`${f.name} must be a valid date/time.`);
      result[f.name] = date.toISOString();
    } else result[f.name] = String(value);
  }
  return result;
}
export function operationPath(
  operation: Operation,
  parameters: Values,
): string {
  return operation.route.replace(/\{(\w+):long\}/g, (_, key: string) =>
    String(positiveId(parameters[key])),
  );
}
/** Exact catalog routes only. Shared transport retains credential, CSRF, cache and redirect policy. */
export function createDev2Api(
  fetcher: typeof fetch = globalThis.fetch.bind(globalThis),
) {
  const auth = createAuthApi(fetcher);
  return async (
    operation: Operation,
    parameters: Values,
    values: Values,
    actor: string,
    signal?: AbortSignal,
  ): Promise<ApiResult<unknown>> => {
    let path: string, data: Values;
    try {
      path = operationPath(operation, parameters);
      data = payload(operation.fields, values, actor);
    } catch {
      return { ok: false, error: { kind: "invalid-response" } };
    }
    const query = new URLSearchParams();
    if (operation.method === "GET")
      for (const [key, value] of Object.entries(data)) {
        if (typeof value === "object")
          return { ok: false, error: { kind: "invalid-response" } };
        query.set(key, String(value));
      }
    let csrfToken: string | undefined;
    if (operation.method !== "GET") {
      const session = await auth.getSession(signal);
      if (!session.ok) return session;
      if (session.data.mustChangePassword)
        return {
          ok: false,
          error: {
            kind: "http",
            status: 403,
            problem: { code: "PASSWORD_CHANGE_REQUIRED" },
          },
        };
      if (session.data.user.id !== actor)
        return { ok: false, error: { kind: "stale" } };
      csrfToken = session.data.csrfToken;
    }
    const client = createHttpClient((url, init) =>
      fetcher(String(url) + (query.size ? "?" + query.toString() : ""), init),
    );
    return client({
      method: operation.method,
      url: path,
      expectedStatus: operation.expectedStatus,
      body: operation.method === "GET" ? undefined : data,
      csrfToken,
      signal,
      decode: (value) =>
        value !== null && (isObject(value) || Array.isArray(value))
          ? value
          : undefined,
    });
  };
}
