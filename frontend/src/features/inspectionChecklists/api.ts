import { useEffect, useMemo } from "react";
import {
  collectionParams,
  collectionQuery,
  validateCollection,
  type CollectionQuery,
  type CollectionPage,
} from "./collection";
import {
  positiveId,
  type Checklist,
  type CreateInput,
  type CriterionInput,
  type RoomType,
} from "./model";
export class ChecklistApiError extends Error {
  constructor(
    public status: number,
    message: string,
    public uncertain = false,
  ) {
    super(message);
  }
}
export interface LoadedChecklist {
  value: Checklist;
  etag: string;
}
type Fetcher = typeof fetch;
const messages: Record<number, string> = {
  400: "Dữ liệu hoặc yêu cầu bảo mật không hợp lệ. Kiểm tra thông tin và thử lại.",
  401: "Phiên đăng nhập backend không hợp lệ. Đăng nhập bằng hệ thống xác thực thật rồi tải lại.",
  403: "Bạn không có quyền thao tác trong chi nhánh này.",
  404: "Không tìm thấy checklist trong phạm vi được phép.",
  409: "Mã trùng hoặc trạng thái đã thay đổi. Kiểm tra bản ghi trước khi tiếp tục.",
  412: "Checklist đã thay đổi. Tải lại bản mới trước khi thao tác.",
  428: "Thiếu phiên bản kiểm soát thay đổi. Tải lại checklist.",
  503: "Dịch vụ hoặc schema chưa sẵn sàng. Kiểm tra kết quả trước khi gửi lại.",
};
export function createChecklistApi(
  send: Fetcher = (...args) => fetch(...args),
  lifetime?: AbortSignal,
) {
  async function request(
    method: string,
    path: string,
    body?: unknown,
    etag?: string,
    signal?: AbortSignal,
  ): Promise<Response> {
    signal =
      signal && lifetime
        ? AbortSignal.any([signal, lifetime])
        : signal || lifetime;
    signal?.throwIfAborted();
    const writing = method !== "GET";
    const headers: Record<string, string> = { Accept: "application/json" };
    if (body !== undefined) headers["Content-Type"] = "application/json";
    if (etag !== undefined) {
      if (!/^"[A-F0-9]{64}"$/.test(etag))
        throw new ChecklistApiError(428, messages[428]);
      headers["If-Match"] = etag;
    }
    if (writing) {
      const bootstrap = await request(
        "GET",
        "/api/auth/csrf",
        undefined,
        undefined,
        signal,
      );
      let token: { csrfToken?: string; headerName?: string };
      try {
        token = await bootstrap.json();
      } catch {
        throw new ChecklistApiError(503, "Không lấy được thông tin bảo mật.");
      }
      if (!token.csrfToken || token.headerName !== "X-CSRF-TOKEN")
        throw new ChecklistApiError(503, "Thông tin bảo mật không hợp lệ.");
      headers[token.headerName] = token.csrfToken;
    }
    signal?.throwIfAborted();
    let response: Response;
    try {
      response = await send(path, {
        method,
        headers,
        body: body === undefined ? undefined : JSON.stringify(body),
        credentials: "include",
        cache: "no-store",
        redirect: "error",
        signal,
      });
    } catch (error) {
      if (error instanceof Error && error.name === "AbortError") throw error;
      throw new ChecklistApiError(
        0,
        writing
          ? "Kết quả chưa xác định. Kiểm tra bản ghi trước khi gửi lại."
          : "Không kết nối được API. Kiểm tra kết nối và tải lại.",
        writing,
      );
    }
    signal?.throwIfAborted();
    if (!response.ok)
      throw new ChecklistApiError(
        response.status,
        messages[response.status] || "Yêu cầu không thành công.",
        writing && response.status >= 500,
      );
    return response;
  }
  const root = (branch: number) =>
    `/api/branches/${positiveId(branch)}/inspection-checklists`;
  async function loaded(
    response: Response,
    branch: number,
    id?: number,
    writing = false,
  ): Promise<LoadedChecklist> {
    try {
      const value = (await response.json()) as Checklist;
      const etag = response.headers.get("ETag") || "";
      if (
        value.branchId !== branch ||
        (id !== undefined && value.id !== id) ||
        !Number.isSafeInteger(value.id) ||
        value.id <= 0 ||
        typeof value.active !== "boolean" ||
        !Array.isArray(value.versions) ||
        value.versions.some(
          (v) =>
            typeof v.usedByInspection !== "boolean" ||
            !["DRAFT", "PUBLISHED", "RETIRED"].includes(v.status) ||
            !Array.isArray(v.criteria),
        ) ||
        !Array.isArray(value.roomTypeIds) ||
        !/^"[A-F0-9]{64}"$/.test(etag)
      )
        throw new Error("Invalid response");
      return { value, etag };
    } catch {
      throw new ChecklistApiError(
        503,
        "Phản hồi API không đầy đủ. Tải lại checklist để kiểm tra.",
        writing,
      );
    }
  }
  return {
    list: async (
      branch: number,
      input: CollectionQuery,
      signal?: AbortSignal,
    ): Promise<CollectionPage> => {
      const query = collectionQuery(input);
      const response = await request(
        "GET",
        `${root(branch)}?${collectionParams(query)}`,
        undefined,
        undefined,
        signal,
      );
      try {
        return validateCollection(
          (await response.json()) as CollectionPage,
          branch,
          query,
        );
      } catch {
        throw new ChecklistApiError(
          503,
          "Danh sách checklist trả về không hợp lệ. Vui lòng tải lại.",
        );
      }
    },
    read: async (branch: number, id: number, signal?: AbortSignal) =>
      loaded(
        await request(
          "GET",
          `${root(branch)}/${positiveId(id)}`,
          undefined,
          undefined,
          signal,
        ),
        branch,
        id,
      ),
    create: async (branch: number, input: CreateInput) =>
      loaded(
        await request("POST", root(branch), input),
        branch,
        undefined,
        true,
      ),
    update: async (
      branch: number,
      id: number,
      etag: string,
      criteria: CriterionInput[],
      reason: string,
    ) =>
      loaded(
        await request(
          "PUT",
          `${root(branch)}/${positiveId(id)}`,
          { criteria, reason },
          etag,
        ),
        branch,
        id,
        true,
      ),
    transition: async (
      branch: number,
      id: number,
      etag: string,
      action: "publish" | "retire",
      reason: string,
      confirmation: boolean,
    ) => {
      if (!confirmation) throw new Error("Cần xác nhận thao tác.");
      return loaded(
        await request(
          "POST",
          `${root(branch)}/${positiveId(id)}/${action}`,
          { reason, confirmation },
          etag,
        ),
        branch,
        id,
        true,
      );
    },
    roomTypes: async (
      branch: number,
      signal?: AbortSignal,
    ): Promise<RoomType[]> => {
      const result: RoomType[] = [];
      let page = 1;
      let total = 0;
      do {
        const response = await request(
          "GET",
          `/api/room-types?branchId=${positiveId(branch)}&isActive=true&page=${page}&pageSize=100`,
          undefined,
          undefined,
          signal,
        );
        const value = (await response.json()) as {
          items: { details: RoomType }[];
          total: number;
        };
        if (
          !Array.isArray(value.items) ||
          !Number.isSafeInteger(value.total) ||
          value.total < 0
        )
          throw new ChecklistApiError(
            503,
            "Danh sách loại phòng không hợp lệ.",
          );
        result.push(
          ...value.items
            .map((i) => i.details)
            .filter((t) => t.branchId === branch && t.isActive),
        );
        total = value.total;
        if (value.items.length === 0) break;
        page++;
      } while ((page - 1) * 100 < total);
      return result;
    },
  };
}

// Component lifetime cancellation also stops writes after a late CSRF bootstrap.
// No authentication authority and no automatic mutation retry.
export function useChecklistApi() {
  const scope = useMemo(() => {
    let controller = new AbortController();
    return {
      api: createChecklistApi((input, init) => {
        controller.signal.throwIfAborted();
        const signal = init?.signal
          ? AbortSignal.any([init.signal, controller.signal])
          : controller.signal;
        return fetch(input, { ...init, signal });
      }),
      activate() {
        if (controller.signal.aborted) controller = new AbortController();
      },
      dispose() {
        controller.abort();
      },
    };
  }, []);
  useEffect(() => {
    scope.activate();
    return () => scope.dispose();
  }, [scope]);
  return scope.api;
}
