/** Return URL V1: route admission only; server resource authorization remains mandatory. */
export function validateReturnUrl(value: unknown): string | null {
  if (
    typeof value !== "string" ||
    value.length > 2048 ||
    !value.startsWith("/") ||
    value.startsWith("//")
  )
    return null;
  let decoded: string;
  try {
    decoded = decodeURIComponent(value);
  } catch {
    return null;
  }
  // Reject recursive encodings, separators and dot segments rather than normalize redirects.
  if (
    /[\\\u0000-\u0020\u007f#]/u.test(value) ||
    /[\\\u0000-\u0020\u007f#%]/u.test(decoded) ||
    decoded.startsWith("//")
  )
    return null;
  const path = decoded.split("?")[0];
  if (
    path.split("/").some((part) => part === "." || part === "..") ||
    path.includes(":")
  )
    return null;
  const query = new URLSearchParams(
    value.includes("?") ? value.slice(value.indexOf("?") + 1) : "",
  );
  for (const key of query.keys())
    if (
      /(?:password|credential|token|secret|authorization|returnto|redirect)/i.test(
        key,
      )
    )
      return null;
  for (const key of ["branchId", "roomId"]) {
    const values = query.getAll(key);
    if (
      values.length > 1 ||
      values.some(
        (v) => !/^[1-9][0-9]*$/.test(v) || !Number.isSafeInteger(Number(v)),
      )
    )
      return null;
  }
  return value;
}
/** Recheck against the destination registry after explicit session revalidation, never a permission grant. */
export function approvedReturnDestination(
  value: unknown,
  revalidated: boolean,
): string | null {
  const safe = validateReturnUrl(value);
  if (!safe || !revalidated) return null;
  const path = safe.split("?")[0];
  const inspection =
    /^\/(?:inspections|reinspections)\/([1-9][0-9]*)\/([1-9][0-9]*)\/execution$/.exec(
      path,
    );
  if (
    inspection &&
    inspection.slice(1).every((id) => Number.isSafeInteger(Number(id)))
  )
    return safe;
  const assignedRework = /^\/cleaning\/reworks\/([1-9][0-9]*)$/.exec(path);
  const history = /^\/inspection-history\/([1-9][0-9]*)$/.exec(path);
  if (history && Number.isSafeInteger(Number(history[1]))) {
    const query = new URLSearchParams(safe.split("?")[1] || "");
    if (
      query.has("branchId") &&
      [...query.keys()].every((key) => key === "branchId")
    )
      return safe;
  }
  const original =
    /^\/reinspections\/([1-9][0-9]*)\/([1-9][0-9]*)\/original$/.exec(path);
  if (
    original &&
    original.slice(1).every((id) => Number.isSafeInteger(Number(id)))
  )
    return safe;
  if (
    assignedRework &&
    Number.isSafeInteger(Number(assignedRework[1])) &&
    new URLSearchParams(safe.split("?")[1] || "").has("branchId")
  )
    return safe;
  if (path === "/inspections/assignment") {
    const query = new URLSearchParams(safe.split("?")[1] || "");
    if (
      query.has("branchId") &&
      [...query.keys()].every((key) => key === "branchId" || key === "roomId")
    )
      return safe;
  }
  const reinspection =
    /^\/inspections\/(?:reworks|cleaning-tasks)\/([1-9][0-9]*)\/assign$/.exec(
      path,
    );
  if (reinspection && Number.isSafeInteger(Number(reinspection[1]))) {
    const query = new URLSearchParams(safe.split("?")[1] || "");
    if (
      query.has("branchId") &&
      [...query.keys()].every((key) => key === "branchId")
    )
      return safe;
  }
  // Other candidate routes remain excluded; their flags are OFF.
  return [
    "/rooms/status",
    "/rooms/turnover",
    "/cleaning/tasks",
    "/cleaning/assignment",
    "/rework",
    "/profile",
    "/operational-tasks",
    "/notifications",
  ].includes(path) || /^\/(?:cleaning\/tasks|rework)\/[1-9][0-9]*$/.test(path)
    ? safe
    : null;
}

export function returnUrlFromSearch(search: string): string | null {
  const values = new URLSearchParams(search).getAll("returnTo");
  return values.length === 1 ? validateReturnUrl(values[0]) : null;
}
export function loginDestination(pathname: string, search: string): string {
  const safe = approvedReturnDestination(pathname + search, true);
  return safe ? "/login?" + new URLSearchParams({ returnTo: safe }) : "/login";
}
