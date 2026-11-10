import { test, expect } from "@playwright/test";
const source = {
  inspectionId: 7,
  reworkId: 8,
  originalInspectionId: 2,
  originalChecklistVersionId: 3,
  assignedChecklistVersionId: 4,
  inspectionRevision: '"0000000000000001"',
  reworkRevision: '"0000000000000002"',
  bindingRevision: '"0000000000000003"',
  criteria: [
    {
      itemId: 11,
      criterion: "Floor clean",
      passCondition: "Dry",
      isRequired: true,
      isActive: true,
    },
  ],
  defects: [
    {
      defectId: 9,
      description: "Wet floor",
      originalCriterion: "Floor clean",
      remediationNote: "Repaired",
      fixed: null,
      recordedBy: null,
      recordedAt: null,
    },
  ],
  candidateStorageAvailable: true,
  lifecycleConclusionAvailable: false,
};
// HTTP interception: verifies real React + typed client, never LIVE_E2E/SQL credit.
test("open exact source, choose Not Fixed, save, reload and review; Conclude stays disabled", async ({
  page,
}) => {
  let stored = structuredClone(source),
    puts = 0;
  await page.route((url) => url.pathname.startsWith("/api/"), async (route) => {
    const request = route.request(),
      url = request.url();
    let body: any = stored;
    if (url.endsWith("/csrf"))
      body = { headerName: "X-CSRF-TOKEN", csrfToken: "test" };
    else if (request.method() === "PUT") {
      puts++;
      expect(request.headers()["if-match"]).toBe(stored.inspectionRevision);
      const command = request.postDataJSON();
      expect(command.decisions).toEqual([{ defectId: 9, fixed: false }]);
      stored = {
        ...stored,
        inspectionRevision: '"0000000000000004"',
        bindingRevision: '"0000000000000005"',
        defects: [
          {
            ...stored.defects[0],
            fixed: false,
            recordedBy: 12,
            recordedAt: "2026-11-10T00:00:00Z",
          },
        ],
      };
      body = stored;
    } else if (!url.includes("?"))
      body = [
        {
          inspectionId: 7,
          reworkId: 8,
          originalInspectionId: 2,
          roomId: 6,
          assignedChecklistVersionId: 4,
        },
      ];
    await route.fulfill({ json: body });
  });
  await page.goto("/tests/fixtures/reinspection-workflow.html");
  await page.getByRole("button", { name: "Inspection #7 · Rework #8" }).click();
  await expect(page.getByText("Wet floor", { exact: true })).toBeVisible();
  await page.getByText("Assigned checklist", { exact: true }).click();
  await expect(page.getByText("Dry", { exact: true })).toBeVisible();
  await expect(
    page.getByRole("button", { name: "Save results", exact: true }),
  ).toBeDisabled();
  await page.getByLabel("Decision").selectOption("not-fixed");
  await page.getByRole("button", { name: "Save results", exact: true }).click();
  await expect(
    page.getByText("Results saved.", { exact: false }),
  ).toBeVisible();
  await page.getByRole("button", { name: "Reload persisted results" }).click();
  await expect(page.getByLabel("Decision")).toHaveValue("not-fixed");
  expect(puts).toBe(1);
  await expect(
    page.getByRole("button", { name: "Conclude unavailable" }),
  ).toBeDisabled();
});
test("stale save disables another mutation until reload", async ({ page }) => {
  let puts = 0;
  await page.route((url) => url.pathname.startsWith("/api/"), async (route) => {
    const url = route.request().url();
    if (route.request().method() === "PUT") {
      puts++;
      await route.fulfill({ status: 412, json: { title: "STALE" } });
      return;
    }
    await route.fulfill({
      json: url.endsWith("/csrf")
        ? { headerName: "X-CSRF-TOKEN", csrfToken: "test" }
        : url.includes("?")
          ? source
          : [
              {
                inspectionId: 7,
                reworkId: 8,
                originalInspectionId: 2,
                roomId: 6,
                assignedChecklistVersionId: 4,
              },
            ],
    });
  });
  await page.goto("/tests/fixtures/reinspection-workflow.html");
  await page.getByRole("button", { name: "Inspection #7 · Rework #8" }).click();
  await page.getByLabel("Decision").selectOption("fixed");
  await page.getByRole("button", { name: "Save results", exact: true }).click();
  await expect(page.getByRole("alert")).toContainText("source changed");
  await expect(
    page.getByRole("button", { name: "Save results", exact: true }),
  ).toBeDisabled();
  expect(puts).toBe(1);
  await page.getByRole("button", { name: "Reload persisted results" }).click();
  await expect(page.getByLabel("Decision")).toBeEnabled();
});
for (const status of [401, 403])
  test(`list ${status} displays authorization failure`, async ({ page }) => {
    await page.route((url) => url.pathname.startsWith("/api/"), (route) =>
      route.fulfill({ status, json: { title: "DENIED" } }),
    );
    await page.goto("/tests/fixtures/reinspection-workflow.html");
    await expect(page.getByRole("alert")).toContainText(
      status === 401 ? "session expired" : "cannot perform",
    );
    await expect(
      page.getByRole("button", { name: "Save results", exact: true }),
    ).toHaveCount(0);
  });
test("empty list has no mutation controls", async ({ page }) => {
  await page.route((url) => url.pathname.startsWith("/api/"), (route) => route.fulfill({ json: [] }));
  await page.goto("/tests/fixtures/reinspection-workflow.html");
  await expect(
    page.getByText("No assigned reinspections available."),
  ).toBeVisible();
  await expect(
    page.getByRole("button", { name: "Save results", exact: true }),
  ).toHaveCount(0);
});
