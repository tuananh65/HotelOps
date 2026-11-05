/** Typed domain contracts; schema-driven forms still use RecordData at the boundary. */
export interface Entity {
  id: string;
  code?: string;
  name: string;
  status: string;
  branch: string;
  department: string;
  version: number;
  createdAt: string;
}
export type HotelRole =
  | "Administrator"
  | "Operations Manager"
  | "Housekeeping Supervisor"
  | "Housekeeping Staff"
  | "Maintenance Lead"
  | "Maintenance Staff"
  | "Storekeeper"
  | "Department Head"
  | "Owner";
export interface Permission {
  id: string;
  name: string;
  actors: HotelRole[];
  rules: string[];
}
export interface User extends Entity {
  username: string;
  email: string;
  phone?: string;
  roles: HotelRole[];
  temporary: boolean;
  branches?: string[];
}
export interface Role extends Entity {
  permissions: string[];
  system: boolean;
}
export interface Branch extends Entity {
  address: string;
  phone?: string;
}
export interface Floor extends Entity {
  kind: "Floor";
}
export interface Area extends Entity {
  kind: "Area";
}
export interface RoomType extends Entity {
  minutes: number;
  checklist: string;
}
export interface StatusHistory {
  from: string;
  to: string;
  time: string;
}
export interface Room extends Entity {
  floor: string;
  area?: string;
  roomType: string;
  active: boolean;
  passed: boolean;
  history?: StatusHistory[];
}
export interface Department extends Entity {
  description?: string;
}
export type Priority = "Low" | "Normal" | "High" | "Critical";
export interface Work extends Entity {
  room?: string;
  assignee?: string;
  due: string;
  priority: Priority;
  description: string;
  criteria?: string;
  completedItems?: string[];
}
export interface CleaningTask extends Work {
  cleaningType: string;
  performedBy?: string;
}
export interface ReworkTask extends Work {
  cleaning: string;
  inspection: string;
  defects: string;
  remediation?: string;
}
export interface InspectionCriterion {
  text: string;
  result: "Pass" | "Fail" | "Fixed" | "Not Fixed";
  defect: string;
}
export interface Inspection extends Work {
  cleaning: string;
  checklist: string;
  checklistVersion: number;
  results?: InspectionCriterion[];
  rework?: string;
}
export interface Incident extends Work {
  severity: string;
  asset?: string;
  category?: string;
  detectedAt: string;
}
export interface MaintenanceTask extends Work {
  incident?: string;
  asset?: string;
  schedule?: string;
}
export interface PreventiveMaintenance extends Work {
  asset: string;
  interval: number;
  intervalUnit: string;
  nextDue: string;
}
export interface Asset extends Entity {
  room?: string;
  location?: string;
  category: string;
  condition: string;
  serial: string;
  model: string;
}
export interface Warehouse extends Entity {
  description?: string;
}
export interface InventoryItem extends Entity {
  unit: string;
  category: string;
  minimum: number;
  minimums?: Record<string, number>;
}
export interface MaterialLine {
  item: string;
  quantity: number;
  unit?: string;
  factor?: number;
  approved?: number;
  issued?: number;
  received?: number;
  returned?: number;
  system?: number;
}
export interface StockTransaction extends Entity {
  type: "Receipt" | "Issue" | "Return" | "Transfer";
  warehouse: string;
  source?: string;
  lines: MaterialLine[];
  postedAt?: string;
}
export interface Stocktake extends Entity {
  warehouse: string;
  cutoff: string;
  creator: string;
  lines: MaterialLine[];
}
export interface LinkedWork {
  key: string;
  id: string;
  mandatory?: boolean;
}
export interface MaterialRequest extends Entity {
  warehouse: string;
  creator: string;
  approver?: string;
  due: string;
  lines: MaterialLine[];
  linkedWork?: string;
}
export interface ReplenishmentRequest extends Entity {
  warehouse: string;
  supplier: string;
  due: string;
  lines: MaterialLine[];
}
export interface OperationalTask extends Work {
  creator: string;
  recurring?: boolean;
}
export interface Shift extends Entity {
  date: string;
  start: string;
  end: string;
  assignees: string[];
}
export interface HandoverItem {
  entity: string;
  id: string;
  name: string;
  status: string;
}
export interface ShiftHandover extends Entity {
  sender: string;
  receiver: string;
  description: string;
  snapshot: HandoverItem[];
}
export interface InternalRequest extends Work {
  creator: string;
  receivingDepartment: string;
  requestType: string;
  linkedWork?: LinkedWork[];
}
export interface Notification extends Entity {
  recipient: string;
  type: string;
  priority: Priority;
  description: string;
  relatedKey?: string;
  relatedId?: string;
}
export interface AuditLog extends Entity {
  actor: string;
  actorId: string;
  entity: string;
  recordId: string;
  action: string;
  before: Record<string, unknown> | null;
  after: Record<string, unknown> | null;
}
export interface StockLedgerEntry extends Entity {
  warehouse: string;
  item: string;
  delta: number;
  document: string;
}
export interface Workday extends Entity {
  user: string;
  start: string;
  end?: string;
}
export interface TimeInterval extends Entity {
  user: string;
  entity: string;
  recordId: string;
  start: string;
  end?: string;
}
