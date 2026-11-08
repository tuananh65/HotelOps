export interface UserActivationDto {
  readonly userId: number;
  readonly accountStatus: "ACTIVE";
}

export interface UserActivationError {
  readonly kind:
    | "invalid-request"
    | "invalid-response"
    | "http"
    | "network"
    | "timeout"
    | "cancelled";
  readonly reason:
    | "INVALID_REQUEST"
    | "INVALID_IF_MATCH"
    | "REQUEST_TOO_LARGE"
    | "INVALID_RESPONSE"
    | "NETWORK"
    | "TIMEOUT"
    | "CANCELLED"
    | "VALIDATION_FAILED"
    | "CSRF_VALIDATION_FAILED"
    | "HTTPS_REQUIRED"
    | "SESSION_INVALID"
    | "ACTIVATION_NOT_ALLOWED"
    | "PASSWORD_CHANGE_REQUIRED"
    | "USER_NOT_FOUND"
    | "INVALID_STATUS_TRANSITION"
    | "STALE_USER_VERSION"
    | "IF_MATCH_REQUIRED"
    | "UNSUPPORTED_MEDIA_TYPE"
    | "INTERNAL_SERVER_ERROR"
    | "AUTH_SERVICE_UNAVAILABLE"
    | "UNEXPECTED_HTTP";
  readonly status?: number;
}

export type ActivateUserResult =
  | {
      readonly outcome: "CONFIRMED_ACTIVATED";
      readonly status: 200;
      readonly data: UserActivationDto;
      readonly etag: string;
    }
  | {
      readonly outcome: "NOT_DISPATCHED" | "REJECTED" | "OUTCOME_UNKNOWN";
      readonly error: UserActivationError;
    };

export interface UserActivationApi {
  activateUser(
    userId: number,
    etag: string,
    reason: string,
    signal?: AbortSignal,
  ): Promise<ActivateUserResult>;
}
