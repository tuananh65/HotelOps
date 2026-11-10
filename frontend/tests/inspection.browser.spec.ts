import { test, expect, type Page } from "@playwright/test";
const tag = '"00000000000000AF"';
const criterion = () => ({
  itemId: 3,
  criterion: "Desk",
  mandatory: true,
  description: "Clean desk",
  passCondition: "No dust",
  outcome: null as string | null,
  note: null,
});
const source = () => ({
  id: 9,
  branchId: 2,
  roomId: 5,
  cleaningTaskId: 8,
  inspectorId: 7,
  versionId: 4,
  state: "ASSIGNED",
  dueAt: null,
  criteria: [criterion()],
  defects: [] as any[],
  evidence: [] as any[],
});
const item = () => ({
  id: 9,
  number: "INSP-9",
  roomId: 5,
  roomNumber: "501",
  branchId: 2,
  cleaningTaskId: 8,
  versionId: 4,
  versionNumber: 2,
  inspectorId: 7,
  inspectorName: "Inspector",
  status: "IN_PROGRESS",
  result: null,
  at: "2026-11-08T01:00:00Z",
  summary: null,
  reworkCount: 0,
});
const session = {
  user: { id: "7", username: "inspector", name: "Inspector" },
  expiresAt: "2099-01-01T00:00:00Z",
  absoluteExpiresAt: "2099-01-02T00:00:00Z",
  mustChangePassword: false,
  csrfToken: "token",
};
async function setup(
  page: Page,
  options: {
    stale?: boolean;
    status?: number;
    empty?: boolean;
    phase?: string;
  } = {},
) {
  let current = source();
  if (options.phase) current.state = options.phase;
  const writes: string[] = [];
  const queries: string[] = [];
  await page.route("**/api/**", async (route) => {
    const req = route.request(),
      url = new URL(req.url()),
      path = url.pathname;
    const send = (body: unknown, status = 200) =>
      route.fulfill({
        status,
        contentType: "application/json",
        headers: { ETag: tag },
        body: JSON.stringify(body),
      });
    if (path === "/api/auth/session") return send(session);
    if (path === "/api/auth/csrf")
      return send({ csrfToken: "token", headerName: "X-CSRF-TOKEN" });
    if (options.status)
      return send(
        { title: options.status === 403 ? "FORBIDDEN" : "SESSION_INVALID" },
        options.status,
      );
    if (path.endsWith("/inspection-severities"))
      return send([{ id: 12, code: "MINOR", name: "Minor" }]);
    if (path.endsWith("/execution")) return send(current);
    if (path.includes("/inspections/") && req.method() !== "GET") {
      writes.push(path);
      expect(req.headers()["if-match"]).toBe(tag);
      expect(req.headers()["x-csrf-token"]).toBe("token");
      if (options.stale) return send({ title: "STALE" }, 412);
      const body = req.postDataJSON();
      if (path.endsWith("/start")) current.state = "INSPECTING";
      if (path.endsWith("/results"))
        for (const r of body.results) {
          current.criteria[0].outcome = r.outcome;
        }
      if (path.endsWith("/defects"))
        current.defects.push({
          id: 22,
          itemId: 3,
          description: body.description,
          status: "OPEN",
          severityId: body.severityId,
          location: body.location,
          evidenceIds: [],
        });
      if (path.endsWith("/finish")) current.state = "PENDING_CONCLUSION";
      return send(current);
    }
    if (path === "/api/inspection-history") {
      queries.push(url.search);
      return send({
        items: options.empty ? [] : [item()],
        total: options.empty ? 0 : 21,
        filters: {
          page: Number(url.searchParams.get("page")),
          pageSize: Number(url.searchParams.get("pageSize")),
        },
      });
    }
    if (path === "/api/inspection-history/9")
      return send({
        inspection: item(),
        criteria: current.criteria,
        defects: current.defects,
        housekeepers: [],
        reworks: [],
        cycleInspections: [],
        evidence: [],
      });
    return send({ title: "NOT_FOUND" }, 404);
  });
  return { writes, queries };
}
test("Start, PASS, save, finish and Pending Conclusion use existing APIs", async ({
  page,
}) => {
  const { writes } = await setup(page);
  await page.goto("/inspections/2/9/execution");
  await expect(
    page.getByText("Assigned checklist version ID: 4", { exact: false }),
  ).toBeVisible();
  await page
    .getByLabel("Operation reason")
    .fill("Assigned inspection performed");
  await page
    .getByRole("button", { name: "Start Inspection", exact: true })
    .click();
  await expect(
    page.getByRole("button", { name: "Save Draft", exact: true }),
  ).toBeVisible();
  await expect(
    page.getByRole("button", { name: "Finish Inspection", exact: true }),
  ).toBeDisabled();
  await page.getByLabel("Outcome: Desk").selectOption("PASS");
  await page.getByRole("button", { name: "Save Draft", exact: true }).click();
  await expect(
    page.getByText("results saved.", { exact: false }),
  ).toBeVisible();
  await page
    .getByRole("button", { name: "Finish Inspection", exact: true })
    .click();
  await expect(
    page.getByText(
      "Pending Conclusion. No approval/rejection action is provided here.",
    ),
  ).toBeVisible();
  expect(writes.length).toBe(3);
});
test("FAIL saved then defect unlocks finish", async ({ page }) => {
  await setup(page);
  await page.goto("/inspections/2/9/execution");
  await page.getByLabel("Operation reason").fill("Quality inspection");
  await page
    .getByRole("button", { name: "Start Inspection", exact: true })
    .click();
  await page.getByLabel("Outcome: Desk").selectOption("FAIL");
  await page.getByRole("button", { name: "Save Draft", exact: true }).click();
  await expect(page.getByText("Defect required: Desk")).toBeVisible();
  await page.getByLabel("Failed criterion").selectOption("3");
  await page.getByLabel("Severity", { exact: true }).selectOption("12");
  await page.getByLabel("Description", { exact: true }).fill("Dust found");
  await page.getByLabel("Location in room").fill("Desk");
  await page
    .getByRole("button", { name: "Record defect", exact: true })
    .click();
  await expect(page.getByText("#22 Dust found")).toBeVisible();
  await expect(
    page.getByRole("button", { name: "Finish Inspection", exact: true }),
  ).toBeEnabled();
});
test("stale write locks controls until explicit reload with no replay", async ({
  page,
}) => {
  const { writes } = await setup(page, { stale: true });
  await page.goto("/inspections/2/9/execution");
  await page.getByLabel("Operation reason").fill("Start");
  await page
    .getByRole("button", { name: "Start Inspection", exact: true })
    .click();
  await expect(
    page.getByText("Further writes are blocked.", { exact: false }),
  ).toBeVisible();
  await expect(
    page.getByRole("button", { name: "Start Inspection", exact: true }),
  ).toBeDisabled();
  expect(writes.length).toBe(1);
  await page
    .getByRole("button", { name: "Reload inspection", exact: true })
    .click();
  await expect(
    page.getByRole("button", { name: "Start Inspection", exact: true }),
  ).toBeEnabled();
});
test("history search/filter/pagination and detail preserve assigned historical version", async ({
  page,
}) => {
  const { queries } = await setup(page);
  await page.goto("/inspections/history");
  await page.getByLabel("Search", { exact: true }).fill("Desk");
  await page.getByLabel("branchId", { exact: true }).fill("2");
  await page.getByLabel("Result", { exact: true }).selectOption("FAIL");
  await page
    .getByRole("button", { name: "Search history", exact: true })
    .click();
  await expect.poll(() => queries.length).toBe(2);
  expect(queries[1]).toContain("search=Desk");
  expect(queries[1]).toContain("branchId=2");
  await page.getByRole("button", { name: "Next page" }).click();
  await expect(page.getByText("Page 2", { exact: false })).toBeVisible();
  await page.getByRole("link", { name: "INSP-9", exact: true }).click();
  await expect(
    page.getByText("Room 501 · Branch #2 · Version 2 (#4)"),
  ).toBeVisible();
  await expect(page.getByRole("button", { name: "Save Draft" })).toHaveCount(0);
});
test("empty history state and refresh", async ({ page }) => {
  const { queries } = await setup(page, { empty: true });
  await page.goto("/inspections/history");
  await expect(
    page.getByText("No inspections match these filters."),
  ).toBeVisible();
  await page.getByRole("button", { name: "Refresh history" }).click();
  await expect.poll(() => queries.length).toBe(2);
});
test("403 hides inspection data and explains authorization", async ({
  page,
}) => {
  await setup(page, { status: 403 });
  await page.goto("/inspections/2/9/execution");
  await expect(page.getByRole("alert").first()).toContainText("Access denied");
  await expect(page.getByLabel("Operation reason")).toHaveCount(0);
});
test("401 enters session recovery without replay", async ({ page }) => {
  await setup(page, { status: 401 });
  await page.goto("/inspections/2/9/execution");
  await expect(
    page.getByText("Session needs verification.", { exact: false }),
  ).toBeVisible();
  await expect(page.getByLabel("Operation reason")).toHaveCount(0);
});
test("503 displays backend failure with reload", async ({ page }) => {
  await setup(page, { status: 503 });
  await page.goto("/inspections/history");
  await expect(page.getByRole("alert")).toContainText("503");
  await expect(
    page.getByRole("button", { name: "Refresh history" }),
  ).toBeEnabled();
});
test("entry navigates only valid explicit identities", async ({ page }) => {
  await setup(page);
  await page.goto("/inspections");
  await page.getByLabel("Branch ID", { exact: true }).fill("2");
  await page.getByLabel("Inspection ID", { exact: true }).fill("9");
  await page.getByRole("button", { name: "Open assigned inspection" }).click();
  await expect(page).toHaveURL(/\/inspections\/2\/9\/execution$/);
});

test("concluded inspection is read-only for results and defects", async ({
  page,
}) => {
  await setup(page, { phase: "PASSED" });
  await page.goto("/inspections/2/9/execution");
  await expect(page.getByText("State:", { exact: false })).toContainText(
    "PASSED",
  );
  await expect(
    page.getByRole("button", { name: "Save Draft", exact: true }),
  ).toHaveCount(0);
  await expect(
    page.getByRole("button", { name: "Record defect", exact: true }),
  ).toHaveCount(0);
  await expect(page.getByLabel("Outcome: Desk")).toBeDisabled();
});
test("Pending Conclusion permits defects without reopening results", async ({
  page,
}) => {
  await setup(page, { phase: "PENDING_CONCLUSION" });
  await page.goto("/inspections/2/9/execution");
  await expect(
    page.getByText(
      "Pending Conclusion. No approval/rejection action is provided here.",
    ),
  ).toBeVisible();
  await expect(
    page.getByRole("button", { name: "Save Draft", exact: true }),
  ).toHaveCount(0);
  await expect(page.getByLabel("Outcome: Desk")).toBeDisabled();
  await expect(
    page.getByText("Record Inspection Defect", { exact: true }),
  ).toBeVisible();
});
