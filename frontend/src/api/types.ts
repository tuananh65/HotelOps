export type Id = string;
export type Json =
  string | number | boolean | null | Json[] | { [key: string]: Json };
export type Query = Record<
  string,
  string | number | boolean | undefined | null
>;
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  code?: string;
  traceId?: string;
  parameters?: Record<string, string | number>;
  errors?: Record<string, string[] | string>;
}
export interface PageDto<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  snapshot: string;
}
export interface ScopeRef {
  id: Id;
  name: string;
}
export interface AccessGrant {
  useCase: string;
  /** null means all server-authorized branches/departments, [] means none. */
  branchIds: Id[] | null;
  departmentIds: Id[] | null;
  ownOnly?: boolean;
}
export interface PrincipalDto {
  id: Id;
  name: string;
  username: string;
  email: string;
  roleNames: string[];
  primaryBranch: ScopeRef;
  primaryDepartment: ScopeRef;
  branches: ScopeRef[];
  departments: ScopeRef[];
  grants: AccessGrant[];
}
export interface SessionDto {
  principal: PrincipalDto;
  expiresAt: string;
  mustChangePassword: boolean;
  accessToken?: string;
  csrfToken?: string;
}
export interface LoginRequest {
  username: string;
  password: string;
}
export interface PasswordRequest {
  currentPassword: string;
  newPassword: string;
}
export interface MaterialLineDto {
  id?: Id;
  itemId: Id;
  unitId: Id;
  quantity: number;
  approvedQuantity?: number;
  issuedQuantity?: number;
  receivedQuantity?: number;
  returnedQuantity?: number;
  countedQuantity?: number;
  systemQuantity?: number;
  conversionFactor?: number;
  damagedQuantity?: number;
  missingQuantity?: number;
  reason?: string;
}
export interface CriterionResultDto {
  text: string;
  resultCode: string;
  defect: string;
}
export interface LineCommandDto {
  lineId: Id;
  approvedQuantity?: number;
  issuedQuantity?: number;
  receivedQuantity?: number;
  acceptedQuantity?: number;
  returnedQuantity?: number;
  countedQuantity?: number;
  damagedQuantity?: number;
  missingQuantity?: number;
  reason?: string;
}
/** Named API attributes, independent of SQL names and of existing form field names. */
export interface EntityAttributes {
  [key: string]: unknown;
  incidentId?: Id;
  scheduleId?: Id;
  roomId?: Id;
  floorId?: Id;
  areaId?: Id;
  roomTypeId?: Id;
  assigneeId?: Id;
  receiverId?: Id;
  requesterId?: Id;
  approverId?: Id;
  warehouseId?: Id;
  destinationWarehouseId?: Id;
  assetId?: Id;
  inspectionId?: Id;
  cleaningTaskId?: Id;
  checklistId?: Id;
  reworkTaskId?: Id;
  dueAt?: string;
  actualStartAt?: string;
  actualEndAt?: string;
  priorityCode?: string;
  severityCode?: string;
  description?: string;
  completedItems?: string[];
  roleNames?: string[];
  lines?: MaterialLineDto[];
  results?: CriterionResultDto[];
  transactionTypeCode?: string;
  conditionCode?: string;
  attachmentIds?: Id[];
}
/** Proposed response projection for every module except the existing incidents reads. */
export interface EntityDto {
  id: Id;
  code?: string;
  name: string;
  statusCode: string;
  branchId: Id | null;
  branchName: string;
  departmentId: Id | null;
  departmentName: string;
  rowVersion?: string;
  createdAt: string;
  allowedActions: string[];
  canEdit: boolean;
  attributes: EntityAttributes;
}
export interface EntityWriteDto {
  code?: string;
  name?: string;
  branchId?: Id;
  departmentId?: Id;
  rowVersion?: string;
  attributes: EntityAttributes;
}
export interface CommandRequest {
  rowVersion?: string;
  reason?: string;
  note?: string;
  assigneeId?: Id;
  receiverId?: Id;
  attachmentIds?: Id[];
  [key: string]: unknown;
}
export interface UserProvisioningDto {
  record: EntityDto;
  temporaryPassword: string;
  validForHours: number;
}
/** Actual camelCase ASP.NET JSON from feature/BE2-incident, commit c6fcb6d. */
export interface IncidentListDto {
  incidentId: string | number;
  incidentNo: string;
  branchId: string | number;
  branchName: string;
  roomId: string | number | null;
  roomNumber: string | null;
  areaId: string | number | null;
  areaName: string | null;
  assetId: string | number | null;
  assetName: string | null;
  incidentTitle: string;
  severityId: string | number | null;
  severityName: string | null;
  priorityId: string | number | null;
  priorityName: string | null;
  status: string;
  reportedBy: string | number;
  reportedByName: string;
  discoveredAt: string;
  reportedAt: string;
  resolvedAt: string | null;
  closedAt: string | null;
}
export interface IncidentDetailDto extends IncidentListDto {
  description: string;
  closureNote: string | null;
  events: Array<{
    eventId: string | number;
    incidentId: string | number;
    eventType: string;
    fromStatus: string | null;
    toStatus: string | null;
    note: string | null;
    actorId: string | number;
    actorName: string;
    occurredAt: string;
  }>;
}
export interface BalanceDto {
  warehouseId: Id;
  itemId: Id;
  quantity: number | null;
  minimum: number | null;
  at?: string;
}
/** Proposed scoped read model for related records and selectable catalogs, NOT table dumps. */
export interface ScreenContextDto {
  snapshot: string;
  collections: Record<string, EntityDto[]>;
  balances?: BalanceDto[];
  metrics?: Record<string, number | null>;
  missingSources?: string[];
}
export interface AttachmentDto {
  id: Id;
  name: string;
  url: string;
  contentType: string;
}
