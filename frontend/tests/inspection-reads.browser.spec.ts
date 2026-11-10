import { test, expect, type Page } from "@playwright/test";
async function setup(
  page: Page,
  { severityStatus = 200, status = 200, empty = false } = {},
) {
  const requests: { path: string; method: string; body: any }[] = [];
  const session = {
    user: { id: "7", username: "hk", name: "HK" },
    expiresAt: "2099-01-01T00:00:00Z",
    absoluteExpiresAt: "2099-01-02T00:00:00Z",
    mustChangePassword: false,
    csrfToken: "token",
  };
  const source = {
    id: 9,
    branchId: 2,
    roomId: 5,
    cleaningTaskId: 8,
    inspectorId: 7,
    versionId: 4,
    state: "INSPECTING",
    dueAt: null,
    criteria: [
      {
        itemId: 3,
        criterion: "Desk",
        mandatory: true,
        description: null,
        passCondition: null,
        outcome: "FAIL",
        note: null,
      },
    ],
    defects: [] as any[],
    evidence: [],
  };
  await page.route("**/api/**", async (route) => {
    const req = route.request(),
      u = new URL(req.url()),
      path = u.pathname;
    requests.push({
      path: req.url(),
      method: req.method(),
      body: req.postData() ? req.postDataJSON() : null,
    });
    const send = (v: unknown, code = 200) =>
      route.fulfill({
        status: code,
        contentType: "application/json",
        headers: { ETag: '"00000000000000AF"' },
        body: JSON.stringify(v),
      });
    if (path === "/api/auth/session") return send(session);
    if (path === "/api/auth/csrf")
      return send({ csrfToken: "token", headerName: "X-CSRF-TOKEN" });
    if (status !== 200) return send({ title: "DENIED" }, status);
    if (path.endsWith("/inspection-severities"))
      return severityStatus === 200
        ? send([{ id: 107, code: "MINOR", name: "Small defect" }])
        : send({ title: "UNAVAILABLE" }, severityStatus);
    if (path.endsWith("/inspections/assigned"))
      return send({
        items: empty
          ? []
          : [
              {
                id: 9,
                number: "INS-9",
                branchId: 2,
                roomId: 5,
                roomNumber: "501",
                cleaningTaskId: 8,
                inspectorId: 7,
                inspectorName: "HK",
                versionId: 4,
                state: "INSPECTING",
                dueAt: null,
              },
            ],
        total: empty ? 0 : 21,
        page: Number(u.searchParams.get("page")),
        pageSize: 20,
      });
    if (path.endsWith("/inspection-assignment-tasks"))
      return send({
        items: [
          {
            id: 8,
            number: "TASK-8",
            branchId: 2,
            roomId: 5,
            roomNumber: "501",
            state: "PENDING_INSPECTION",
            pendingReinspection: false,
          },
        ],
        total: 1,
        page: 1,
        pageSize: 20,
      });
    if (path.endsWith("/inspection-options"))
      return send({
        task: { id: 8, branchId: 2, workflowState: "PENDING_INSPECTION" },
        candidates: [{ id: 7, name: "HK", openTasks: 0, openTasksInWindow: 0 }],
        recordedShifts: [],
        total: 1,
        filters: { page: 1, pageSize: 20 },
        checklist: {
          id: 1,
          name: "Assigned checklist",
          versionId: 4,
          versionNumber: 2,
          activeCriteria: 1,
        },
        capturedAt: "2026-11-08T00:00:00Z",
        workflowStateVerified: true,
      });
    if (path.endsWith("/execution")) return send(source);
    if (path.endsWith("/defects")) {
      const b = req.postDataJSON();
      source.defects.push({
        id: 22,
        itemId: b.itemId,
        description: b.description,
        status: "OPEN",
        severityId: b.severityId,
        location: b.location,
        evidenceIds: [],
      });
      return send(source);
    }
    return send({ title: "NOT_FOUND" }, 404);
  });
  return requests;
}
test("directory search/page/reload and direct assigned navigation", async ({
  page,
}) => {
  const calls = await setup(page);
  await page.goto("/inspections");
  await page.getByLabel("List branch ID").fill("2");
  await page.getByLabel("List search").fill("A&B");
  await page
    .getByRole("button", { name: "Load inspections", exact: true })
    .click();
  await expect(page.getByRole("link", { name: "Open INS-9" })).toBeVisible();
  expect(
    calls.find((x) => x.path.includes("/inspections/assigned"))?.path,
  ).toContain("search=A%26B");
  await page.getByRole("button", { name: "Next list page" }).click();
  await expect(page.getByText("21 records · Page 2")).toBeVisible();
  await page.getByRole("button", { name: "Refresh list" }).click();
  await expect(page.getByRole("link", { name: "Open INS-9" })).toBeVisible();
  await page.getByRole("link", { name: "Open INS-9" }).click();
  await expect(page).toHaveURL(/\/inspections\/2\/9\/execution$/);
});
test("queue planning is preview-only and sends no assignment", async ({
  page,
}) => {
  const calls = await setup(page);
  await page.goto("/inspections");
  await page.getByLabel("List branch ID").fill("2");
  await page
    .getByLabel("Inspection list", { exact: true })
    .selectOption("tasks");
  await page
    .getByRole("button", { name: "Load inspections", exact: true })
    .click();
  await page.getByRole("button", { name: "Plan TASK-8" }).click();
  await page.getByLabel("Inspector candidate").selectOption("7");
  await expect(
    page.getByText("Assignment submission unavailable:", { exact: false }),
  ).toBeVisible();
  expect(calls.filter((x) => x.method === "POST")).toHaveLength(0);
});
test("severity selector uses returned IDs and submits defect", async ({
  page,
}) => {
  const calls = await setup(page);
  await page.goto("/inspections/2/9/execution");
  await expect(page.getByLabel("Severity", { exact: true })).toContainText(
    "Small defect (MINOR)",
  );
  await expect(page.getByLabel("Severity reference ID")).toHaveCount(0);
  await page.getByLabel("Failed criterion").selectOption("3");
  await page.getByLabel("Severity", { exact: true }).selectOption("107");
  await page.getByLabel("Description", { exact: true }).fill("Dust");
  await page.getByLabel("Location in room").fill("Desk");
  await page
    .getByRole("button", { name: "Record defect", exact: true })
    .click();
  await expect(page.getByText("#22 Dust")).toBeVisible();
  expect(calls.find((x) => x.path.endsWith("/defects"))?.body.severityId).toBe(
    107,
  );
});
test("severity service failure leaves execution readable and disables defect submission", async ({
  page,
}) => {
  await setup(page, { severityStatus: 503 });
  await page.goto("/inspections/2/9/execution");
  await expect(
    page.getByText("Assigned checklist version ID:", { exact: false }),
  ).toBeVisible();
  await expect(
    page.getByText("Severity lookup unavailable.", { exact: false }),
  ).toBeVisible();
  await expect(
    page.getByRole("button", { name: "Record defect", exact: true }),
  ).toBeDisabled();
});
test("empty assigned list", async ({ page }) => {
  await setup(page, { empty: true });
  await page.goto("/inspections");
  await page.getByLabel("List branch ID").fill("2");
  await page
    .getByRole("button", { name: "Load inspections", exact: true })
    .click();
  await expect(
    page.getByText("No inspections or tasks match this list."),
  ).toBeVisible();
});
for (const status of [401, 403, 503])
  test(`directory handles HTTP ${status}`, async ({ page }) => {
    await setup(page, { status });
    await page.goto("/inspections");
    await page.getByLabel("List branch ID").fill("2");
    await page
      .getByRole("button", { name: "Load inspections", exact: true })
      .click();
    if (status === 401)
      await expect(
        page.getByText("Session needs verification.", { exact: false }),
      ).toBeVisible();
    else
      await expect(page.getByRole("alert")).toContainText(
        status === 403 ? "Access denied" : "503",
      );
  });
