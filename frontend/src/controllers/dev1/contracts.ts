/** Wire DTOs from DEV1 cf8fb020. Dates are ISO strings, IDs must be safe JS integers.
 * No EntityDto/allowedActions/permissions are fabricated from these responses. */
export interface DeactivateRoomTypeRequest {
  confirmation: boolean;
  reason: string;
}

export interface UpdateRoomInformationRequest {
  floorId: number;
  roomTypeId: number;
  roomNumber: string;
  reason: string;
  areaId?: number | null;
  notes?: string | null;
}

export interface ActiveRoomReferenceDto {
  id: number;
  roomNumber: string;
}

export interface CreateDepartmentRequest {
  code: string;
  name: string;
  description?: string | null;
  managerUserId?: number | null;
}

export interface UpdateDepartmentRequest {
  name: string;
  description?: string | null;
  managerUserId?: number | null;
}

export interface DepartmentInformationDto {
  id: number;
  branchId: number;
  code: string;
  name: string;
  description: string | null;
  managerUserId: number | null;
  isActive: boolean;
}

export interface UpdateFloorRequest {
  code: string;
  name: string;
  reason: string;
  description?: string | null;
}

export interface UpdateAreaRequest {
  code: string;
  name: string;
  reason: string;
  floorId?: number | null;
  description?: string | null;
}

export interface EditableStructureDto {
  kind: string;
  id: number;
  branchId: number;
  code: string;
  name: string;
  description: string | null;
  floorId: number | null;
  floorNumber: number | null;
  isActive: boolean;
}

export interface ChecklistCriterionRequest {
  text: string;
  group: string | null;
  mandatory: boolean | null;
  passCondition?: string | null;
  description?: string | null;
}

export interface CreateChecklistRequest {
  code: string;
  name: string;
  roomTypeIds: number[];
  criteria: ChecklistCriterionRequest[];
  description?: string | null;
}

export interface UpdateChecklistRequest {
  criteria: ChecklistCriterionRequest[];
  reason: string;
}

export interface ChecklistTransitionRequest {
  reason: string;
  confirmation?: boolean;
}

export interface ChecklistCriterionDto {
  id: number;
  text: string;
  group: string | null;
  mandatory: boolean;
  passCondition: string | null;
  description: string | null;
  order: number;
}

export interface ChecklistVersionDto {
  id: number;
  number: number;
  status: string;
  effectiveAt: string | null;
  publishedBy: number | null;
  usedByInspection: boolean;
  criteria: ChecklistCriterionDto[];
}

export interface ChecklistDetailsDto {
  id: number;
  branchId: number;
  code: string;
  name: string;
  description: string | null;
  active: boolean;
  roomTypeIds: number[];
  versions: ChecklistVersionDto[];
}

export interface StructureBranchDto {
  id: number;
  code: string;
  name: string;
  isActive: boolean;
}

export interface StructureFloorDto {
  id: number;
  branchId: number;
  code: string;
  name: string;
  floorNumber: number | null;
  description: string | null;
  isActive: boolean;
}

export interface StructureAreaDto {
  id: number;
  branchId: number;
  floorId: number | null;
  code: string;
  name: string;
  description: string | null;
  isActive: boolean;
}

export interface StructureRoomDto {
  id: number;
  branchId: number;
  floorId: number;
  areaId: number | null;
  roomNumber: string;
  isActive: boolean;
}

export interface StructureDepartmentDto {
  id: number;
  branchId: number;
  code: string;
  name: string;
  isActive: boolean;
}

export interface StructurePageDto<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface BranchStructureDto {
  branch: StructureBranchDto;
  visibility: string;
  nodeType: string;
  collection: string | null;
  availableCollections: string[];
  isActive: boolean | null;
  selectedFloor: StructureFloorDto | null;
  selectedArea: StructureAreaDto | null;
  selectedRoom: StructureRoomDto | null;
  selectedDepartment: StructureDepartmentDto | null;
  floors: StructurePageDto<StructureFloorDto> | null;
  areas: StructurePageDto<StructureAreaDto> | null;
  rooms: StructurePageDto<StructureRoomDto> | null;
  departments: StructurePageDto<StructureDepartmentDto> | null;
}

