// Called only after the real Approve/Reject operation has committed in the owned trial.
export async function verifyLockedInspectionDefects(
  page,
  api,
  branch,
  inspection,
  state,
) {
  async function request(url, method = "GET", data, headers = {}) {
    return page.evaluate(
      async ({ url, method, data, headers }) => {
        const response = await fetch(url, {
          method,
          credentials: "include",
          cache: "no-store",
          headers: {
            ...(data ? { "Content-Type": "application/json" } : {}),
            ...headers,
          },
          body: data ? JSON.stringify(data) : undefined,
        });
        return {
          status: response.status,
          etag: response.headers.get("ETag"),
          body: await response.json(),
        };
      },
      { url, method, data, headers },
    );
  }
  const before = await request(api + "/execution");
  if (before.status !== 200 || before.body.state !== state)
    throw Error("Actual concluded source required");
  await page
    .getByText(
      `MSG24: Inspection is ${state} and its defects can no longer be changed.`,
      { exact: true },
    )
    .waitFor();
  if (
    !(await page
      .getByRole("button", { name: "Record defect", exact: true })
      .isDisabled())
  )
    throw Error("Concluded defect form enabled");
  const csrf = await request("/api/auth/csrf");
  if (csrf.status !== 200) throw Error("CSRF unavailable");
  const data = {
    itemId: before.body.criteria[0].itemId,
    severityId: before.body.severities[0]?.id ?? 0,
    description: "Synthetic forbidden terminal defect",
    location: "Synthetic room",
  };
  const checks = [];
  for (const method of ["POST", "PUT"]) {
    // Passed inspections may have no defect: terminal policy must reject even before input/reference lookup.
    const url =
      api +
      "/defects" +
      (method === "PUT"
        ? "/" + (before.body.defects[0]?.id ?? Number.MAX_SAFE_INTEGER)
        : "");
    for (const [etag, status] of [
      [before.etag, 409],
      ['"FFFFFFFFFFFFFFFF"', 412],
    ]) {
      const response = await request(url, method, data, {
        "X-CSRF-TOKEN": csrf.body.csrfToken,
        "If-Match": etag,
      });
      if (response.status !== status)
        throw Error(
          "Terminal defect rejection " + method + ": " + response.status,
        );
      if (
        status === 409 &&
        (response.body.title !== "INSPECTION_CONCLUDED" ||
          response.body.messageCode !== "MSG24" ||
          response.body.state !== state ||
          response.body.inspectionId !== inspection ||
          response.body.branchId !== branch ||
          response.body.etag !== before.etag)
      )
        throw Error("Unscoped terminal diagnostic");
      const after = await request(api + "/execution");
      if (JSON.stringify(after) !== JSON.stringify(before))
        throw Error("Concluded inspection changed");
      checks.push({ method, status, snapshotUnchanged: true });
    }
  }
  return { state, terminalDefectMutationsDenied: checks, readOnlyForm: true };
}
