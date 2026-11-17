export interface InboxRow {
  id: number;
  type: string;
  title: string;
  message: string;
  priority: string;
  createdAt: string;
  readAt: string | null;
  requiresAcknowledgment?: boolean;
  acknowledgedAt?: string | null;
}
export interface Inbox {
  items: InboxRow[];
  totalCount: number;
  unreadCount: number;
  page: number;
  pageSize: number;
  snapshot: string;
  acknowledgmentAvailable?: boolean;
}
export class InboxFailure extends Error {
  constructor(readonly status: number) {
    super("Notification request failed");
  }
}
const request = async (
  url: string,
  signal: AbortSignal,
  init: RequestInit = {},
) => {
  const response = await fetch(url, {
    credentials: "same-origin",
    cache: "no-store",
    redirect: "error",
    ...init,
    signal,
  });
  if (!response.ok) throw new InboxFailure(response.status);
  return response;
};
export async function readInbox(
  query: string,
  signal: AbortSignal,
): Promise<Inbox> {
  const data = await (
    await request(`/api/notifications?${query}`, signal)
  ).json();
  if (
    !data ||
    !Array.isArray(data.items) ||
    ![data.totalCount, data.unreadCount, data.page, data.pageSize].every(
      (n) => Number.isSafeInteger(n) && n >= 0,
    ) ||
    typeof data.snapshot !== "string" ||
    !data.items.every(
      (r: InboxRow) =>
        r &&
        Number.isSafeInteger(r.id) &&
        r.id > 0 &&
        [r.type, r.title, r.message, r.priority, r.createdAt].every(
          (x) => typeof x === "string",
        ) &&
        (r.readAt === null || typeof r.readAt === "string") &&
        (data.acknowledgmentAvailable !== true ||
          (typeof r.requiresAcknowledgment === "boolean" &&
            (r.acknowledgedAt === null ||
              typeof r.acknowledgedAt === "string"))),
    )
  )
    throw new Error("Invalid inbox response");
  return data;
}
export async function markInboxRead(
  id: number | number[] | null,
  signal: AbortSignal,
  stillCurrent: () => boolean,
  action: "read" | "acknowledge" = "read",
): Promise<void> {
  const ids = Array.isArray(id) ? [...new Set(id)] : null;
  if (action === "acknowledge" && !ids)
    throw new Error("Invalid acknowledgment selection");
  if (
    (Array.isArray(id) &&
      (id.length < 1 ||
        id.length > 100 ||
        id.some((n) => !Number.isSafeInteger(n) || n <= 0))) ||
    (typeof id === "number" && (!Number.isSafeInteger(id) || id <= 0))
  )
    throw new Error("Invalid notification selection");
  const csrf = await (await request("/api/auth/csrf", signal)).json();
  if (
    !csrf ||
    typeof csrf.csrfToken !== "string" ||
    !csrf.csrfToken ||
    csrf.headerName !== "X-CSRF-TOKEN"
  )
    throw new Error("Invalid CSRF response");
  if (signal.aborted || !stillCurrent()) throw new Error("Session changed");
  await request(
    action === "acknowledge"
      ? "/api/notifications/acknowledge"
      : id === null
        ? "/api/notifications/read-all"
        : ids
          ? "/api/notifications/read-selected"
          : `/api/notifications/${id}/read`,
    signal,
    {
      method: "POST",
      headers: {
        "X-CSRF-TOKEN": csrf.csrfToken,
        ...(ids ? { "Content-Type": "application/json" } : {}),
      },
      ...(ids ? { body: JSON.stringify({ ids }) } : {}),
    },
  );
}

// Resolving a source is a read. Marking the recipient's notification is a
// separate CSRF-protected action; never write for a denied or stale source.
export async function openInboxSource(
  id: number,
  signal: AbortSignal,
  stillCurrent: () => boolean,
): Promise<string> {
  const check = () => {
    if (signal.aborted || !stillCurrent()) throw new Error("Session changed");
  };
  check();
  const path = await resolveInboxSource(id, signal);
  check();
  await markInboxRead(id, signal, stillCurrent);
  check();
  return path;
}

export async function resolveInboxSource(
  id: number,
  signal: AbortSignal,
): Promise<string> {
  if (!Number.isSafeInteger(id) || id <= 0)
    throw new Error("Invalid notification identifier");
  const data = await (
    await request(`/api/notifications/${id}/source`, signal)
  ).json();
  if (typeof data?.path !== "string")
    throw new Error("Invalid notification source");
  const cancellation = /^\/notifications\/([1-9][0-9]*)\/(?:cleaning-cancellation|asset-condition)$/.exec(data.path);
  if (cancellation) {
    if (Number(cancellation[1]) !== id) throw new Error("Invalid notification source");
    return data.path;
  }
  const ids =
    /^\/workday\/handovers\/([1-9][0-9]*)$/.exec(data.path) ??
    /^\/time-corrections\?branchId=([1-9][0-9]*)&correctionId=([1-9][0-9]*)$/.exec(data.path) ??
    /^\/maintenance-assignment-receipts\/([1-9][0-9]*)$/.exec(data.path) ??
    /^\/operations\/(?:material-requests|replenishments)\/([1-9][0-9]*)$/.exec(data.path) ??
    /^\/operations\/incidents\/([1-9][0-9]*)$/.exec(data.path) ??
    /^\/workday\?branchId=([1-9][0-9]*)&shiftAssignmentId=([1-9][0-9]*)$/.exec(data.path) ??
    /^\/operations\/maintenance\?branchId=([1-9][0-9]*)&runId=([1-9][0-9]*)$/.exec(data.path) ??
    /^\/rooms\/([1-9][0-9]*)\?branchId=([1-9][0-9]*)$/.exec(data.path) ??
    /^\/operations\/maintenance\/([1-9][0-9]*)\?action=A004A-036$/.exec(data.path) ??
    /^\/operations\/incidents\/([1-9][0-9]*)\?action=A004A-023$/.exec(data.path) ??
    /^\/cleaning\/tasks\/([1-9][0-9]*)\?branchId=([1-9][0-9]*)&monitor=1$/.exec(
      data.path,
    ) ??
    /^\/cleaning\/tasks\/([1-9][0-9]*)\?branchId=([1-9][0-9]*)&assignmentId=([1-9][0-9]*)$/.exec(data.path) ??
    /^\/(?:cleaning\/reworks|inspection-history)\/([1-9][0-9]*)\?branchId=([1-9][0-9]*)$/.exec(
      data.path,
    ) ??
    /^\/(?:reinspections|inspections)\/([1-9][0-9]*)\/([1-9][0-9]*)\/execution$/.exec(
      data.path,
    ) ??
    /^\/inspections\/reworks\/([1-9][0-9]*)\/assign\?branchId=([1-9][0-9]*)$/.exec(
      data.path,
    ) ??
    /^\/internal-requests\?requestId=([1-9][0-9]*)$/.exec(data.path) ??
    /^\/internal-requests\/([1-9][0-9]*)\/handovers\/([1-9][0-9]*)$/.exec(data.path) ??
    /^\/workday\?branchId=([1-9][0-9]*)&unscheduledId=([1-9][0-9]*)$/.exec(data.path) ??
    /^\/operational-tasks\?branchId=([1-9][0-9]*)&taskId=([1-9][0-9]*)$/.exec(
      data.path,
    );
  if (!ids || !ids.slice(1).every((id) => Number.isSafeInteger(Number(id))))
    throw new Error("Invalid notification source");
  return data.path;
}