export interface CreateRoomTypeRequest {
  branchId: number;
  code: string;
  name: string;
  standardCleaningMinutes: number;
  defaultChecklistId: number;
  description?: string | null;
  standardCapacity?: number | null;
}

export interface CreatedRoomTypeDto {
  id: number;
  branchId: number;
  code: string;
  name: string;
  description: string | null;
  standardCleaningMinutes: number;
  defaultChecklistId: number;
  standardCapacity: number | null;
  isActive: boolean;
}

export interface UpdateRoomTypeRequest {
  name: string;
  standardCleaningMinutes: number;
  defaultChecklistId: number;
  description?: string | null;
  code?: string | null;
}

export interface RoomTypeDetailsDto {
  id: number;
  branchId: number | null;
  code: string;
  name: string;
  description: string | null;
  standardCleaningMinutes: number | null;
  defaultChecklistId: number | null;
  standardCapacity: number | null;
  isActive: boolean;
}

export interface RoomTypeListItem {
  details: RoomTypeDetailsDto;
  activeRooms: number;
  canUpdate: boolean;
}

export interface RoomTypePage {
  items: RoomTypeListItem[];
  total: number;
  page: number;
  pageSize: number;
  branchId: number | null;
  isActive: boolean | null;
  search: string | null;
  sort: string;
  descending: boolean;
}

export interface CreateRoomRequest {
  branchId: number;
  floorId: number;
  roomTypeId: number;
  roomNumber: string;
  areaId?: number | null;
  notes?: string | null;
}

export interface CreatedRoomDto {
  id: number;
  branchId: number;
  floorId: number;
  areaId: number | null;
  roomTypeId: number;
  roomNumber: string;
  notes: string | null;
  operationalStatus: string;
  isActive: boolean;
}

export interface CreateBranchRequest {
  code: string;
  name: string;
  address: string;
  phone: string | null;
  email: string | null;
}

export interface UpdateBranchRequest {
  name: string;
  address: string;
  phone: string | null;
  email: string | null;
  reason: string;
}

export interface EditableBranchDto {
  id: number;
  code: string;
  name: string;
  address: string | null;
  phone: string | null;
  email: string | null;
  timezone: string | null;
  isActive: boolean;
}

export interface OperationalHistoryQuery {
  branchId?: number | null;
  roomId?: number | null;
  from?: string | null;
  to?: string | null;
  result?: string | null;
  inspectorId?: number | null;
  housekeeperId?: number | null;
  search?: string | null;
  page?: number;
  pageSize?: number;
}

export interface OperationalHistoryPage<T> {
  items: T[];
  total: number;
  filters: OperationalHistoryQuery;
}

export interface HistoryAssignmentDto {
  id: number;
  userId: number;
  name: string;
  status: string;
  assignedAt: string;
  endedAt: string | null;
  reason: string | null;
}

export interface HistoryProgressDto {
  id: number;
  percent: number;
  status: string | null;
  blockerTypeId: number | null;
  note: string | null;
  actorId: number;
  at: string;
}

export interface HistoryInspectionDto {
  id: number;
  number: string;
  roomId: number;
  roomNumber: string;
  branchId: number;
  cleaningTaskId: number | null;
  versionId: number;
  versionNumber: number;
  inspectorId: number;
  inspectorName: string;
  status: string;
  result: string | null;
  at: string;
  summary: string | null;
  reworkCount: number;
}

export interface HistoryCriterionDto {
  id: number | null;
  itemId: number;
  criterion: string;
  description: string | null;
  mandatory: boolean;
  passCondition: string | null;
  outcome: string | null;
  note: string | null;
  at: string | null;
  group: string | null;
  evidenceIds: number[] | null;
}

export interface HistoryDefectDto {
  id: number;
  itemId: number | null;
  description: string;
  status: string;
  severityId: number | null;
  correctiveAction: string | null;
  resolvedAt: string | null;
  resolutionNote: string | null;
  location: string | null;
  evidenceIds: number[] | null;
}

export interface HistoryReworkDto {
  id: number;
  cleaningTaskId: number;
  inspectionId: number;
  assigneeId: number | null;
  reason: string;
  status: string;
  requestedAt: string;
  completedAt: string | null;
  note: string | null;
}

