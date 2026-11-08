import { test, expect, type Page } from "@playwright/test";
const tag = '"0000000000000001"',
  next = '"0000000000000002"';
async function open(page: Page, capability = "PROVISIONAL_V1") {
  await page.route("**/api/auth/csrf", (r) =>
    r.fulfill({
      json: { headerName: "X-CSRF-TOKEN", csrfToken: "synthetic-test" },
    }),
  );
  await page.route("**/api/branches/7/cleaning-monitor/cleaning/12?**", (r) =>
    r.fulfill({
      json: {
        item: {
          kind: "cleaning",
          id: 12,
          cleaningTaskId: 12,
          taskNumber: "CL-12",
          roomId: 3,
          roomNumber: "101",
          floorId: 2,
          floorName: null,
          recordedStatus: "PENDING",
          workflowState: "ASSIGNED",
          priorityId: null,
          priorityCode: null,
          assigneeId: 5,
          assigneeName: "Trial staff",
          dueAtUtc: null,
          isOverdue: null,
          progressPercent: 0,
          blockerCode: null,
          blockerNote: null,
          description: null,
        },
        assignments: [],
        assignmentCount: 0,
        progress: [],
        progressCount: 0,
        audit: [],
        auditCount: 0,
        timelinePage: 1,
        timelineLimit: 20,
        asOfUtc: "2026-11-07T00:00:00Z",
      },
    }),
  );
  await page.route("**/api/branches/7/cleaning-tasks/12/cancellation", (r) =>
    r.fulfill({
      headers: { ETag: tag },
      json: {
        id: 12,
        branchId: 7,
        sourceETag: tag,
        capability,
        blocker:
          capability === "UNAVAILABLE"
            ? "CANCELLATION_CONTRACT_REQUIRED"
            : null,
      },
    }),
  );
  await page.goto("/cleaning/tasks/12?branchId=7&monitor=1&kind=cleaning");
}
test("confirmation and reason gate cancellation; uncertain retry keeps exact key and payload", async ({
  page,
}) => {
  const requests: { key: string; body: unknown }[] = [];
  await page.route("**/api/branches/7/cleaning-tasks/12/cancel", async (r) => {
    const key = r.request().headers()["idempotency-key"];
    expect(r.request().headers()["if-match"]).toBe(tag);
    requests.push({ key, body: r.request().postDataJSON() });
    if (requests.length === 1)
      return r.fulfill({ status: 503, json: { title: "OUTCOME_UNCONFIRMED" } });
    return r.fulfill({
      headers: { ETag: next },
      json: {
        id: 12,
        branchId: 7,
        sourceETag: next,
        operationId: key,
        state: "CANCELLED",
        readiness: "PENDING_CLEANING",
        readinessEventId: 9,
        contract: "PROVISIONAL_NOT_ACCEPTED",
      },
    });
  });
  await open(page);
  const panel = page.getByRole("region", { name: "Hủy công việc vệ sinh" });
  const button = panel.getByRole("button", {
    name: "Hủy công việc",
    exact: true,
  });
  await expect(button).toBeDisabled();
  await panel.getByLabel("Lý do hủy", { exact: true }).fill("No longer needed");
  await expect(button).toBeDisabled();
  await panel.getByRole("checkbox").check();
  await button.click();
  await expect(panel.getByLabel("Lý do hủy", { exact: true })).toBeDisabled();
  await panel.getByRole("button", { name: "Kiểm tra lại yêu cầu hủy" }).click();
  await expect(panel.getByRole("status")).toContainText("Đã hủy công việc");
  expect(requests).toHaveLength(2);
  expect(requests[0]).toEqual(requests[1]);
});
test("unavailable server capability hides mutation", async ({ page }) => {
  await open(page, "UNAVAILABLE");
  await expect(
    page.getByText("Chưa đủ điều kiện hủy công việc."),
  ).toBeVisible();
  await expect(
    page.getByRole("button", { name: "Hủy công việc", exact: true }),
  ).toHaveCount(0);
});
test("stale ETag stops retry until metadata is refreshed", async ({ page }) => {
  await page.route("**/api/branches/7/cleaning-tasks/12/cancel", (r) =>
    r.fulfill({ status: 412, json: { title: "STALE" } }),
  );
  await open(page);
  const panel = page.getByRole("region", { name: "Hủy công việc vệ sinh" });
  await panel.getByLabel("Lý do hủy", { exact: true }).fill("No longer needed");
  await panel.getByRole("checkbox").check();
  await panel
    .getByRole("button", { name: "Hủy công việc", exact: true })
    .click();
  await expect(panel.getByRole("status")).toContainText("dữ liệu đã thay đổi");
  await expect(panel.getByRole("button")).toHaveCount(0);
});
