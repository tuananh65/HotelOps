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
  // UC041/049 and unintegrated features remain excluded; flags are OFF.
  return [
    "/rooms/status",
    "/rooms/turnover",
    "/cleaning/tasks",
    "/cleaning/assignment",
    "/rework",
    "/profile",
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
