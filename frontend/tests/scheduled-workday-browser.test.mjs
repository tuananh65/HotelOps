import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { existsSync } from "node:fs";
import { build } from "esbuild";
import { chromium } from "@playwright/test";
import { day, shift, session } from "./workday-fixtures.mjs";
const result = await build({
  stdin: {
    resolveDir: process.cwd(),
    loader: "tsx",
    contents: `import React,{useState}from'react';import{createRoot}from'react-dom/client';import{AuthProvider}from'./src/auth/AuthProvider';import{createAuthStore}from'./src/auth/authStore';import{createAuthApi}from'./src/auth/authApi';import{ScheduledWorkdayPanel}from'./src/workday/ScheduledWorkdayPanel';const s=window.scenario,calls=[],waiters=[];let working=s.working??false,claimed=false;const d=${JSON.stringify(day())},sh=${JSON.stringify(shift())},sess=${JSON.stringify(session())};const reply=(v,status=200)=>Response.json(v,{status,headers:{ETag:'"AAAAAAAAAAE="'}});window.fetch=async(u,o)=>{calls.push({url:u,method:o.method});if(u==='/api/auth/session')return (s.anonymous||s.revoked)?reply({},401):reply({...sess,mustChangePassword:s.restricted??false});if(u==='/api/auth/csrf'){if(s.csrfFailure==='network')throw Error('SECRET');if(s.csrfFailure==='cancelled')throw new DOMException('SECRET','AbortError');if(s.csrfFailure==='malformed')return reply({headerName:'Wrong',csrfToken:'SECRET'});if(s.csrfStatus)return reply({title:'SECRET',detail:'SECRET',traceId:'SECRET',csrfToken:'SECRET'},s.csrfStatus);return reply({headerName:'X-CSRF-TOKEN',csrfToken:'synthetic'});}const b=Number(u.split('/')[3]);if(u.endsWith('/own-shifts'))return s.shiftInvalid?reply({},401):s.shiftDenied?reply({},403):reply(s.claimOnce&&claimed?[]:[{...sh,branchId:b}]);if(u.endsWith('/current')){const f=()=>s.currentInvalid?reply({},401):s.currentDenied?reply({},403):working?reply({...d,branchId:b,openInterval:s.open?{intervalId:3,workItemKind:'TASK',workItemId:4,openedAtUtc:d.startedAtUtc,rowVersion:d.rowVersion}:null}):new Response(null,{status:204});if(s.late&&b===1)return new Promise(resolve=>waiters.push(()=>resolve(f())));return f();}const end=u.endsWith('/end');const finish=()=>{if(s.network)throw Error('SECRET');if(s.status)return reply({title:'SECRET'},s.status);working=!end;if(!end)claimed=true;const v={...d,branchId:b,status:end?'ENDED':'WORKING',endedAtUtc:end?(s.endedAtUtc??'2026-11-08T09:00:00Z'):null};delete v.openInterval;return reply(v,end?200:201);};if(s.waitPost)return new Promise(resolve=>waiters.push(()=>resolve(finish())));return finish();};const store=createAuthStore(createAuthApi(window.fetch));function App(){const[b,set]=useState(s.invalid?0:1);return <><button onClick={()=>set(2)}>Change branch</button><ScheduledWorkdayPanel branchId={b} dispatchEnabled={s.enabled??false}/></>;}createRoot(document.getElementById('root')).render(<AuthProvider store={store} fallback={<p>Checking session</p>}><App/></AuthProvider>);window.fixture={calls,recover:()=>{s.currentInvalid=false;s.shiftInvalid=false;s.revoked=false;s.csrfStatus=undefined;s.csrfFailure=undefined;},revoke:()=>{s.revoked=true;},finish:()=>waiters.splice(0).forEach(f=>f()),replace:()=>store.revalidate()};`,
  },
  bundle: true,
  write: false,
  format: "iife",
  jsx: "automatic",
  define: { "process.env.NODE_ENV": '"production"' },
});
let browser;
before(async () => {
  const executablePath = [
    "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
    "C:/Program Files/Google/Chrome/Application/chrome.exe",
  ].find(existsSync);
  browser = await chromium.launch({
    headless: true,
    ...(executablePath ? { executablePath } : {}),
  });
});
after(async () => browser?.close());
async function run(s, fn) {
  const p = await browser.newPage();
  p.setDefaultTimeout(5000);
  const errors = [],
    logs = [];
  p.on("console", (message) => logs.push(message.text()));
  p.on("pageerror", (e) => errors.push(e.message));
  await p.route("**/*", (r) =>
    r.request().resourceType() === "document"
      ? r.fulfill({ contentType: "text/html", body: '<div id="root"></div>' })
      : r.abort(),
  );
  try {
    await p.goto("https://offline.test");
    await p.evaluate((s) => {
      window.scenario = s;
      for (const k of ["localStorage", "sessionStorage"])
        Object.defineProperty(window, k, {
          get() {
            throw Error("Storage forbidden");
          },
        });
    }, s);
    await p.addScriptTag({ content: result.outputFiles[0].text });
    await fn(p);
    assert.deepEqual(errors, []);
    assert.ok(!logs.join(" ").includes("SECRET"));
    assert.ok(!(await p.locator("body").innerText()).includes("SECRET"));
  } finally {
    await p.close();
  }
}
const posts = (p) =>
  p.evaluate(() => window.fixture.calls.filter((c) => c.method === "POST"));
