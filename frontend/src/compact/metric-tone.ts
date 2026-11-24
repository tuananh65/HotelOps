import "./dashboard-colors.css";

const tones: Record<string, string> = {};
for (const [tone, keys] of Object.entries({
  green: [
    "ready",
    "roomsReady",
    "completed",
    "completedToday",
    "accepted",
    "closed",
    "workingStaff",
    "positiveStock",
    "stockedItems",
  ],
  amber: [
    "roomsNeedCleaning",
    "roomsPendingInspection",
    "unassigned",
    "awaitingAcceptance",
    "pendingInspection",
    "pendingReview",
    "rework",
    "maintenanceRework",
    "pendingRequests",
    "underfilledShifts",
    "pendingMaterials",
    "pmDue",
    "lowStock",
    "openStocktakes",
    "openReplenishments",
    "needCleaning",
    "INSPECTION",
    "REWORK",
    "MATERIAL_REQUEST",
  ],
  red: [
    "overdue",
    "overdueTasks",
    "incidents",
    "reportedIncidents",
    "severeIncidents",
    "slaOverdue",
    "pmOverdueWork",
    "affectedAssets",
    "defects",
    "outOfStock",
    "zeroStock",
    "negativeStock",
    "emptyItems",
    "emptyStock",
    "INCIDENT",
    "INCIDENT_unresolved",
    "INCIDENT_overdue",
    "blocked",
  ],
  purple: ["unreadNotifications", "transfers", "INTERNAL_REQUEST"],
})) {
  for (const key of keys) tones[key] = tone;
}

// Tone follows the metric meaning, never its position in a permission-filtered list.
export const metricClass = (key: string) =>
  `panel metric-card metric-${tones[key] || "blue"}`;
