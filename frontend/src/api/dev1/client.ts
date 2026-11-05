/** DEV1 read transport; auth and CSRF rotation remain owned by DEV3. */
export class ApiError extends Error {
  constructor(
    public readonly status: number,
    public readonly code: string,
  ) {
    super(code);
  }
}

export function apiBase(value = "/api"): string {
  if (!/^\/(?!\/)[a-zA-Z0-9/_-]+$/.test(value)) {
    throw new Error("API base URL must be a same-origin path");
  }
  return value.replace(/\/$/, "");
}

export async function getJson(
  path: string,
  signal?: AbortSignal,
  base = import.meta.env.VITE_DEV1_API_BASE_URL || "/api",
): Promise<unknown> {
  if (!path.startsWith("/") || path.startsWith("//"))
    throw new Error("Invalid API path");
  let response: Response;
  try {
    response = await fetch(`${apiBase(base)}${path}`, {
      method: "GET",
      credentials: "include",
      cache: "no-store",
      redirect: "error",
      headers: { Accept: "application/json" },
      signal,
    });
  } catch (error) {
    if (signal?.aborted) throw error;
    throw new ApiError(0, "NETWORK_UNAVAILABLE");
  }
  const body: unknown = await response.json().catch(() => null);
  if (!response.ok) {
    const problem = body as { code?: unknown; title?: unknown } | null;
    const code = problem?.code ?? problem?.title;
    throw new ApiError(
      response.status,
      typeof code === "string" ? code : "REQUEST_FAILED",
    );
  }
  if (body === null) throw new ApiError(502, "INVALID_RESPONSE");
  return body;
}

export function errorMessage(error: unknown): string {
  if (error instanceof ApiError) {
    if (error.status === 401)
      return "Phiên đăng nhập không hợp lệ hoặc đã hết hạn. Cần phiên máy chủ; đăng nhập prototype không tạo phiên API.";
    if (error.status === 403)
      return "Bạn không có quyền ROOM_VIEW tại chi nhánh này.";
    if (error.status === 400)
      return "Bộ lọc không hợp lệ hoặc kết nối chưa dùng HTTPS.";
    if (error.status === 503)
      return "Dịch vụ phòng hoặc cơ sở dữ liệu tạm thời không khả dụng.";
    if (error.status === 0)
      return "Không kết nối được API. Kiểm tra HTTPS và API proxy rồi thử lại.";
  }
  return "Không tải được dữ liệu phòng. Vui lòng thử lại.";
}
