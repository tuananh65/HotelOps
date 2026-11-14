export const stamp = "2026-11-08T01:00:00Z";
export const secret = "PRIVATE-WORKDAY-FIXTURE";
export const shift = (branchId = 1) => ({
  shiftAssignmentId: 10,
  shiftId: 11,
  branchId,
  departmentId: 12,
  shiftDate: "2026-11-08",
  scheduledStartAtUtc: stamp,
  scheduledEndAtUtc: "2026-11-08T09:00:00Z",
});
export const day = (branchId = 1) => ({
  workdayId: 20,
  branchId,
  departmentId: 12,
  workDate: "2026-11-08",
  shiftAssignmentId: 10,
  isUnscheduled: false,
  startedAtUtc: stamp,
  rowVersion: "AAAAAAAAAAE=",
  openInterval: null,
});
export const session = (restricted = false, id = "7") => ({
  user: { id, username: "test-user", name: "Test User" },
  expiresAt: "2026-12-01T12:00:00Z",
  absoluteExpiresAt: "2026-12-01T20:00:00Z",
  mustChangePassword: restricted,
  csrfToken: secret,
});