async function choose(p) {
  await p.getByLabel("Scheduled shift", { exact: true }).selectOption("10");
  await p.getByText("No current working day.").waitFor();
}
async function openReview(p, operation) {
  const before = await p.evaluate(() => window.fixture.calls.length);
  await p
    .getByRole("button", {
      name: operation === "start" ? "Start scheduled workday" : "End workday",
      exact: true,
    })
    .click();
  await p.getByRole("region", { name: "Review scheduled workday" }).waitFor();
  assert.equal(
    await p.evaluate(() => window.fixture.calls.length),
    before,
    "opening review must not bootstrap CSRF or POST",
  );
}
async function perform(p, operation) {
  await openReview(p, operation);
  await p
    .getByRole("button", {
      name:
        operation === "start"
          ? "Confirm scheduled start"
          : "Confirm workday end",
      exact: true,
    })
    .click();
}
test("OFF zero CSRF/POST", () =>
  run({}, async (p) => {
    await choose(p);
    assert.equal(
      await p
        .getByRole("button", { name: "Start scheduled workday" })
        .isDisabled(),
      true,
    );
    assert.equal(
      (await p.evaluate(() => window.fixture.calls)).filter(
        (c) => c.method === "POST" || c.url.endsWith("/csrf"),
      ).length,
      0,
    );
  }));
test("confirmed start then end refresh authoritative current", () =>
  run({ enabled: true }, async (p) => {
    await choose(p);
    await perform(p, "start");
    await p.getByText("Working day 20", { exact: true }).waitFor();
    assert.equal(
      await p
        .getByRole("button", { name: "Start scheduled workday" })
        .isDisabled(),
      true,
    );
    await perform(p, "end");
    await p.getByText("No current working day.").waitFor();
    assert.equal((await posts(p)).length, 2);
  }));
test("open interval blocks End", () =>
  run({ enabled: true, working: true, open: true }, async (p) => {
    await p.getByText(/End blocked:/).waitFor();
    assert.equal(
      await p
        .getByRole("button", { name: "End workday", exact: true })
        .isDisabled(),
      true,
    );
    assert.equal((await posts(p)).length, 0);
  }));
for (const s of [{ restricted: true }, { anonymous: true }, { invalid: true }])
  test("no protected calls " + JSON.stringify(s), () =>
    run(s, async (p) => {
      await p
        .getByText(
          s.invalid
            ? "Choose a valid branch ID."
            : "Confirmed unrestricted session required.",
        )
        .waitFor();
      assert.equal(
        (await p.evaluate(() => window.fixture.calls)).filter((c) =>
          c.url.includes("/workdays"),
        ).length,
        0,
      );
    }),
  );
test("double click one POST", () =>
  run({ enabled: true, waitPost: true }, async (p) => {
    await choose(p);
    await openReview(p, "start");
    await p
      .getByRole("button", { name: "Confirm scheduled start", exact: true })
      .evaluate((b) => {
        b.click();
        b.click();
      });
    await p.waitForFunction(() =>
      window.fixture.calls.some((c) => c.method === "POST"),
    );
    assert.equal((await posts(p)).length, 1);
    await p.evaluate(() => window.fixture.finish());
    await p.getByText("Working day 20", { exact: true }).waitFor();
  }));
