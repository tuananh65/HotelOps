export interface UserProfileDto {
  readonly id: number;
  readonly fullName: string;
  readonly email: string;
  readonly phone: string | null;
}

export interface UserProfilePatch {
  fullName?: string;
  email?: string;
  phone?: string | null;
}

export interface UserProfileError {
  readonly kind:
    "invalid-request" | "http" | "invalid-response" | "network" | "cancelled";
  readonly reason:
    | "INVALID_REQUEST"
    | "INVALID_ETAG"
    | "INVALID_RESPONSE"
    | "NETWORK"
    | "CANCELLED"
    | "VALIDATION_FAILED"
    | "CSRF_VALIDATION_FAILED"
    | "SESSION_INVALID"
    | "FORBIDDEN"
    | "PASSWORD_CHANGE_REQUIRED"
    | "NOT_FOUND"
    | "EMAIL_IN_USE"
    | "LAST_DURABLE_ADMIN"
    | "STALE_VERSION"
    | "IF_MATCH_REQUIRED"
    | "UNSUPPORTED_MEDIA_TYPE"
    | "GLOBAL_ADMIN_SERIALIZATION_UNAVAILABLE"
    | "SERVICE_UNAVAILABLE"
    | "UNEXPECTED_HTTP";
  readonly status?: number;
}

export type GetProfileResult =
  | {
      readonly ok: true;
      readonly status: 200;
      readonly data: UserProfileDto;
      readonly etag: string;
    }
  | { readonly ok: false; readonly error: UserProfileError };

export type UpdateProfileResult =
  | {
      readonly outcome: "CONFIRMED_UPDATED";
      readonly status: 200;
      readonly data: UserProfileDto;
      readonly etag: string;
    }
  | {
      readonly outcome: "NOT_DISPATCHED" | "REJECTED" | "OUTCOME_UNKNOWN";
      readonly error: UserProfileError;
    };

export interface UserProfileApi {
  getProfile(userId: number, signal?: AbortSignal): Promise<GetProfileResult>;
  updateProfile(
    userId: number,
    etag: string,
    patch: UserProfilePatch,
    signal?: AbortSignal,
  ): Promise<UpdateProfileResult>;
}
