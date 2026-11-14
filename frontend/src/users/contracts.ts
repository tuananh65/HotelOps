/** Backend Int64 JSON numbers. The decoder accepts positive safe integers only. */
export type UserId = number;

export interface UserDirectoryQuery {
  readonly search?: string;
  readonly branchId?: number;
  readonly departmentId?: number;
  readonly roleId?: number;
  readonly accountStatus?: "ACTIVE" | "INACTIVE";
  readonly page?: number;
  readonly pageSize?: number;
}

export interface UserAccessReviewQuery {
  readonly feature?: string;
  readonly permissionCode?: string;
}

export interface UserDirectoryBranchDto {
  readonly id: number;
  readonly code: string;
  readonly name: string;
}

export interface UserDirectoryDepartmentDto {
  readonly id: number;
  readonly code: string;
  readonly name: string;
}

export interface UserDirectoryRoleDto {
  readonly id: number;
  readonly code: string;
  readonly name: string;
  readonly branch: UserDirectoryBranchDto | null;
}

export interface UserDirectoryItemDto {
  readonly id: UserId;
  readonly employeeCode: string;
  readonly username: string;
  readonly fullName: string;
  readonly email: string;
  readonly primaryBranch: UserDirectoryBranchDto | null;
  readonly department: UserDirectoryDepartmentDto | null;
  readonly roles: readonly UserDirectoryRoleDto[];
  readonly accountStatus: string;
}

export interface UserDirectoryResponse {
  readonly items: readonly UserDirectoryItemDto[];
  readonly page: number;
  readonly pageSize: number;
  readonly totalCount: number;
}

export interface UserAccountDetailDto {
  readonly id: UserId;
  readonly employeeCode: string;
  readonly username: string;
  readonly fullName: string;
  readonly email: string;
  readonly phone: string | null;
  readonly accountStatus: string;
  readonly primaryBranch: UserDirectoryBranchDto | null;
  readonly department: UserDirectoryDepartmentDto | null;
}

export interface AccessReviewRoleDto {
  readonly assignmentId: number;
  readonly roleId: number;
  readonly code: string;
  readonly name: string;
  readonly branchId: number | null;
  readonly assignmentActive: boolean;
  readonly roleActive: boolean;
  readonly assignedAtUtc: string;
  readonly expiresAtUtc: string | null;
}

export interface AccessReviewMembershipDto {
  readonly branchId: number;
  readonly code: string;
  readonly name: string;
  readonly accessLevel: string;
  readonly branchActive: boolean;
  readonly grantedAtUtc: string;
  readonly revokedAtUtc: string | null;
}

export interface AccessReviewGrantDto {
  readonly permissionCode: string;
  readonly permissionName: string;
  readonly feature: string | null;
  readonly configuredScope: string;
  readonly assignmentId: number;
  readonly roleCode: string;
  readonly branchId: number | null;
  readonly isEffective: boolean;
}

export interface AccessReviewPermissionSourceDto {
  readonly assignmentId: number;
  readonly roleCode: string;
  readonly scope: string;
  readonly branchId: number | null;
  readonly requiresResourceOwnership: boolean;
}

export interface AccessReviewPermissionDto {
  readonly code: string;
  readonly name: string;
  readonly feature: string | null;
  readonly sources: readonly AccessReviewPermissionSourceDto[];
}

export interface AccessReviewScopeDto {
  readonly isGlobal: boolean;
  readonly branchIds: readonly number[];
}

/** A server-evaluated report, not a frontend permission or CanExecute decision. */
export interface UserAccessReviewDto {
  readonly account: UserAccountDetailDto;
  readonly evaluatedAtUtc: string;
  readonly reviewerScope: AccessReviewScopeDto;
  readonly assignedRoles: readonly AccessReviewRoleDto[];
  readonly branchMemberships: readonly AccessReviewMembershipDto[];
  readonly configuredGrants: readonly AccessReviewGrantDto[];
  readonly effectivePermissions: readonly AccessReviewPermissionDto[];
}

export type UsersProblemCode =
  | "VALIDATION_FAILED"
  | "AUTHENTICATION_REQUIRED"
  | "SESSION_INVALID"
  | "FORBIDDEN"
  | "PERMISSION_DENIED"
  | "PASSWORD_CHANGE_REQUIRED"
  | "AUTH_SERVICE_UNAVAILABLE"
  | "INTERNAL_SERVER_ERROR";

/** No raw ProblemDetails, submitted values, exceptions, tokens or response body. */
export type UsersApiError =
  | {
      readonly kind: "http";
      readonly status: number;
      readonly code?: UsersProblemCode;
    }
  | {
      readonly kind:
        "invalid-request" | "invalid-response" | "network" | "cancelled";
    };

export type UsersApiResult<T> =
  | { readonly ok: true; readonly status: 200; readonly data: T }
  | { readonly ok: false; readonly error: UsersApiError };

export interface UsersApi {
  listUsers(
    query?: UserDirectoryQuery,
    signal?: AbortSignal,
  ): Promise<UsersApiResult<UserDirectoryResponse>>;
  getUser(
    id: UserId,
    signal?: AbortSignal,
  ): Promise<UsersApiResult<UserAccountDetailDto>>;
  getEffectiveAccess(
    id: UserId,
    query?: UserAccessReviewQuery,
    signal?: AbortSignal,
  ): Promise<UsersApiResult<UserAccessReviewDto>>;
}
