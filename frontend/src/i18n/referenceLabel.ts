/** Internal keys remain in requests/links; only labels are exposed to the reader. */
export function referenceLabel(source: unknown, key: string): string {
  if (!source || typeof source !== "object") return "Chưa có tên";
  const row = source as Record<string, unknown>;
  const base = key.replace(/Id$/, "");
  const aliases: Record<string, string[]> = {
    id: ["name", "title", "number", "code", "taskNumber"],
    roomId: ["roomNumber", "roomName"],
    inspectorId: ["inspectorName"],
    taskId: ["taskNumber", "taskNo", "taskTitle", "title"],
    workItemId: ["workItemTitle", "workItemName", "title"],
    assigneeId: ["assigneeName", "employeeName"],
    handlerId: ["handlerName", "currentAssigneeName"],
    uomId: ["unitName", "uomName"],
    itemId: ["itemName"],
    uploadedBy: ["uploadedByName", "uploaderName"],
    actorId: ["actorName"],
    requestId: ["requestNo", "subject"],
    incidentId: ["incidentNo", "incidentTitle"],
  };
  for (const field of [
    ...(aliases[key] ?? []),
    `${base}Name`,
    `${base}Number`,
    `${base}No`,
    `${base}Title`,
  ]) {
    const value = row[field];
    if (typeof value === "string" && value.trim()) return value;
  }
  return "Chưa có tên";
}