export interface HistoryEvidenceDto {
  id: number;
  entityType: string;
  entityId: number;
  name: string;
  mimeType: string | null;
  bytes: number | null;
  uploadedAt: string;
  downloadPath: string;
}

export interface HistoryInspectionDetailDto {
  inspection: HistoryInspectionDto;
  housekeepers: HistoryAssignmentDto[];
  criteria: HistoryCriterionDto[];
  defects: HistoryDefectDto[];
  reworks: HistoryReworkDto[];
  cycleInspections: HistoryInspectionDto[];
  evidence: HistoryEvidenceDto[];
}

export interface HistoryCycleDto {
  id: number;
  number: string;
  roomId: number;
  roomNumber: string;
  branchId: number;
  cleaningType: string;
  status: string;
  at: string;
  start: string | null;
  end: string | null;
  result: string | null;
  reworkCount: number;
  cancellationReason: string | null;
  assignees: HistoryAssignmentDto[];
}

export interface HistoryCycleDetailDto {
  cycle: HistoryCycleDto;
  progress: HistoryProgressDto[];
  inspections: HistoryInspectionDetailDto[];
  reworks: HistoryReworkDto[];
  evidence: HistoryEvidenceDto[];
}

export interface CreateFloorRequest {
  code: string;
  name: string;
  floorNumber?: number | null;
  description?: string | null;
}

export interface CreateAreaRequest {
  code: string;
  name: string;
  floorId?: number | null;
  description?: string | null;
}

export interface CreatedFloorDto {
  id: number;
  branchId: number;
  code: string;
  name: string;
  floorNumber: number | null;
  description: string | null;
  isActive: boolean;
}

export interface CreatedAreaDto {
  id: number;
  branchId: number;
  code: string;
  name: string;
  floorId: number | null;
  description: string | null;
  isActive: boolean;
}

export interface DeactivateStructureRequest {
  reason: string;
  confirmation: boolean;
}

export interface StructureDependencyDto {
  kind: string;
  count: number;
  ids: number[];
}

export interface EvidenceUpload {
  name: string;
  mimeType: string;
  base64: string;
}

export interface InspectionCriterionInput {
  itemId: number;
  outcome: string;
  note: string | null;
  photos: EvidenceUpload[] | null;
}

export interface SaveInspectionRequest {
  results: InspectionCriterionInput[];
  reason: string;
}

export interface InspectionActionRequest {
  reason: string;
}

export interface RecordInspectionDefectRequest {
  itemId: number;
  severityId: number;
  description: string;
  location: string;
  photos?: EvidenceUpload[] | null;
}

export interface AssignCleaningRequest {
  assigneeId: number;
  dueAt: string;
  reason: string;
  adoptUnassignedLegacy?: boolean;
}

export interface AssignInspectionRequest {
  inspectorId: number;
  dueAt: string;
  reason: string;
}

export interface RequestReworkRequest {
  assigneeId: number;
  dueAt: string;
  defectIds: number[];
  reason: string;
}

export interface InspectionExecutionDto {
  id: number;
  branchId: number;
  roomId: number;
  cleaningTaskId: number;
  inspectorId: number;
  versionId: number;
  state: string;
  dueAt: string | null;
  criteria: HistoryCriterionDto[];
  defects: HistoryDefectDto[];
  evidence: HistoryEvidenceDto[];
}

export interface HousekeepingWriteDto {
  id: number;
  state: string;
  notification: string;
}

export interface HousekeepingPlanningQuery {
  search?: string | null;
  from?: string | null;
  to?: string | null;
  page?: number;
  pageSize?: number;
}

export interface PlanningTaskDto {
  id: number;
  number: string;
  roomId: number;
  roomNumber: string;
  branchId: number;
  recordedStatus: string;
  dueAt: string | null;
  workflowState: string | null;
}

export interface PlanningShiftDto {
  id: number;
  userId: number;
  from: string;
  to: string;
  recordedShiftStatus: string;
  recordedAssignmentStatus: string;
}

export interface PlanningStaffDto {
  id: number;
  name: string;
  departmentId: number | null;
  openTasks: number;
  openTasksInWindow: number;
}

export interface PlanningChecklistDto {
  id: number;
  name: string;
  versionId: number;
  versionNumber: number;
  activeCriteria: number;
}

