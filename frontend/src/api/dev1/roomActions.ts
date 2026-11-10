import { cleaningOptions } from "./cleaningCreate";

// Navigation eligibility only. UC028 reauthorizes and validates on entry/submit.
export async function cleaningActionHref(
  branch: string,
  room: string,
  signal?: AbortSignal,
) {
  const options = await cleaningOptions(branch, room, signal);
  signal?.throwIfAborted();
  return `/cleaning/tasks/new?branchId=${options.branchId}&roomId=${options.roomId}`;
}
