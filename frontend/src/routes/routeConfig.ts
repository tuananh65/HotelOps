import { screens } from "../domain";
import AssetDetailScreen from "../screens/assets/AssetDetailScreen";
import AssetListScreen from "../screens/assets/AssetListScreen";
import AssetMaintenanceHistoryScreen from "../screens/assets/AssetMaintenanceHistoryScreen";
import AuditLogDetailScreen from "../screens/audit/AuditLogDetailScreen";
import AuditLogScreen from "../screens/audit/AuditLogScreen";
import ChangePasswordScreen from "../screens/auth/ChangePasswordScreen";
import ForgotPasswordScreen from "../screens/auth/ForgotPasswordScreen";
import ProfileScreen from "../screens/auth/ProfileScreen";
import SignInScreen from "../screens/auth/SignInScreen";
import DashboardScreen from "../screens/dashboard/DashboardScreen";
import BranchDetailScreen from "../screens/hotel/BranchDetailScreen";
import BranchListScreen from "../screens/hotel/BranchListScreen";
import DepartmentDetailScreen from "../screens/hotel/DepartmentDetailScreen";
import DepartmentListScreen from "../screens/hotel/DepartmentListScreen";
import FloorAreaDetailScreen from "../screens/hotel/FloorAreaDetailScreen";
import FloorAreaListScreen from "../screens/hotel/FloorAreaListScreen";
import RoomDetailScreen from "../screens/hotel/RoomDetailScreen";
import RoomListScreen from "../screens/hotel/RoomListScreen";
import RoomReadinessBoardScreen from "../screens/hotel/RoomReadinessBoardScreen";
import RoomTypeDetailScreen from "../screens/hotel/RoomTypeDetailScreen";
import RoomTypeListScreen from "../screens/hotel/RoomTypeListScreen";
import CleaningAssignmentScreen from "../screens/housekeeping/CleaningAssignmentScreen";
import CleaningTaskDetailScreen from "../screens/housekeeping/CleaningTaskDetailScreen";
import CleaningTaskListScreen from "../screens/housekeeping/CleaningTaskListScreen";
import CreateCleaningTaskScreen from "../screens/housekeeping/CreateCleaningTaskScreen";
import ReworkTaskDetailScreen from "../screens/housekeeping/ReworkTaskDetailScreen";
import ReworkTaskListScreen from "../screens/housekeeping/ReworkTaskListScreen";
import RoomTurnoverHistoryScreen from "../screens/housekeeping/RoomTurnoverHistoryScreen";
import InspectionChecklistDetailScreen from "../screens/inspections/InspectionChecklistDetailScreen";
import InspectionChecklistListScreen from "../screens/inspections/InspectionChecklistListScreen";
import InspectionHistoryScreen from "../screens/inspections/InspectionHistoryScreen";
import InspectionQueueScreen from "../screens/inspections/InspectionQueueScreen";
import InspectionResultScreen from "../screens/inspections/InspectionResultScreen";
import ReinspectionScreen from "../screens/inspections/ReinspectionScreen";
import RequestReworkScreen from "../screens/inspections/RequestReworkScreen";
import RoomInspectionScreen from "../screens/inspections/RoomInspectionScreen";
import InternalRequestDetailScreen from "../screens/internalRequests/InternalRequestDetailScreen";
import InternalRequestListScreen from "../screens/internalRequests/InternalRequestListScreen";
import InventoryDashboardScreen from "../screens/inventory/InventoryDashboardScreen";
import ItemDetailScreen from "../screens/inventory/ItemDetailScreen";
import ItemListScreen from "../screens/inventory/ItemListScreen";
import StockBalanceLedgerScreen from "../screens/inventory/StockBalanceLedgerScreen";
import StockTransactionDetailScreen from "../screens/inventory/StockTransactionDetailScreen";
import StockTransactionListScreen from "../screens/inventory/StockTransactionListScreen";
import StocktakeDetailScreen from "../screens/inventory/StocktakeDetailScreen";
import StocktakeListScreen from "../screens/inventory/StocktakeListScreen";
import UnitofMeasurementScreen from "../screens/inventory/UnitofMeasurementScreen";
import WarehouseDetailScreen from "../screens/inventory/WarehouseDetailScreen";
import WarehouseListScreen from "../screens/inventory/WarehouseListScreen";
import IncidentDetailScreen from "../screens/maintenance/IncidentDetailScreen";
import IncidentListScreen from "../screens/maintenance/IncidentListScreen";
import MaintenanceAssignmentScreen from "../screens/maintenance/MaintenanceAssignmentScreen";
import MaintenanceTaskDetailScreen from "../screens/maintenance/MaintenanceTaskDetailScreen";
import MaintenanceTaskListScreen from "../screens/maintenance/MaintenanceTaskListScreen";
import PreventiveMaintenanceScheduleDetailScreen from "../screens/maintenance/PreventiveMaintenanceScheduleDetailScreen";
import PreventiveMaintenanceScheduleListScreen from "../screens/maintenance/PreventiveMaintenanceScheduleListScreen";
import ReferenceDataScreen from "../screens/maintenance/ReferenceDataScreen";
import ReportIncidentScreen from "../screens/maintenance/ReportIncidentScreen";
import MaterialRequestDetailScreen from "../screens/materialRequests/MaterialRequestDetailScreen";
import MaterialRequestListScreen from "../screens/materialRequests/MaterialRequestListScreen";
import NotificationCenterScreen from "../screens/notifications/NotificationCenterScreen";
import NotificationDetailScreen from "../screens/notifications/NotificationDetailScreen";
import ReplenishmentDetailScreen from "../screens/replenishment/ReplenishmentDetailScreen";
import ReplenishmentListScreen from "../screens/replenishment/ReplenishmentListScreen";
import OperationalReportsScreen from "../screens/reports/OperationalReportsScreen";
import PermissionMatrixScreen from "../screens/roles/PermissionMatrixScreen";
import RoleDetailScreen from "../screens/roles/RoleDetailScreen";
import RoleListScreen from "../screens/roles/RoleListScreen";
import OverdueWorkScreen from "../screens/tasks/OverdueWorkScreen";
import TaskAssignmentScreen from "../screens/tasks/TaskAssignmentScreen";
import TaskDetailScreen from "../screens/tasks/TaskDetailScreen";
import TaskListScreen from "../screens/tasks/TaskListScreen";
import WorkloadTimeLogsScreen from "../screens/tasks/WorkloadTimeLogsScreen";
import UserDetailScreen from "../screens/users/UserDetailScreen";
import UserListScreen from "../screens/users/UserListScreen";
import ShiftDetailScreen from "../screens/workTime/ShiftDetailScreen";
import ShiftHandoverDetailScreen from "../screens/workTime/ShiftHandoverDetailScreen";
import ShiftHandoverListScreen from "../screens/workTime/ShiftHandoverListScreen";
import ShiftManagementScreen from "../screens/workTime/ShiftManagementScreen";
import WorkdayRecordScreen from "../screens/workTime/WorkdayRecordScreen";
const components = [
  SignInScreen,
  ForgotPasswordScreen,
  ChangePasswordScreen,
  ProfileScreen,
  DashboardScreen,
  NotificationCenterScreen,
  NotificationDetailScreen,
  UserListScreen,
  UserDetailScreen,
  RoleListScreen,
  RoleDetailScreen,
  PermissionMatrixScreen,
  BranchListScreen,
  BranchDetailScreen,
  FloorAreaListScreen,
  FloorAreaDetailScreen,
  RoomTypeListScreen,
  RoomTypeDetailScreen,
  RoomListScreen,
  RoomDetailScreen,
  DepartmentListScreen,
  DepartmentDetailScreen,
  RoomReadinessBoardScreen,
  CleaningTaskListScreen,
  CreateCleaningTaskScreen,
  CleaningTaskDetailScreen,
  CleaningAssignmentScreen,
  ReworkTaskListScreen,
  ReworkTaskDetailScreen,
  RoomTurnoverHistoryScreen,
  InspectionChecklistListScreen,
  InspectionChecklistDetailScreen,
  InspectionQueueScreen,
  RoomInspectionScreen,
  InspectionResultScreen,
  RequestReworkScreen,
  ReinspectionScreen,
  InspectionHistoryScreen,
  IncidentListScreen,
  ReportIncidentScreen,
  IncidentDetailScreen,
  MaintenanceAssignmentScreen,
  MaintenanceTaskListScreen,
  MaintenanceTaskDetailScreen,
  PreventiveMaintenanceScheduleListScreen,
  PreventiveMaintenanceScheduleDetailScreen,
  ReferenceDataScreen,
  AssetListScreen,
  AssetDetailScreen,
  AssetMaintenanceHistoryScreen,
  WarehouseListScreen,
  WarehouseDetailScreen,
  InventoryDashboardScreen,
  ItemListScreen,
  ItemDetailScreen,
  UnitofMeasurementScreen,
  StockTransactionListScreen,
  StockTransactionDetailScreen,
  StocktakeListScreen,
  StocktakeDetailScreen,
  StockBalanceLedgerScreen,
  MaterialRequestListScreen,
  MaterialRequestDetailScreen,
  ReplenishmentListScreen,
  ReplenishmentDetailScreen,
  TaskListScreen,
  TaskDetailScreen,
  TaskAssignmentScreen,
  WorkloadTimeLogsScreen,
  OverdueWorkScreen,
  ShiftManagementScreen,
  ShiftDetailScreen,
  WorkdayRecordScreen,
  ShiftHandoverListScreen,
  ShiftHandoverDetailScreen,
  InternalRequestListScreen,
  InternalRequestDetailScreen,
  OperationalReportsScreen,
  AuditLogScreen,
  AuditLogDetailScreen,
];
export const requiredRoutes = screens.map((screen, index) => ({
  ...screen,
  Component: components[index],
}));
