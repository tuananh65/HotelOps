export interface IncidentContext { branchId?: number; roomId?: number }

export function incidentId(value: string): number {
  if (!/^[1-9]\d*$/.test(value)) throw new Error("Invalid incident context identifier.");
  const id = Number(value);
  if (!Number.isSafeInteger(id)) throw new Error("Incident context identifier exceeds browser precision.");
  return id;
}

// Proposed v1 contract: IDs are canonical positive decimals within JS safe range.
export function parseIncidentContext(search: string): IncidentContext {
  const query = new URLSearchParams(search);
  const result: IncidentContext = {};
  for (const key of ["branchId", "roomId"] as const) {
    const values = query.getAll(key);
    if (values.length > 1) throw new Error(`Duplicate ${key}.`);
    if (values.length) result[key] = incidentId(values[0]);
  }
  if (result.roomId && !result.branchId) throw new Error("branchId is required with roomId.");
  return result;
}

export function reportIncidentHref(branchId: string, roomId: string): string {
  incidentId(branchId); incidentId(roomId);
  return `/incidents/new?${new URLSearchParams({ branchId, roomId })}`;
}
