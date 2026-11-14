import { createHttpClient } from "../auth/http";
import { roomDto, safeId, strongTag, type RoomDto } from "../hotel/contracts";
export type RoomLifecycleResult =
  | { outcome: "CONFIRMED"; data: RoomDto; etag: string }
  | {
      outcome: "NOT_DISPATCHED" | "REJECTED" | "OUTCOME_UNKNOWN";
      reason:
        | "DISABLED"
        | "BUSY"
        | "INVALID_REQUEST"
        | "STALE"
        | "CSRF"
        | "HTTP"
        | "NETWORK"
        | "INVALID_RESPONSE";
      status?: number;
    };
/** Technical candidate. No production route imports or flag can enable server release. */
export function createRoomLifecycleApi(
  fetcher: typeof fetch = globalThis.fetch.bind(globalThis),
  enabled = false,
) {
  let busy = false;
  return {
    async change(
      branchId: number,
      roomId: number,
      reactivate: boolean,
      etag: string,
      reason: string,
      current: () => boolean,
      signal?: AbortSignal,
    ): Promise<RoomLifecycleResult> {
      if (busy) return { outcome: "NOT_DISPATCHED", reason: "BUSY" };
      if (!enabled) return { outcome: "NOT_DISPATCHED", reason: "DISABLED" };
      if (
        !safeId(branchId) ||
        !safeId(roomId) ||
        typeof reactivate !== "boolean" ||
        !strongTag(etag) ||
        typeof reason !== "string" ||
        reason.trim().length < 10 ||
        reason.trim().length > 1000 ||
        /[\x00-\x1f\x7f-\x9f]/.test(reason)
      )
        return { outcome: "NOT_DISPATCHED", reason: "INVALID_REQUEST" };
      const body = JSON.stringify({
        reason: reason.trim(),
        confirmation: true,
      });
      if (signal?.aborted || !current())
        return { outcome: "NOT_DISPATCHED", reason: "STALE" };
      busy = true;
      let dispatched = false;
      try {
        let failureStatus: number | undefined;
        const http = createHttpClient(async (url, options) => {
          const response = await fetcher(url, options);
          if (response.status >= 400 && response.status <= 599)
            failureStatus = response.status;
          return response;
        });
        const csrf = await http({
          method: "GET",
          url: "/api/auth/csrf",
          expectedStatus: 200,
          signal,
          decode(v) {
            if (
              v &&
              typeof v === "object" &&
              "headerName" in v &&
              v.headerName === "X-CSRF-TOKEN" &&
              "csrfToken" in v &&
              typeof v.csrfToken === "string" &&
              /^[\x21-\x7e]{1,8192}$/.test(v.csrfToken)
            )
              return v.csrfToken;
          },
        });
        if (!csrf.ok)
          return {
            outcome: "NOT_DISPATCHED",
            reason: "CSRF",
            ...(csrf.error.kind === "http"
              ? { status: csrf.error.status }
              : failureStatus !== undefined
                ? { status: failureStatus }
                : {}),
          };
        if (signal?.aborted || !current())
          return { outcome: "NOT_DISPATCHED", reason: "STALE" };
        dispatched = true;
        const r = await fetcher(
          `/api/rooms/${roomId}/${reactivate ? "reactivate" : "deactivate"}?branchId=${branchId}`,
          {
            method: "POST",
            credentials: "include",
            cache: "no-store",
            redirect: "error",
            signal,
            headers: {
              Accept: "application/json",
              "Content-Type": "application/json",
              "X-CSRF-TOKEN": csrf.data,
              "If-Match": etag,
            },
            body,
          },
        );
        if (signal?.aborted || !current())
          return { outcome: "OUTCOME_UNKNOWN", reason: "STALE" };
        if (r.status === 200) {
          let value: unknown;
          try {
            if (
              r.headers.get("Content-Type")?.split(";")[0].trim() ===
              "application/json"
            )
              value = await r.json();
          } catch {}
          if (signal?.aborted || !current())
            return { outcome: "OUTCOME_UNKNOWN", reason: "STALE" };
          const data = roomDto(value),
            next = r.headers.get("ETag");
          if (
            data &&
            data.id === roomId &&
            data.branchId === branchId &&
            data.isActive === reactivate &&
            (!reactivate || data.operationalStatus !== "READY") &&
            strongTag(next) &&
            next !== etag
          )
            return { outcome: "CONFIRMED", data, etag: next };
          return {
            outcome: "OUTCOME_UNKNOWN",
            reason: "INVALID_RESPONSE",
            status: 200,
          };
        }
        if (r.status === 409) {
          let p: unknown;
          try {
            p = await r.json();
          } catch {}
          if (
            p &&
            typeof p === "object" &&
            "status" in p &&
            p.status === 409 &&
            "code" in p &&
            "title" in p &&
            p.code === p.title &&
            [
              "ROOM_DEPENDENCIES_EXIST",
              "ROOM_PARENT_INELIGIBLE",
              "ROOM_STATE_CONFLICT",
            ].includes(String(p.code))
          )
            return { outcome: "REJECTED", reason: "HTTP", status: 409 };
        }
        if ([400, 401, 403, 404, 412, 428].includes(r.status))
          return { outcome: "REJECTED", reason: "HTTP", status: r.status };
        return { outcome: "OUTCOME_UNKNOWN", reason: "HTTP", status: r.status };
      } catch {
        return {
          outcome: dispatched ? "OUTCOME_UNKNOWN" : "NOT_DISPATCHED",
          reason: "NETWORK",
        };
      } finally {
        busy = false;
      }
    },
  };
}