test("unknown outcome stays locked after refresh", () =>
  run({ enabled: true, network: true }, async (p) => {
    await choose(p);
    await perform(p, "start");
    await p.getByText(/Outcome unknown/).waitFor();
    await p.getByRole("button", { name: "Refresh snapshot" }).click();
    await p.getByText("No current working day.").waitFor();
    assert.equal(
      await p.getByLabel("Scheduled shift", { exact: true }).isDisabled(),
      true,
    );
    assert.equal(
      await p
        .getByRole("button", { name: "Start scheduled workday" })
        .isDisabled(),
      true,
    );
    assert.equal((await posts(p)).length, 1);
  }));
test("branch replacement ignores stale current", () =>
  run({ late: true, working: true }, async (p) => {
    await p.waitForFunction(() =>
      window.fixture.calls.some((c) => c.url.endsWith("/current")),
    );
    await p.getByRole("button", { name: "Change branch" }).click();
    await p.getByText("Working day 20", { exact: true }).waitFor();
    await p.evaluate(() => window.fixture.finish());
    assert.match(await p.locator("main").innerText(), /Branch 2/);
  }));
test("session replacement ignores late POST", () =>
  run({ enabled: true, waitPost: true }, async (p) => {
    await choose(p);
    await perform(p, "start");
    await p.waitForFunction(() =>
      window.fixture.calls.some((c) => c.method === "POST"),
    );
    await p.evaluate(() => window.fixture.replace());
    await p.getByText("No current working day.").waitFor();
    await p.evaluate(() => window.fixture.finish());
    assert.equal(await p.getByText(/Start confirmed/).count(), 0);
    assert.equal((await posts(p)).length, 1);
  }));
test("current denial blocks Start", () =>
  run({ enabled: true, currentDenied: true }, async (p) => {
    await p.getByText(/Current workday unavailable/).waitFor();
    await p.getByLabel("Scheduled shift", { exact: true }).selectOption("10");
    assert.equal(
      await p
        .getByRole("button", { name: "Start scheduled workday" })
        .isDisabled(),
      true,
    );
  }));
test("shift denial permits independent End", () =>
  run({ enabled: true, working: true, shiftDenied: true }, async (p) => {
    await p.getByText("Working day 20", { exact: true }).waitFor();
    await perform(p, "end");
    await p.getByText("No current working day.").waitFor();
    assert.equal((await posts(p)).length, 1);
  }));
for (const status of [401, 403, 409, 412, 503])
  test("UI mutation error " + status, () =>
    run({ enabled: true, status }, async (p) => {
      await choose(p);
      await perform(p, "start");
      await p
        .getByRole(status === 401 ? "alert" : "status")
        .filter({
          hasText:
            status === 401
              ? "Session invalid"
              : status === 403
                ? "Server denied"
                : status === 409
                  ? "Workday conflict"
                  : status === 412
                    ? "version changed"
                    : "Outcome unknown",
        })
        .waitFor();
      assert.equal((await posts(p)).length, 1);
    }),
  );
test("double End dispatches once and cannot end empty current", () =>
  run({ enabled: true, working: true, waitPost: true }, async (p) => {
    await p.getByText("Working day 20", { exact: true }).waitFor();
    await openReview(p, "end");
    await p
      .getByRole("button", { name: "Confirm workday end", exact: true })
      .evaluate((b) => {
        b.click();
        b.click();
      });
    await p.waitForFunction(() =>
      window.fixture.calls.some((c) => c.method === "POST"),
    );
    assert.equal((await posts(p)).length, 1);
    await p.evaluate(() => window.fixture.finish());
    await p.getByText("No current working day.").waitFor();
    assert.equal(
      await p
        .getByRole("button", { name: "End workday", exact: true })
        .isDisabled(),
      true,
    );
  }));
test("branch change during POST discards old success", () =>
  run({ enabled: true, waitPost: true }, async (p) => {
    await choose(p);
    await perform(p, "start");
    await p.waitForFunction(() =>
      window.fixture.calls.some((c) => c.method === "POST"),
    );
    await p.getByRole("button", { name: "Change branch" }).click();
    await p.getByText("No current working day.").waitFor();
    await p.evaluate(() => window.fixture.finish());
    assert.equal(await p.getByText(/Start confirmed/).count(), 0);
    assert.match(await p.locator("main").innerText(), /Branch 2/);
    assert.equal((await posts(p)).length, 1);
  }));

