export type InitialRoleCode =
  | "HK_STAFF"
  | "HK_SUPERVISOR"
  | "MAINTENANCE_STAFF"
  | "MAINTENANCE_LEAD"
  | "STOREKEEPER"
  | "RECEPTION";

export interface CreateUserRequest {
  employeeCode: string;
  username: string;
  fullName: string;
  email: string;
  phone?: string | null;
  branchId: number;
  departmentId: number;
  roleCode: InitialRoleCode;
}

export interface CreatedEmployeeAccountDto {
  readonly id: number;
  readonly employeeCode: string;
  readonly username: string;
  readonly fullName: string;
  readonly email: string;
  readonly phone: string | null;
  readonly branchId: number;
  readonly departmentId: number;
  readonly roleCode: InitialRoleCode;
  readonly accountStatus: "ACTIVE";
}

export interface CreateUserResponse {
  readonly user: CreatedEmployeeAccountDto;
  /** Success-only secret. Caller owns its lifetime; never persist or log this result. */
  readonly temporaryCredential: {
    readonly password: string;
    readonly expiresAtUtc: string;
  };
}

export type DuplicateField = "employeeCode" | "username" | "email";
export type CreateUserFailureReason =
  | "INVALID_REQUEST"
  | "CANCELLED"
  | "NETWORK"
  | "INVALID_RESPONSE"
  | "VALIDATION_FAILED"
  | "CSRF_VALIDATION_FAILED"
  | "SESSION_INVALID"
  | "FORBIDDEN"
  | "PASSWORD_CHANGE_REQUIRED"
  | "DUPLICATE"
  | "LAST_DURABLE_ADMIN"
  | "GLOBAL_ADMIN_SERIALIZATION_UNAVAILABLE"
  | "UNSUPPORTED_MEDIA_TYPE"
  | "SERVICE_UNAVAILABLE"
  | "UNEXPECTED_HTTP";

export interface CreateUserFailure {
  readonly reason: CreateUserFailureReason;
  readonly status?: number;
  readonly duplicateFields?: readonly DuplicateField[];
}

export type CreateUserResult =
  | {
      readonly outcome: "CONFIRMED_CREATED";
      readonly status: 201;
      readonly location: string;
      readonly data: CreateUserResponse;
    }
  | {
      readonly outcome: "NOT_DISPATCHED" | "REJECTED" | "OUTCOME_UNKNOWN";
      readonly error: CreateUserFailure;
    };

export interface CreateUserApi {
  createUser(
    input: CreateUserRequest,
    signal?: AbortSignal,
  ): Promise<CreateUserResult>;
}
