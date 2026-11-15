export interface InboxRow {
  id: number;
  type: string;
  title: string;
  message: string;
  priority: string;
  createdAt: string;
  readAt: string | null;
}
export interface Inbox {
  items: InboxRow[];
  totalCount: number;
  unreadCount: number;
  page: number;
  pageSize: number;
  snapshot: string;
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
        (r.readAt === null || typeof r.readAt === "string"),
    )
  )
    throw new Error("Invalid inbox response");
  return data;
}
export async function markInboxRead(
  id: number | null,
  signal: AbortSignal,
  stillCurrent: () => boolean,
): Promise<void> {
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
    id === null
      ? "/api/notifications/read-all"
      : `/api/notifications/${id}/read`,
    signal,
    { method: "POST", headers: { "X-CSRF-TOKEN": csrf.csrfToken } },
  );
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
  if (
    typeof data?.path !== "string" ||
    !/^\/(?:cleaning\/reworks|inspection-history)\/([1-9][0-9]*)\?branchId=([1-9][0-9]*)$/.test(
      data.path,
    )
  )
    throw new Error("Invalid notification source");
  const ids =
    /^\/(?:cleaning\/reworks|inspection-history)\/([1-9][0-9]*)\?branchId=([1-9][0-9]*)$/.exec(
      data.path,
    )!;
  if (!ids.slice(1).every((id) => Number.isSafeInteger(Number(id))))
    throw new Error("Invalid notification source");
  return data.path;
}
