import { HomeNavigation } from "./HomeNavigation";
import { Link, Navigate, Route, Routes } from "react-router-dom";
import CleaningTaskListScreen from "../screens/housekeeping/CleaningTaskListScreen";
import CreateCleaningTaskScreen from "../screens/housekeeping/CreateCleaningTaskScreen";
import CleaningTaskDetailScreen from "../screens/housekeeping/CleaningTaskDetailScreen";
import CleaningAssignmentScreen from "../screens/housekeeping/CleaningAssignmentScreen";
import ReworkTaskListScreen from "../screens/housekeeping/ReworkTaskListScreen";
import ReworkTaskDetailScreen from "../screens/housekeeping/ReworkTaskDetailScreen";
import RoomTurnoverHistoryScreen from "../screens/housekeeping/RoomTurnoverHistoryScreen";
import RoomStatusBoardScreen from "../screens/rooms/RoomStatusBoardScreen";
import { Dev2Workspace } from "../dev2/Workspace";
import { InventoryRoute } from "../inventory/InventoryRoute";
import { StockBalancePage, StockDocumentPage } from "../inventory/StockBalancePage";
import { MinimumStockPage } from "../inventory/MinimumStockPage";
import { UnitManagementPage } from "../inventory/UnitManagementPage";
import { ItemManagementPage } from "../inventory/ItemManagementPage";
import { WarehouseManagementPage } from "../inventory/WarehouseManagementPage";
import { ReversalPage } from "../inventory/ReversalPage";
import { AdjustmentPage } from "../inventory/AdjustmentPage";
import { StocktakePage } from "../inventory/StocktakePage";
import { TransferWorkspacePage } from "../inventory/TransferWorkspacePage";
import { TransferDetailPage } from "../inventory/TransferDetailPage";
import { ReturnPostPage } from "../inventory/ReturnPostPage";
import { ReceiptWorkspacePage } from "../inventory/ReceiptWorkspacePage";
export function OperationsRoutes() {
  return (
    <>
      <nav aria-label="Hotel operations">
        <HomeNavigation enabled={["/operations/incidents", "/rooms/status", "/cleaning/tasks", "/cleaning/assignment", "/rework", "/rooms/turnover"]} />
      </nav>
      <main>
        <Routes>
          <Route path="/operations/*" element={<Dev2Workspace />} />
          <Route path="/inventory/items" element={<InventoryRoute mode="list" />} />
          <Route path="/inventory/documents/:id/reverse" element={<ReversalPage />} />
          <Route path="/inventory/adjustments" element={<AdjustmentPage />} />
          <Route path="/inventory/stocktakes" element={<StocktakePage />} />
          <Route path="/inventory/stocktakes/:id" element={<StocktakePage />} />
          <Route path="/inventory/transfers" element={<TransferWorkspacePage />} />
          <Route path="/inventory/transfers/:id" element={<TransferDetailPage />} />
          <Route path="/inventory/returns/:id" element={<ReturnPostPage />} />
          <Route path="/inventory/receipts" element={<ReceiptWorkspacePage />} />
          <Route path="/inventory/stock" element={<StockBalancePage />} />
          <Route path="/inventory/minimums" element={<MinimumStockPage />} />
          <Route path="/inventory/units" element={<UnitManagementPage />} />
          <Route path="/inventory/warehouses" element={<WarehouseManagementPage />} />
          <Route path="/inventory/catalog" element={<ItemManagementPage />} />
          <Route path="/inventory/documents/:id" element={<StockDocumentPage />} />
          <Route path="/inventory/items/:id" element={<InventoryRoute mode="detail" />} />
          <Route path="/" element={<Navigate to="/cleaning/tasks" replace />} />
          <Route path="/cleaning/tasks" element={<CleaningTaskListScreen />} />
          <Route
            path="/cleaning/tasks/new"
            element={<CreateCleaningTaskScreen />}
          />
          <Route
            path="/cleaning/tasks/:id"
            element={<CleaningTaskDetailScreen />}
          />
          <Route
            path="/cleaning/assignment"
            element={<CleaningAssignmentScreen />}
          />
          <Route path="/rework" element={<ReworkTaskListScreen />} />
          <Route path="/rework/:id" element={<ReworkTaskDetailScreen />} />
          <Route path="/rooms/status" element={<RoomStatusBoardScreen />} />
          <Route
            path="/rooms/turnover"
            element={<RoomTurnoverHistoryScreen />}
          />
          <Route path="*" element={<p role="alert">Page not found</p>} />
        </Routes>
      </main>
    </>
  );
}
