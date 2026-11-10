export const room = {
  id: 11,
  branchId: 7,
  roomNumber: "A101",
  floor: { id: 2, code: "F1", name: "Floor One", number: 1 },
  area: null,
  roomType: { id: 4, code: "STD", name: "Standard" },
  operationalStatusCode: "DIRTY",
  isActive: true,
};
export const board = {
  items: [room],
  page: 1,
  pageSize: 20,
  totalCount: 21,
  statusCounts: [{ operationalStatusCode: "DIRTY", count: 21 }],
};
export const source = {
  type: "CLEANING_TASK",
  id: 12,
  availability: "VERIFIED_SAME_ROOM",
};
export const detail = {
  room,
  description: "Recorded room",
  currentStatusEvidence: {
    availability: "RECORDED",
    reason: "Cleaning required",
    recordedAtUtc: "2026-11-10T00:00:00Z",
    source,
  },
  unreleasedBlocks: [
    {
      id: 13,
      reason: "Repair door",
      blockedAtUtc: "2026-11-09T00:00:00Z",
      expectedReleaseAtUtc: null,
    },
  ],
};
export const history = {
  page: 1,
  pageSize: 20,
  totalCount: 1,
  coverage: "RECORDED_STATUS_EVENTS_ONLY",
  items: [
    {
      id: 14,
      fromStatusCode: "OCCUPIED",
      toStatusCode: "DIRTY",
      changedAtUtc: "2026-11-10T00:00:00Z",
      reason: "Checkout recorded",
      source,
    },
  ],
};
