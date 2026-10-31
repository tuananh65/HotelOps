import { endpoints, resourcePaths, type ResourceKey } from "./endpoints";
import { request, ApiError, configureSession } from "./http";
import { adaptBackendSession, type BackendSessionDto } from "./authAdapter";
import type {
  EntityDto,
  EntityWriteDto,
  PageDto,
  Query,
  CommandRequest,
  IncidentListDto,
  IncidentDetailDto,
  ScreenContextDto,
  SessionDto,
  LoginRequest,
  PasswordRequest,
  AttachmentDto,
} from "./types";
export function createModuleService(key: ResourceKey) {
  const path = resourcePaths[key];
  return {
    list: (query?: Query, signal?: AbortSignal) =>
      request<PageDto<EntityDto>>(path, { query, signal }),
    get: (id: string, signal?: AbortSignal) =>
      request<EntityDto>(`${path}/${encodeURIComponent(id)}`, { signal }),
    create: (body: EntityWriteDto, signal?: AbortSignal) =>
      request<EntityDto>(path, { method: "POST", body, signal }),
    update: (id: string, body: EntityWriteDto) =>
      request<EntityDto | undefined>(`${path}/${encodeURIComponent(id)}`, {
        method: "PUT",
        body,
      }),
    command: (id: string, operation: string, body: CommandRequest) =>
      request<EntityDto | undefined>(
        `${path}/${encodeURIComponent(id)}/${operation}`,
        { method: "POST", body },
      ),
  };
}
export const moduleServices = Object.fromEntries(
  Object.keys(resourcePaths).map((key) => [
    key,
    createModuleService(key as ResourceKey),
  ]),
) as Record<ResourceKey, ReturnType<typeof createModuleService>>;
export const incidentReads = {
  list: (signal?: AbortSignal) =>
    request<IncidentListDto[]>(resourcePaths.incidents, { signal }),
  get: (id: string, signal?: AbortSignal) =>
    request<IncidentDetailDto>(
      `${resourcePaths.incidents}/${encodeURIComponent(id)}`,
      { signal },
    ),
};
export const authService = {
  csrf: () =>
    request<{ csrfToken: string; headerName: string }>(endpoints.csrf, { public: true }),
  session: async (signal?: AbortSignal): Promise<SessionDto> =>
    adaptBackendSession(await request<BackendSessionDto>(endpoints.session, { signal })),
  login: async (body: LoginRequest): Promise<SessionDto> => {
    // BE1's antiforgery middleware also protects the anonymous login request.
    const bootstrap = await request<{ csrfToken: string; headerName: string }>(
      endpoints.csrf, { public: true },
    );
    if (bootstrap.headerName !== "X-CSRF-TOKEN" || !bootstrap.csrfToken)
      throw new ApiError("CSRF bootstrap response is invalid.", 0, "configuration");
    configureSession(undefined, bootstrap.csrfToken);
    return adaptBackendSession(await request<BackendSessionDto>(endpoints.login, {
      method: "POST", body, public: true,
    }));
  },
  logout: () => request<void>(endpoints.logout, { method: "POST" }),
  changePassword: async (body: PasswordRequest): Promise<SessionDto> =>
    adaptBackendSession(await request<BackendSessionDto>(endpoints.password, {
      method: "POST", body,
    })),
};
export const contextService = {
  get: (screen: number, query: Query, signal?: AbortSignal) =>
    request<ScreenContextDto>(
      screen === 5 ? endpoints.dashboard : endpoints.context(screen),
      { query, signal },
    ),
};
export const attachmentService = {
  async upload(files: File[]): Promise<AttachmentDto[]> {
    const body = new FormData();
    files.forEach((file) => body.append("files", file));
    return request<AttachmentDto[]>(endpoints.attachments, {
      method: "POST",
      body,
    });
  },
};
/** Fetch all pages for existing client-side tables; never silently truncate a collection. */
export async function readAll(
  key: ResourceKey,
  query: Query,
  signal?: AbortSignal,
): Promise<EntityDto[]> {
  const result: EntityDto[] = [];
  let snapshot: string | undefined;
  for (let page = 1; ; page++) {
    const response = await moduleServices[key].list(
      { ...query, page, pageSize: 100, snapshot },
      signal,
    );
    if (
      !response ||
      !Array.isArray(response.items) ||
      response.page !== page ||
      !response.snapshot ||
      !Number.isInteger(response.totalCount) ||
      response.totalCount < 0
    )
      throw new ApiError(
        "The API list does not match PageDto. Check paging and snapshot metadata.",
        0,
        "server",
      );
    if (snapshot && response.snapshot !== snapshot)
      throw new ApiError(
        "The data snapshot changed during pagination. Reload the list.",
        409,
        "conflict",
      );
    snapshot = response.snapshot;
    result.push(...response.items);
    if (result.length >= response.totalCount) return result;
    if (!response.items.length || page >= 10000)
      throw new ApiError(
        "The API returned incomplete pagination.",
        0,
        "server",
      );
  }
}