for (const scenario of [
  { shiftInvalid: true, working: true },
  { currentInvalid: true, working: true },
  { shiftInvalid: true, working: true, late: true },
]) {
  test(
    "read 401 hides all protected snapshots and requires session recovery " +
      JSON.stringify(scenario),
    () =>
      run({ ...scenario, enabled: true }, async (p) => {
        await p
          .getByText(
            "Session invalid. Revalidate before reading or changing workday.",
          )
          .waitFor();
        await p.evaluate(() => window.fixture.finish());
        assert.equal(
          await p.getByText("Working day 20", { exact: true }).count(),
          0,
        );
        assert.equal(
          await p.getByLabel("Scheduled shift", { exact: true }).count(),
          0,
        );
        assert.equal(
          await p
            .getByRole("button", { name: "Refresh snapshot", exact: true })
            .count(),
          0,
        );
        assert.equal((await posts(p)).length, 0);
        const before = await p.evaluate(() => window.fixture.calls.length);
        await p
          .getByRole("button", { name: "Change branch", exact: true })
          .click();
        assert.equal(
          await p.evaluate(() => window.fixture.calls.length),
          before,
        );
        await p.evaluate(() => window.fixture.revoke());
        await p
          .getByRole("button", { name: "Revalidate session", exact: true })
          .click();
        await p.waitForFunction(
          () =>
            window.fixture.calls.filter((c) => c.url === "/api/auth/session")
              .length === 2,
        );
        assert.equal((await posts(p)).length, 0);
        assert.equal(
          await p.getByText("Working day 20", { exact: true }).count(),
          0,
        );
      }),
  );
}

test("mutation 401 removes previous data and prevents repeated End", () =>
  run({ enabled: true, working: true, status: 401 }, async (p) => {
    await p.getByText("Working day 20", { exact: true }).waitFor();
    await perform(p, "end");
    await p
      .getByText(
        "Session invalid. Revalidate before reading or changing workday.",
      )
      .waitFor();
    assert.equal(
      await p.getByText("Working day 20", { exact: true }).count(),
      0,
    );
    assert.equal(
      await p.getByRole("button", { name: "End workday", exact: true }).count(),
      0,
    );
    assert.equal((await posts(p)).length, 1);
  }));

test("explicit successful session revalidation restores reads without POST replay", () =>
  run({ enabled: true, working: true, currentInvalid: true }, async (p) => {
    await p
      .getByText(
        "Session invalid. Revalidate before reading or changing workday.",
      )
      .waitFor();
    await p.evaluate(() => window.fixture.recover());
    await p
      .getByRole("button", { name: "Revalidate session", exact: true })
      .click();
    await p.getByText("Working day 20", { exact: true }).waitFor();
    assert.equal((await posts(p)).length, 0);
    assert.equal(
      (await p.evaluate(() => window.fixture.calls)).filter(
        (c) => c.url === "/api/auth/session",
      ).length,
      2,
    );
  }));

test("malformed End calendar locks replay without confirmed success or refresh", () =>
  run(
    { enabled: true, working: true, endedAtUtc: "2027-02-29T09:00:00Z" },
    async (p) => {
      await p.getByText("Working day 20", { exact: true }).waitFor();
      await perform(p, "end");
      await p.getByText(/Outcome unknown/).waitFor();
      assert.equal(await p.getByText(/End confirmed/).count(), 0);
      assert.equal(
        await p
          .getByRole("button", { name: "End workday", exact: true })
          .isDisabled(),
        true,
      );
      assert.equal((await posts(p)).length, 1);
      assert.equal(
        (await p.evaluate(() => window.fixture.calls)).filter((c) =>
          c.url.endsWith("/current"),
        ).length,
        1,
      );
    },
  ));