export interface HousekeepingPlanningDto {
  task: PlanningTaskDto;
  candidates: PlanningStaffDto[];
  recordedShifts: PlanningShiftDto[];
  total: number;
  filters: HousekeepingPlanningQuery;
  checklist: PlanningChecklistDto | null;
  capturedAt: string;
  workflowStateVerified: boolean;
}

export interface ProgressDefinition {
  label: string;
  required: boolean;
}

export interface InitializeProgressRequest {
  items: ProgressDefinition[] | null;
  reason: string;
}

export interface ProgressItemInput {
  id: number;
  done: boolean | null;
  outcome: string | null;
  note: string | null;
}

export interface SaveProgressRequest {
  percent: number;
  items: ProgressItemInput[];
  note: string | null;
  photos?: EvidenceUpload[] | null;
}

export interface ProgressItemDto {
  id: number;
  label: string;
  required: boolean;
  order: number;
  done: boolean | null;
  outcome: string | null;
  note: string | null;
}

export interface WorkProgressDto {
  id: number;
  branchId: number;
  kind: string;
  state: string;
  percent: number;
  items: ProgressItemDto[];
  timeline: HistoryProgressDto[];
  evidence: HistoryEvidenceDto[];
}

export interface RoomDirectoryFloorDto {
  id: number;
  code: string;
  name: string;
  number: number | null;
}

export interface RoomDirectoryAreaDto {
  id: number;
  code: string;
  name: string;
}

export interface RoomDirectoryTypeDto {
  id: number;
  code: string;
  name: string;
}

export interface RoomDirectoryItemDto {
  id: number;
  branchId: number;
  roomNumber: string;
  floor: RoomDirectoryFloorDto;
  area: RoomDirectoryAreaDto | null;
  roomType: RoomDirectoryTypeDto;
  operationalStatusCode: string;
  isActive: boolean;
}

export interface RoomDirectoryResponse {
  items: RoomDirectoryItemDto[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface RoomStatusSourceDto {
  type: string | null;
  id: number | null;
  availability: string;
}

export interface RoomStatusEventDto {
  id: number;
  fromStatusCode: string | null;
  toStatusCode: string;
  changedAtUtc: string;
  reason: string | null;
  source: RoomStatusSourceDto;
}

export interface RoomCurrentStatusEvidenceDto {
  availability: string;
  reason: string | null;
  recordedAtUtc: string | null;
  source: RoomStatusSourceDto | null;
}

export interface RoomDirectoryDetailDto {
  room: RoomDirectoryItemDto;
  description: string | null;
  currentStatusEvidence: RoomCurrentStatusEvidenceDto;
  historyUrl: string | null;
}

export interface RoomDirectoryHistoryDto {
  items: RoomStatusEventDto[];
  page: number;
  pageSize: number;
  totalCount: number;
  coverage: string;
}

export interface BranchStructureQuery {
  nodeType?: "branch" | "floor" | "area" | "room" | "department";
  nodeId?: number;
  collection?: "floors" | "areas" | "rooms" | "departments";
  isActive?: boolean;
  page?: number;
  pageSize?: number;
}
export interface RoomTypeQuery {
  branchId?: number;
  isActive?: boolean;
  search?: string;
  page?: number;
  pageSize?: number;
  sort?: "code" | "name" | "status";
  descending?: boolean;
}
export type RoomDirectoryQuery = {
  floorId?: number;
  areaId?: number;
  roomTypeId?: number;
  search?: string;
  operationalStatusCode?:
    | "AVAILABLE"
    | "OCCUPIED"
    | "DIRTY"
    | "CLEANING"
    | "INSPECTION"
    | "READY"
    | "OUT_OF_SERVICE";
  isActive?: boolean;
  page?: number;
  pageSize?: number;
  sort?: "floor" | "roomNumber" | "roomType" | "status";
  descending?: boolean;
} & (
  | { branchId: number; allBranches?: false }
  | { allBranches: true; branchId?: never }
);
export interface PageQuery {
  page?: number;
  pageSize?: number;
}
export interface InitializedProgress {
  id: number;
  kind: "template" | "cleaning" | "task";
  initialized: boolean;
}
