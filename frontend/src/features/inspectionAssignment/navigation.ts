export const inspectionAssignmentPath = "/inspections/assignment";
export interface AssignmentContext { branchId: number; roomId?: number }
export function parseAssignmentContext(search: string): AssignmentContext | null {
  const query = new URLSearchParams(search);
  if ([...query.keys()].some(k => k !== "branchId" && k !== "roomId")) return null;
  function read(key: string, required: boolean): number | undefined | null {
    const values = query.getAll(key);
    if (!values.length) return required ? null : undefined;
    if (values.length !== 1 || !/^[1-9][0-9]*$/.test(values[0])) return null;
    const value = Number(values[0]);
    return Number.isSafeInteger(value) ? value : null;
  }
  const branchId = read("branchId", true), roomId = read("roomId", false);
  return branchId == null || roomId === null ? null : { branchId, roomId };
}
