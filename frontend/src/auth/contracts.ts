export interface CsrfBootstrapDto {
  csrfToken: string;
  headerName: string;
}

export interface LoginRequest {
  username: string;
  password: string;
}

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
  confirmNewPassword: string;
}

export interface AuthUserDto {
  id: string;
  username: string;
  name: string;
}

export interface SessionDto {
  user: AuthUserDto;
  expiresAt: string;
  absoluteExpiresAt: string;
  mustChangePassword: boolean;
  csrfToken: string;
}

/** Known ProblemDetails fields only; arbitrary server extensions are discarded. */
export interface ApiProblem {
  status?: number;
  type?: string;
  title?: string;
  detail?: string;
  instance?: string;
  code?: string;
  traceId?: string;
  parameters?: { lockedUntil?: string };
}

export type ApiFailure =
  | { kind: "http"; status: number; problem: ApiProblem | null }
  | { kind: "network" | "cancelled" | "invalid-response" | "stale" | "busy" };

export type ApiResult<T> =
  { ok: true; status: number; data: T } | { ok: false; error: ApiFailure };

export interface AuthApi {
  getCsrf(signal?: AbortSignal): Promise<ApiResult<CsrfBootstrapDto>>;
  login(
    request: LoginRequest,
    signal?: AbortSignal,
  ): Promise<ApiResult<SessionDto>>;
  getSession(signal?: AbortSignal): Promise<ApiResult<SessionDto>>;
  logout(signal?: AbortSignal): Promise<ApiResult<void>>;
  changePassword(
    request: ChangePasswordRequest,
    signal?: AbortSignal,
  ): Promise<ApiResult<SessionDto>>;
  /** Detached local cache. Null after a failed mutation means unknown, NOT confirmed logout. */
  getSessionSnapshot(): SessionDto | null;
}
