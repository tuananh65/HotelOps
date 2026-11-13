/** Proposed V1 only; deliberately not consumed by SignInScreen or production navigation. */
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
  ].includes(path)
    ? safe
    : null;
}
