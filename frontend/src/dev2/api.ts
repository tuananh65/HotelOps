import { createHttpClient, isObject } from "../auth/http";
import { createAuthApi } from "../auth/authApi";
import type { ApiResult } from "../auth/contracts";
import type { Field, Operation, Values } from "./contracts";
import { fieldLabel } from "../i18n/vi";

export function positiveId(value: unknown): number {
  if (typeof value !== "string" && typeof value !== "number")
    throw Error("Mã định danh phải là số nguyên dương.");
  if (!/^[1-9]\d*$/.test(String(value)) || !Number.isSafeInteger(Number(value)))
    throw Error("Mã định danh phải là số nguyên dương trong giới hạn cho phép.");
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
      if (!f.optional) throw Error(`${fieldLabel(f.name)} là bắt buộc.`);
      continue;
    }
    if (f.fields) {
      if (!Array.isArray(value))
        throw Error(`${fieldLabel(f.name)} phải có các dòng dữ liệu.`);
      if (!f.optional && value.length === 0)
        throw Error(`${fieldLabel(f.name)} cần ít nhất một dòng.`);
      result[f.name] = value.map((v) => {
        if (!isObject(v)) throw Error("Dòng không hợp lệ.");
        return payload(f.fields!, v, actor);
      });
    } else if (/^(long|int|decimal)/.test(f.type)) {
      const number = Number(value);
      if (
        !Number.isFinite(number) ||
        (/^(long|int)/.test(f.type) && !Number.isSafeInteger(number))
      )
        throw Error(`${fieldLabel(f.name)} phải là số hợp lệ.`);
      if ((f.name.endsWith("Id") || f.name === "id") && number <= 0)
        throw Error(`${fieldLabel(f.name)} phải lớn hơn 0.`);
      result[f.name] = number;
    } else if (f.type.startsWith("bool")) result[f.name] = value === true;
    else if (f.type.startsWith("DateTime")) {
      const date = new Date(String(value));
      if (Number.isNaN(date.getTime()))
        throw Error(`${fieldLabel(f.name)} phải là ngày giờ hợp lệ.`);
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
      if (operation.id === "A004A-021") {
        if (typeof values.similarReviewToken !== "string" || !values.similarReviewToken || values.similarReviewToken.length > 4096)
          throw Error("Xem sự cố tương tự trước khi tạo sự cố mới.");
        data.similarReviewToken = values.similarReviewToken;
        if (values.occurrenceLink != null) {
          if (!isObject(values.occurrenceLink) || typeof values.occurrenceLink.version !== "string"
            || !/^"[0-9A-F]{16}"$/.test(values.occurrenceLink.version) || typeof values.occurrenceLink.failedCheckKey !== "string"
            || !values.occurrenceLink.failedCheckKey || values.occurrenceLink.failedCheckKey.length > 64)
            throw Error("Liên kết lần thực hiện không hợp lệ.");
          data.occurrenceLink = { runId: positiveId(values.occurrenceLink.runId), version: values.occurrenceLink.version, failedCheckKey: values.occurrenceLink.failedCheckKey };
        }
        if (values.files != null) {
          if (!Array.isArray(values.files) || values.files.length > 5 || values.files.some(f => !isObject(f)
            || typeof f.name !== "string" || typeof f.mimeType !== "string" || typeof f.base64 !== "string"))
            throw Error("Bằng chứng sự cố không hợp lệ.");
          data.files = values.files;
        }
      }
      if (["A004A-021", "A004A-043"].includes(operation.id) && (values.internalRequestId != null || values.requestLink != null)) {
        if (!isObject(values.requestLink) || typeof values.requestLink.version !== "string"
          || !/^[A-Za-z0-9+/]{11}=$/.test(values.requestLink.version) || typeof values.requestLink.mandatory !== "boolean")
          throw Error("Cần phiên bản nguồn đã tải và điều kiện hoàn tất rõ ràng.");
        data.internalRequestId = positiveId(values.internalRequestId);
        data.requestLink = { version: values.requestLink.version, mandatory: values.requestLink.mandatory };
      }
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