async function triggerScheduled(p, operation) {
  if (operation === "start") await choose(p);
  else await p.getByText("Working day 20", { exact: true }).waitFor();
  await perform(p, operation);
}
for (const operation of ["start", "end"]) {
  for (const recovered of [false, true]) {
    test(
      "CSRF 401 " +
        operation +
        " requires confirmed session recovery: " +
        recovered,
      () =>
        run(
          { enabled: true, working: operation === "end", csrfStatus: 401 },
          async (p) => {
            await triggerScheduled(p, operation);
            await p
              .getByText(
                "Session invalid. Revalidate before reading or changing workday.",
              )
              .waitFor();
            assert.equal(
              await p.getByText("Working day 20", { exact: true }).count(),
              0,
            );
            assert.equal(
              await p.getByLabel("Scheduled shift", { exact: true }).count(),
              0,
            );
            const before = await p.evaluate(() => window.fixture.calls.length);
            await p
              .getByRole("button", { name: "Change branch", exact: true })
              .click();
            assert.equal(
              await p.evaluate(() => window.fixture.calls.length),
              before,
            );
            await p.evaluate(
              (success) =>
                success ? window.fixture.recover() : window.fixture.revoke(),
              recovered,
            );
            await p
              .getByRole("button", { name: "Revalidate session", exact: true })
              .click();
            await p.waitForFunction(
              () =>
                window.fixture.calls.filter(
                  (c) => c.url === "/api/auth/session",
                ).length === 2,
            );
            if (recovered)
              await p
                .getByRole("heading", {
                  name: "Scheduled workday",
                  exact: true,
                })
                .waitFor();
            else {
              await p
                .getByText("Confirmed unrestricted session required.", {
                  exact: true,
                })
                .waitFor();
              assert.equal(
                await p
                  .getByRole("heading", {
                    name: "Scheduled workday",
                    exact: true,
                  })
                  .count(),
                0,
              );
            }
            const calls = await p.evaluate(() => window.fixture.calls);
            assert.equal(
              calls.filter((c) => c.url === "/api/auth/csrf").length,
              1,
            );
            assert.equal((await posts(p)).length, 0);
            assert.ok(!JSON.stringify(calls).includes("SECRET"));
          },
        ),
    );
  }
  for (const status of [403, 503]) {
    test(
      "CSRF " +
        status +
        " " +
        operation +
        " remains operation-level with no POST",
      () =>
        run(
          { enabled: true, working: operation === "end", csrfStatus: status },
          async (p) => {
            await triggerScheduled(p, operation);
            await p
              .getByRole("status")
              .filter({
                hasText:
                  status === 403 ? "Server denied" : "Service unavailable",
              })
              .waitFor();
            assert.equal(
              await p
                .getByText(
                  "Session invalid. Revalidate before reading or changing workday.",
                )
                .count(),
              0,
            );
            assert.equal(
              await p
                .getByRole("heading", {
                  name: "Scheduled workday",
                  exact: true,
                })
                .count(),
              1,
            );
            assert.equal((await posts(p)).length, 0);
            assert.equal(
              (await p.evaluate(() => window.fixture.calls)).filter(
                (c) => c.url === "/api/auth/csrf",
              ).length,
              1,
            );
          },
        ),
    );
  }
}
for (const csrfFailure of ["network", "cancelled", "malformed"]) {
  test(
    "CSRF " + csrfFailure + " cannot dispatch or expose sensitive failure",
    () =>
      run({ enabled: true, csrfFailure }, async (p) => {
        await triggerScheduled(p, "start");
        await p
          .getByRole("status")
          .filter({ hasText: "Operation not confirmed" })
          .waitFor();
        assert.equal((await posts(p)).length, 0);
        assert.equal(
          (await p.evaluate(() => window.fixture.calls)).filter(
            (c) => c.url === "/api/auth/csrf",
          ).length,
          1,
        );
      }),
  );
}

