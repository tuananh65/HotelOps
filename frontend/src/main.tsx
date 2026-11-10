import React from "react";
import { createRoot } from "react-dom/client";
import { BrowserRouter, Link, Navigate, Route, Routes } from "react-router-dom";
import CleaningTaskListScreen from "./screens/housekeeping/CleaningTaskListScreen";
import CreateCleaningTaskScreen from "./screens/housekeeping/CreateCleaningTaskScreen";
import CleaningTaskDetailScreen from "./screens/housekeeping/CleaningTaskDetailScreen";
import CleaningAssignmentScreen from "./screens/housekeeping/CleaningAssignmentScreen";
import ReworkTaskListScreen from "./screens/housekeeping/ReworkTaskListScreen";
import ReworkTaskDetailScreen from "./screens/housekeeping/ReworkTaskDetailScreen";
import RoomTurnoverHistoryScreen from "./screens/housekeeping/RoomTurnoverHistoryScreen";
import RoomStatusBoardScreen from "./screens/rooms/RoomStatusBoardScreen";
import "./styles.css";
import "./styles/operations.css";
// API cookie/session/CSRF authorization remains on the existing backend boundary.
function App() {
  return <BrowserRouter><nav aria-label="Hotel operations"><Link to="/rooms/status">Room Status</Link> · <Link to="/cleaning/tasks">Cleaning Tasks</Link> · <Link to="/cleaning/assignment">Assignment</Link> · <Link to="/rework">Rework</Link> · <Link to="/rooms/turnover">Turnover History</Link></nav><main><Routes>
    <Route path="/" element={<Navigate to="/cleaning/tasks" replace />} />
    <Route path="/cleaning/tasks" element={<CleaningTaskListScreen />} />
    <Route path="/cleaning/tasks/new" element={<CreateCleaningTaskScreen />} />
    <Route path="/cleaning/tasks/:id" element={<CleaningTaskDetailScreen />} />
    <Route path="/cleaning/assignment" element={<CleaningAssignmentScreen />} />
    <Route path="/rework" element={<ReworkTaskListScreen />} />
    <Route path="/rework/:id" element={<ReworkTaskDetailScreen />} />
    <Route path="/rooms/status" element={<RoomStatusBoardScreen />} />
    <Route path="/rooms/turnover" element={<RoomTurnoverHistoryScreen />} />
    <Route path="*" element={<p role="alert">Page not found</p>} />
  </Routes></main></BrowserRouter>;
}
createRoot(document.getElementById("root")!).render(<React.StrictMode><App /></React.StrictMode>);
