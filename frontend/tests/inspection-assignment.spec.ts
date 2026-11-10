import { test, expect } from "@playwright/test";
const options = {
  taskId: 2,
  roomId: 3,
  taskRevision: '"0000000000000001"',
  cycleRevision: '"0000000000000002"',
  checklistVersionId: 4,
  inspectors: [{ userId: 5, fullName: "Inspector" }],
  responsibleSupervisors: [{ userId: 6, fullName: "Supervisor" }],
  productionMutationAvailable: false,
};
for (const failure of [503, 412, "unknown"] as const)
  test(
    "assignment Review/Confirm " + failure + " stays closed without retry",
    async ({ page }) => {
      let posts = 0;
      await page.route(
        (u) => u.pathname.startsWith("/api/"),
        async (route) => {
          const req = route.request(),
            url = req.url();
          if (url.endsWith("/csrf")) {
            await route.fulfill({
              json: { headerName: "X-CSRF-TOKEN", csrfToken: "test" },
            });
            return;
          }
          if (req.method() === "POST") {
            posts++;
            expect(req.headers()["if-match"]).toBe(options.taskRevision);
            expect(req.postDataJSON().responsibleSupervisorId).toBe(6);
            if (failure === "unknown") await route.abort("failed");
            else
              await route.fulfill({
                status: failure,
                json: { title: "UC041_ASSIGNMENT_NOTIFICATION_NOT_ACCEPTED" },
              });
            return;
          }
          await route.fulfill({
            json: url.endsWith("/2")
              ? options
              : {
                  pending: [{ taskId: 2, roomId: 3 }],
                  assigned: [
                    {
                      inspectionId: 7,
                      roomId: 3,
                      checklistVersionId: 4,
                      inspectionRevision: options.taskRevision,
                    },
                  ],
                },
          });
        },
      );
      await page.goto("/tests/fixtures/inspection-assignment.html");
      await page.getByRole("button", { name: "Room #3 · Task #2" }).click();
      await page.getByRole("combobox", {name:"Inspector", exact:true}).selectOption("5");
      await page.getByRole("combobox", {name:"Responsible supervisor", exact:true}).selectOption("6");
      await page.getByLabel("Due time").fill("2030-01-01T12:00");
      await page
        .getByLabel("Reason", { exact: true })
        .fill("Explicit assignment");
      await page.getByRole("button", { name: "Review assignment" }).click();
      const confirm = page.getByRole("button", {
        name: "Confirm assignment",
        exact: true,
      });
      await expect(confirm).toBeDisabled();
      await page.getByLabel("I confirm this assignment").check();
      await confirm.click();
      await expect(page.getByRole("alert")).toContainText(
        failure === 503
          ? "mandatory notification"
          : failure === 412
            ? "source changed"
            : "unconfirmed",
      );
      await expect(confirm).toBeDisabled();
      expect(posts).toBe(1);
      await page.getByRole("button", { name: "Open Inspection #7" }).click();
      await expect(page.locator("#opened")).toHaveText("7");
    },
  );
for (const status of [401, 403])
  test("assignment queue authorization " + status, async ({ page }) => {
    await page.route(
      (u) => u.pathname.startsWith("/api/"),
      (r) => r.fulfill({ status, json: { title: "DENIED" } }),
    );
    await page.goto("/tests/fixtures/inspection-assignment.html");
    await expect(page.getByRole("alert")).toContainText(
      status === 401 ? "session expired" : "cannot assign",
    );
    await expect(
      page.getByRole("button", { name: "Confirm assignment", exact: true }),
    ).toHaveCount(0);
  });
