import { test, expect } from "@playwright/test";
const initial = {
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
      fixed: false,
      recordedBy: 12,
      recordedAt: "2026-11-10T00:00:00Z",
    },
  ],
  candidateStorageAvailable: true,
  lifecycleConclusionAvailable: false,
};
// Intercepted HTTP tests the real React + typed client. Not live SQL/HTTPS acceptance.
for (const failure of [null, 412, 403, "unknown"] as const)
  test(
    "Review -> Confirm proposal " + (failure ?? "persists and reloads"),
    async ({ page }) => {
      let source: any = structuredClone(initial),
        proposal: any = null,
        posts = 0;
      await page.route(
        (url) => url.pathname.startsWith("/api/"),
        async (route) => {
          const request = route.request(),
            url = request.url();
          if (url.endsWith("/csrf")) {
            await route.fulfill({
              json: { headerName: "X-CSRF-TOKEN", csrfToken: "test" },
            });
            return;
          }
          if (url.includes("/conclusion-proposal")) {
            if (request.method() === "GET") {
              await route.fulfill({ json: { proposal } });
              return;
            }
            posts++;
            const command = request.postDataJSON();
            expect(command.confirmed).toBe(true);
            expect(command.selectedDefectIds).toEqual([9]);
            expect(request.headers()["if-match"]).toBe(
              initial.inspectionRevision,
            );
            if (failure === "unknown") {
              await route.abort("failed");
              return;
            }
            if (failure) {
              await route.fulfill({
                status: failure,
                json: { title: "DENIED" },
              });
              return;
            }
            source = {
              ...source,
              inspectionRevision: '"0000000000000004"',
              bindingRevision: '"0000000000000005"',
            };
            proposal = {
              operationId: command.operationId,
              inspectionId: 7,
              reworkId: 8,
              actorUserId: 12,
              originalChecklistVersionId: 3,
              assignedChecklistVersionId: 4,
              proposedReworkState: "FAILED",
              inspectionRevision: source.inspectionRevision,
              bindingRevision: source.bindingRevision,
              reworkRevision: source.reworkRevision,
              proposalRevision: '"0000000000000006"',
              notFixedDefectIds: [9],
              createdAt: "2026-11-10T00:00:00Z",
              replayed: false,
              lifecycleApplied: false,
              nextRoundCreationAvailable: false,
            };
            await route.fulfill({
              json: proposal,
              headers: { ETag: source.inspectionRevision },
            });
            return;
          }
          await route.fulfill({
            json: url.includes("?")
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
        },
      );
      await page.goto("/tests/fixtures/reinspection-workflow.html");
      await page
        .getByRole("button", { name: "Inspection #7 · Rework #8" })
        .click();
      await page
        .getByRole("button", {
          name: "Review conclusion proposal",
          exact: true,
        })
        .click();
      await expect(
        page.getByText("Some selected defects are Not Fixed.", {
          exact: false,
        }),
      ).toBeVisible();
      const confirm = page.getByRole("button", {
        name: "Save conclusion proposal",
        exact: true,
      });
      await expect(confirm).toBeDisabled();
      await page.getByLabel("I confirm the saved defect decisions").check();
      await confirm.click();
      if (failure) {
        await expect(
          page.getByRole("button", { name: "Save results", exact: true }),
        ).toBeDisabled();
        await expect(confirm).toBeDisabled();
        await expect(page.getByRole("alert").last()).toContainText(
          failure === 403
            ? "cannot perform"
            : failure === 412
              ? "source changed"
              : "could not be confirmed",
        );
      } else {
        await expect(
          page.getByText("Saved proposal: Rework FAILED.", { exact: false }),
        ).toBeVisible();
        await expect(
          page.getByRole("button", { name: "Save results", exact: true }),
        ).toBeDisabled();
        await page
          .getByRole("button", { name: "Reload persisted results" })
          .click();
        await expect(
          page.getByText("Saved proposal: Rework FAILED.", { exact: false }),
        ).toBeVisible();
      }
      expect(posts).toBe(1);
      await expect(
        page.getByRole("button", { name: "Conclude unavailable" }),
      ).toBeDisabled();
    },
  );
