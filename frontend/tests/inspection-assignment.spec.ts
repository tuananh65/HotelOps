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
            url = new URL(req.url()).pathname;
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

for (const nextStatus of [200, 404, 412])
  test(`changing task clears previous assignment context (${nextStatus})`, async ({ page }) => {
    let posts = 0;
    await page.route((u) => u.pathname.startsWith('/api/'), async (route) => {
      if (route.request().method() === 'POST') {
        posts++;
        await route.fulfill({ status: 503, json: {} });
        return;
      }
      const path = new URL(route.request().url()).pathname;
      if (path.endsWith('/2')) {
        await route.fulfill({ json: options });
      } else if (path.endsWith('/8')) {
        await route.fulfill({ status: nextStatus, json: nextStatus === 200
          ? { ...options, taskId: 8, roomId: 9 }
          : { title: 'SOURCE_CHANGED' } });
      } else {
        await route.fulfill({ json: { pending: [{ taskId: 2, roomId: 3 }, { taskId: 8, roomId: 9 }], assigned: [] } });
      }
    });
    await page.goto('/tests/fixtures/inspection-assignment.html');
    await page.getByRole('button', { name: /Task #2$/ }).click();
    await page.getByRole('combobox', { name: 'Inspector', exact: true }).selectOption('5');
    await page.getByRole('combobox', { name: 'Responsible supervisor', exact: true }).selectOption('6');
    await page.getByLabel('Due time').fill('2030-01-01T12:00');
    await page.getByLabel('Reason', { exact: true }).fill('Previous task reason');
    await page.getByRole('button', { name: 'Review assignment' }).click();
    await page.getByLabel('I confirm this assignment').check();
    await page.getByRole('button', { name: /Task #8$/ }).click();
    await expect(page.getByRole('button', { name: 'Confirm assignment', exact: true })).toHaveCount(0);
    if (nextStatus === 200) {
      await expect(page.getByLabel('Due time')).toHaveValue('');
      await expect(page.getByLabel('Reason', { exact: true })).toHaveValue('');
      await expect(page.getByRole('combobox', { name: 'Inspector', exact: true })).toHaveValue('');
      await expect(page.getByRole('combobox', { name: 'Responsible supervisor', exact: true })).toHaveValue('');
      await expect(page.getByRole('button', { name: 'Review assignment' })).toBeDisabled();
    } else {
      await expect(page.getByRole('alert')).toBeVisible();
      await expect(page.getByLabel('Due time')).toHaveCount(0);
      await expect(page.getByRole('button', { name: 'Reload assignment source' })).toHaveCount(0);
    }
    expect(posts).toBe(0);
  });
const destination = '/tests/fixtures/inspection-assignment-destination.html';
test('invalid direct-open and disabled activation do not fetch', async ({ page }) => {
  let requests = 0;
  await page.route(u => u.pathname.startsWith('/api/'), async r => { requests++; await r.fulfill({json:{}}); });
  await page.goto(destination + '?branchId=1&roomId=-1');
  await expect(page.getByRole('alert')).toContainText('Invalid assignment context');
  await page.goto(destination + '?branchId=1&roomId=3#disabled');
  await expect(page.getByRole('alert')).toContainText('not activated');
  expect(requests).toBe(0);
});
for (const status of [401,403,404,409,412]) test('direct-open context failure ' + status, async ({page}) => {
  await page.route(u => u.pathname.startsWith('/api/'), r => r.fulfill({status,json:{title:'DENIED'}}));
  await page.goto(destination + '?branchId=1&roomId=3');
  await expect(page.getByRole('alert')).toBeVisible();
  await expect(page.getByRole('button', {name:/Task #/})).toHaveCount(0);
  await expect(page.getByLabel('Due time')).toHaveCount(0);
});
test('multiple candidates require selection; Back reloads clean room context', async ({page}) => {
  let optionReads=0, posts=0;
  await page.route(u => u.pathname.startsWith('/api/'), async r => {
    const url=new URL(r.request().url());
    if(r.request().method()==='POST') posts++;
    if(url.pathname.endsWith('/2')) { optionReads++; await r.fulfill({json:options}); return; }
    expect(url.searchParams.get('roomId')).toMatch(/^(3|9)$/);
    await r.fulfill({json:{pending:url.searchParams.get('roomId')==='3' ? [{taskId:2,roomId:3},{taskId:8,roomId:3}] : [],assigned:[]}});
  });
  await page.goto(destination+'?branchId=1&roomId=3');
  await expect(page.getByRole('button',{name:/Task #/})).toHaveCount(2);
  expect(optionReads).toBe(0);
  await page.getByRole('button',{name:/Task #2$/}).click();
  await page.getByLabel('Reason',{exact:true}).fill('Old room reason');
  await page.getByRole('link',{name:'Other room'}).click();
  await expect(page.getByText('No pending tasks.')).toBeVisible();
  await expect(page.getByLabel('Due time')).toHaveCount(0);
  await page.goBack();
  await expect(page.getByRole('button',{name:/Task #/})).toHaveCount(2);
  await expect(page.getByLabel('Reason',{exact:true})).toHaveCount(0);
  expect(optionReads).toBe(1); expect(posts).toBe(0);
});
test('task options for mismatched room fail closed', async ({page}) => {
  await page.route(u => u.pathname.startsWith('/api/'), r => r.fulfill({json:new URL(r.request().url()).pathname.endsWith('/2') ? {...options,roomId:9} : {pending:[{taskId:2,roomId:3}],assigned:[]}}));
  await page.goto(destination+'?branchId=1&roomId=3');
  await page.getByRole('button',{name:/Task #2$/}).click();
  await expect(page.getByRole('alert')).toBeVisible();
  await expect(page.getByLabel('Due time')).toHaveCount(0);
});