for (const operation of ["start", "end"]) {
  test(
    "review " +
      operation +
      " shows server record and cancel dispatches nothing",
    () =>
      run({ enabled: true, working: operation === "end" }, async (p) => {
        if (operation === "start") await choose(p);
        else await p.getByText("Working day 20", { exact: true }).waitFor();
        await openReview(p, operation);
        const region = p.getByRole("region", {
          name: "Review scheduled workday",
        });
        assert.match(
          await region.innerText(),
          operation === "start" ? /Assignment 10/ : /Workday 20/,
        );
        assert.match(
          await region.innerText(),
          /Actual time is determined by the server/,
        );
        await p.getByRole("button", { name: "Cancel review" }).click();
        assert.equal(await region.count(), 0);
        assert.equal((await posts(p)).length, 0);
        assert.equal(
          await p.evaluate(
            () =>
              window.fixture.calls.filter((c) => c.url === "/api/auth/csrf")
                .length,
          ),
          0,
        );
      }),
  );
  test("refresh invalidates " + operation + " review without replay", () =>
    run({ enabled: true, working: operation === "end" }, async (p) => {
      if (operation === "start") await choose(p);
      else await p.getByText("Working day 20", { exact: true }).waitFor();
      await openReview(p, operation);
      await p.getByRole("button", { name: "Refresh snapshot" }).click();
      assert.equal(
        await p
          .getByRole("region", { name: "Review scheduled workday" })
          .count(),
        0,
      );
      assert.equal((await posts(p)).length, 0);
    }),
  );
}
test("changing selected shift invalidates review", () =>
  run({ enabled: true }, async (p) => {
    await choose(p);
    await openReview(p, "start");
    await p.getByLabel("Scheduled shift", { exact: true }).selectOption("");
    assert.equal(
      await p.getByRole("button", { name: "Confirm scheduled start" }).count(),
      0,
    );
    assert.equal((await posts(p)).length, 0);
  }));
test("branch replacement discards reviewed identifiers", () =>
  run({ enabled: true }, async (p) => {
    await choose(p);
    await openReview(p, "start");
    await p.getByRole("button", { name: "Change branch" }).click();
    await p.getByText("No current working day.").waitFor();
    assert.equal(
      await p.getByRole("region", { name: "Review scheduled workday" }).count(),
      0,
    );
    assert.equal((await posts(p)).length, 0);
  }));
test("session replacement discards unsubmitted review", () =>
  run({ enabled: true }, async (p) => {
    await choose(p);
    await openReview(p, "start");
    await p.evaluate(() => window.fixture.replace());
    await p.getByText("No current working day.").waitFor();
    assert.equal(
      await p.getByRole("region", { name: "Review scheduled workday" }).count(),
      0,
    );
    assert.equal((await posts(p)).length, 0);
  }));

test("032 scheduled claim stays consumed after End; receipt retains server times", () =>
  run({ enabled: true, claimOnce: true }, async (p) => {
    await choose(p);
    await perform(p, "start");
    await p.getByText("Working day 20", { exact: true }).waitFor();
    await p
      .getByRole("region", { name: "Confirmed workday result" })
      .getByText("Workday 20: WORKING")
      .waitFor();
    await perform(p, "end");
    await p.getByText("No current working day.").waitFor();
    await p.getByText("No assigned shifts.").waitFor();
    const receipt = p.getByRole("region", { name: "Confirmed workday result" });
    await receipt.getByText("Workday 20: ENDED").waitFor();
    assert.equal(await receipt.locator("time").count(), 2);
    assert.equal(
      await p
        .getByRole("button", { name: "Start scheduled workday" })
        .isDisabled(),
      true,
    );
    assert.equal((await posts(p)).length, 2);
  }));
test("032 confirmed receipt is removed on branch replacement", () =>
  run({ enabled: true }, async (p) => {
    await choose(p);
    await perform(p, "start");
    await p.getByRole("region", { name: "Confirmed workday result" }).waitFor();
    await p.getByRole("button", { name: "Change branch" }).click();
    await p.getByText("Working day 20", { exact: true }).waitFor();
    assert.equal(
      await p.getByRole("region", { name: "Confirmed workday result" }).count(),
      0,
    );
  }));
test("032 confirmed receipt is removed on session replacement", () =>
  run({ enabled: true }, async (p) => {
    await choose(p);
    await perform(p, "start");
    await p.getByRole("region", { name: "Confirmed workday result" }).waitFor();
    await p.evaluate(() => window.fixture.replace());
    await p.getByText("Working day 20", { exact: true }).waitFor();
    assert.equal(
      await p.getByRole("region", { name: "Confirmed workday result" }).count(),
      0,
    );
  }));
test("032 unknown Start outcome creates no confirmed receipt", () =>
  run({ enabled: true, network: true }, async (p) => {
    await choose(p);
    await perform(p, "start");
    await p.getByText(/Outcome unknown/).waitFor();
    assert.equal(
      await p.getByRole("region", { name: "Confirmed workday result" }).count(),
      0,
    );
    assert.equal((await posts(p)).length, 1);
  }));
