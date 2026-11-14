export interface UserBranchMembershipDto {
  readonly accessId: number;
  readonly userId: number;
  readonly branchId: number;
  readonly accessLevel: "STANDARD";
  readonly isPrimary: false;
}

export interface UserBranchMembershipError {
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
    | "BRANCH_UNAVAILABLE"
    | "CSRF_VALIDATION_FAILED"
    | "HTTPS_REQUIRED"
    | "SESSION_INVALID"
    | "BRANCH_ASSIGNMENT_NOT_ALLOWED"
    | "PASSWORD_CHANGE_REQUIRED"
    | "USER_NOT_FOUND"
    | "TARGET_NOT_ACTIVE"
    | "MEMBERSHIP_EXISTS"
    | "EXISTING_ROLE_PATH_CONFLICT"
    | "OPERATIONS_MANAGER_SCOPE_CONFLICT"
    | "STALE_USER_VERSION"
    | "IF_MATCH_REQUIRED"
    | "UNSUPPORTED_MEDIA_TYPE"
    | "INTERNAL_SERVER_ERROR"
    | "AUTH_SERVICE_UNAVAILABLE"
    | "UNEXPECTED_HTTP";
  readonly status?: number;
}

export type AddBranchMembershipResult =
  | {
      readonly outcome: "CONFIRMED_ADDED";
      readonly status: 201;
      readonly data: UserBranchMembershipDto;
      readonly etag: string;
    }
  | {
      readonly outcome: "NOT_DISPATCHED" | "REJECTED" | "OUTCOME_UNKNOWN";
      readonly error: UserBranchMembershipError;
    };

export interface UserBranchMembershipApi {
  addBranchMembership(
    userId: number,
    etag: string,
    branchId: number,
    signal?: AbortSignal,
  ): Promise<AddBranchMembershipResult>;
}
