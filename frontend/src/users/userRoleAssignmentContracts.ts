export interface UserRoleAssignmentDto {
  readonly id: number;
  readonly userId: number;
  readonly branchId: number;
  readonly roleCode: string;
  readonly assignedBy: number;
  readonly assignedAtUtc: string;
  readonly isActive: true;
  readonly expiresAtUtc: null;
}

export interface UserRoleAssignmentError {
  readonly kind:
    | "invalid-request"
    | "invalid-response"
    | "http"
    | "network"
    | "timeout"
    | "cancelled";
  readonly reason:
    | "INVALID_REQUEST"
    | "INVALID_ROLE_CODE"
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
    | "ROLE_ASSIGNMENT_NOT_ALLOWED"
    | "PASSWORD_CHANGE_REQUIRED"
    | "USER_NOT_FOUND"
    | "TARGET_NOT_ACTIVE"
    | "EXISTING_ROLE_PATH_CONFLICT"
    | "PRIMARY_SCOPE_CONFLICT"
    | "LAST_DURABLE_ADMIN"
    | "GLOBAL_ADMIN_SERIALIZATION_UNAVAILABLE"
    | "STALE_USER_VERSION"
    | "IF_MATCH_REQUIRED"
    | "UNSUPPORTED_MEDIA_TYPE"
    | "INTERNAL_SERVER_ERROR"
    | "AUTH_SERVICE_UNAVAILABLE"
    | "UNEXPECTED_HTTP";
  readonly status?: number;
}

export type AssignUserRoleResult =
  | {
      readonly outcome: "CONFIRMED_ASSIGNED";
      readonly status: 201;
      readonly data: UserRoleAssignmentDto;
      readonly etag: string;
    }
  | {
      readonly outcome: "NOT_DISPATCHED" | "REJECTED" | "OUTCOME_UNKNOWN";
      readonly error: UserRoleAssignmentError;
    };

export interface UserRoleAssignmentApi {
  assignUserRole(
    userId: number,
    etag: string,
    roleCode: string,
    branchId: number,
    signal?: AbortSignal,
  ): Promise<AssignUserRoleResult>;
}
