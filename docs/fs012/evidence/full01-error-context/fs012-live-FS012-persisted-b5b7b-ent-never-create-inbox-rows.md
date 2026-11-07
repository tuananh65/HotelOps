# Instructions

- Following Playwright test failed.
- Explain why, be concise, respect Playwright best practices.
- Provide a snippet of code with the fix, if possible.

# Test info

- Name: fs012-live.spec.ts >> FS012 persisted recipient inactivity, lost branch access and superseded assignment never create inbox rows
- Location: tests\fs012-live.spec.ts:903:1

# Error details

```
Error: expect(received).toBe(expected) // Object.is equality

Expected: 200
Received: 400
```

# Test source

```ts
  825  |     revokedActorReplay: 403,
  826  |     unauthorizedInboxRows: 0,
  827  |     guard,
  828  |   });
  829  | });
  830  |
  831  | test("FS012 blocked room Create preserves legacy state, keeps effective block and exposes actual item preview", async ({
  832  |   page,
  833  | }) => {
  834  |   await login(page.context(), "admin");
  835  |   const room = mode("block-unused-room");
  836  |   const opts = await (
  837  |     await page.request.get(
  838  |       `/api/branches/${fixture.branchId}/cleaning-task-options?roomId=${room.roomId}`,
  839  |     )
  840  |   ).json();
  841  |   expect(opts.cleaningItems.length).toBe(2);
  842  |   await page.goto(
  843  |     `/cleaning/tasks/new?branchId=${fixture.branchId}&roomId=${room.roomId}`,
  844  |   );
  845  |   await expect(
  846  |     page.getByText(opts.cleaningItems[0].label, { exact: false }),
  847  |   ).toBeVisible();
  848  |   await page
  849  |     .locator("input[name=dueAt]")
  850  |     .fill(new Date(Date.now() + 3600000).toISOString());
  851  |   const pending = page.waitForResponse(
  852  |     (r) =>
  853  |       r.url().endsWith(`/api/branches/${fixture.branchId}/cleaning-tasks`) &&
  854  |       r.request().method() === "POST",
  855  |   );
  856  |   await page
  857  |     .getByRole("button", { name: "Save and Assign", exact: true })
  858  |     .click();
  859  |   const response = await pending;
  860  |   expect(response.status()).toBe(201);
  861  |   const created = await response.json();
  862  |   expect(created.hasEffectiveBlock).toBe(true);
  863  |   expect(created.readiness).toBe("PENDING_CLEANING");
  864  |   expect(created.itemsInitialized).toBe(false);
  865  |   await expect(page).toHaveURL(
  866  |     new RegExp(
  867  |       `/cleaning/assignment\\?branchId=${fixture.branchId}&taskId=${created.id}`,
  868  |     ),
  869  |   );
  870  |   const proof = call([
  871  |     "lifecycle-inspect",
  872  |     "--local-sql-certificate-exception",
  873  |   ]);
  874  |   expect(
  875  |     proof.rooms.find((x: any) => x.roomId === room.roomId).operationalStatus,
  876  |   ).toBe(room.operationalStatus);
  877  |   expect(proof.events.find((x: any) => x.taskId === created.id).blocked).toBe(
  878  |     true,
  879  |   );
  880  |   const duplicate = await (
  881  |     await page.request.get(
  882  |       `/api/branches/${fixture.branchId}/cleaning-task-options?roomId=${room.roomId}`,
  883  |     )
  884  |   ).json();
  885  |   expect(duplicate.existingTaskId).toBe(created.id);
  886  |   await page.goto(
  887  |     `/cleaning/tasks/new?branchId=${fixture.branchId}&roomId=${room.roomId}`,
  888  |   );
  889  |   await expect(
  890  |     page.getByRole("link", { name: "Open existing cleaning task" }),
  891  |   ).toHaveAttribute("href", new RegExp(`/cleaning/tasks/${created.id}\\?`));
  892  |   record("uc028-blocked-preview-continuation", {
  893  |     legacyUnchanged: true,
  894  |     effectiveBlock: true,
  895  |     expectedReleaseDoesNotRelease: true,
  896  |     readiness: "PENDING_CLEANING",
  897  |     snapshotNotFrozen: true,
  898  |     saveAndAssignNavigation: true,
  899  |     existingTaskLink: true,
  900  |   });
  901  | });
  902  |
  903  | test("FS012 persisted recipient inactivity, lost branch access and superseded assignment never create inbox rows", async ({
  904  |   page,
  905  | }) => {
  906  |   await login(page.context(), "admin");
  907  |   const results = [];
  908  |   for (const [mutate, restore, terminal] of [
  909  |     ["recipient-inactive", "recipient-active", "RECIPIENT_INACTIVE"],
  910  |     [
  911  |       "recipient-branch-revoke",
  912  |       "recipient-branch-restore",
  913  |       "RECIPIENT_OUT_OF_SCOPE",
  914  |     ],
  915  |     ["supersede-latest", null, "SUPERSEDED"],
  916  |   ]) {
  917  |     const n = await createTask(page.context());
  918  |     const r = await assign(
  919  |       page.context(),
  920  |       n.task.id,
  921  |       n.tag,
  922  |       crypto.randomUUID(),
  923  |       command(),
  924  |     );
> 925  |     expect(r.status()).toBe(200);
       |                        ^ Error: expect(received).toBe(expected) // Object.is equality
  926  |     const result = await r.json();
  927  |     mode(mutate!, result.publicationId);
  928  |     try {
  929  |       expect(mode("deliver", result.publicationId).status).toBe(terminal);
  930  |     } finally {
  931  |       if (restore) mode(restore);
  932  |     }
  933  |     const proof = inspectNotification(),
  934  |       intent = proof.intents.find(
  935  |         (x: any) => x.publicationId === result.publicationId,
  936  |       );
  937  |     expect(intent.status).toBe("TERMINAL");
  938  |     expect(intent.inboxNotificationId).toBeNull();
  939  |     results.push(terminal);
  940  |   }
  941  |   const parity = mode("model-parity");
  942  |   expect(parity.rows.length).toBe(3);
  943  |   record("terminal-source-policy-and-model", {
  944  |     results,
  945  |     parity,
  946  |     unauthorizedInboxRows: 0,
  947  |   });
  948  | });
  949  |
  950  | test("FS012 real UC032 PUT retains IN_PROGRESS at 100 percent and enforces ownership and stale tag", async ({
  951  |   page,
  952  | }) => {
  953  |   await login(page.context(), "owner");
  954  |   const url = `/api/branches/${fixture.branchId}/cleaning-tasks/${fixture.taskId}/progress`;
  955  |   const before = await page.request.get(url);
  956  |   expect(before.status()).toBe(200);
  957  |   const data = await before.json(),
  958  |     tag = before.headers().etag;
  959  |   const csrf = await (await page.request.get("/api/auth/csrf")).json();
  960  |   const body = {
  961  |     percent: 100,
  962  |     items: data.items.map((x: any) => ({
  963  |       id: x.id,
  964  |       done: true,
  965  |       note: "FS012 UC032 regression",
  966  |     })),
  967  |     note: "FS012 real SQL UC032",
  968  |   };
  969  |   const saved = await page.request.put(url, {
  970  |     headers: { "X-CSRF-TOKEN": csrf.csrfToken, "If-Match": tag },
  971  |     data: body,
  972  |   });
  973  |   expect(saved.status()).toBe(200);
  974  |   const result = await saved.json();
  975  |   expect(result.state).toBe("IN_PROGRESS");
  976  |   expect(result.percent).toBe(100);
  977  |   const fresh = await (await page.request.get("/api/auth/csrf")).json();
  978  |   expect(
  979  |     (
  980  |       await page.request.put(url, {
  981  |         headers: { "X-CSRF-TOKEN": fresh.csrfToken, "If-Match": tag },
  982  |         data: body,
  983  |       })
  984  |     ).status(),
  985  |   ).toBe(412);
  986  |   const proof = call(["persistence", "--local-sql-certificate-exception"]);
  987  |   expect(proof.status).toBe("IN_PROGRESS");
  988  |   expect(proof.ownerIsAuthor).toBe(true);
  989  |   expect(proof.auditMatches).toBe(true);
  990  |   await page.context().clearCookies();
  991  |   await login(page.context(), "other");
  992  |   const other = await (await page.request.get("/api/auth/csrf")).json();
  993  |   expect(
  994  |     (
  995  |       await page.request.put(url, {
  996  |         headers: {
  997  |           "X-CSRF-TOKEN": other.csrfToken,
  998  |           "If-Match": saved.headers().etag,
  999  |         },
  1000 |         data: body,
  1001 |       })
  1002 |     ).status(),
  1003 |   ).toBe(403);
  1004 |   record("uc032-real-write-regression", {
  1005 |     read: 200,
  1006 |     put: 200,
  1007 |     percent: 100,
  1008 |     state: "IN_PROGRESS",
  1009 |     stale: 412,
  1010 |     foreignOwner: 403,
  1011 |     sqlPersistence: true,
  1012 |     auditIdentity: true,
  1013 |   });
  1014 | });
  1015 |
  1016 | test("FS012 assignment authority rejects anonymous, forbidden, foreign branch and invalid key without mutation", async ({
  1017 |   page,
  1018 |   browser,
  1019 | }) => {
  1020 |   await login(page.context(), "admin");
  1021 |   const source = call([
  1022 |       "lifecycle-inspect",
  1023 |       "--local-sql-certificate-exception",
  1024 |     ]),
  1025 |     notification = inspectNotification();
```