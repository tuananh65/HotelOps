import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { existsSync } from "node:fs";
import { build } from "esbuild";
import { chromium } from "@playwright/test";
const compiled = await build({
  stdin: {
    resolveDir: process.cwd(),
    loader: "tsx",
    contents: `import React from'react';import{flushSync}from'react-dom';import{createRoot}from'react-dom/client';import{AuthProvider}from'./src/auth/AuthProvider';import{createAuthStore}from'./src/auth/authStore';import{createAuthApi}from'./src/auth/authApi';import{StructureDeactivationPanel}from'./src/structure-deactivation/StructureDeactivationPanel';
const s=window.scenario,calls=[],waiters=[];const reply=(body,status=200)=>{const r=Response.json(body,{status,headers:{ETag:'"'+(s.etag??'A').repeat(64)+'"'}});return r;};
window.fetch=async(u,o)=>{calls.push({url:u,method:o.method,body:o.body,headers:Object.fromEntries(new Headers(o.headers))});if(u==='/api/auth/session'&&s.anonymous)return reply({status:401},401);if(u==='/api/auth/session')return reply({user:{id:s.principal??'7',username:'tester',name:'Tester'},expiresAt:'2099-01-01T00:00:00Z',absoluteExpiresAt:'2099-01-02T00:00:00Z',mustChangePassword:s.restricted??false,csrfToken:'token'});if(u==='/api/auth/csrf'){if(s.waitCsrf)return new Promise(resolve=>waiters.push(()=>resolve(reply({headerName:'X-CSRF-TOKEN',csrfToken:'token'}))));return reply({headerName:'X-CSRF-TOKEN',csrfToken:'token'},s.csrfStatus??200);}
const b=Number(u.split('/')[3]),kind=u.split('/')[4]==='floors'?'floor':'area',id=Number(u.split('/')[5]);const body={kind,id,branchId:b,code:'F'+b,name:'Protected '+b,description:null,floorId:kind==='area'?5:null,floorNumber:kind==='floor'?2:null,isActive:true};if(o.method==='GET'){if(s.late&&b===42)return new Promise(resolve=>waiters.push(()=>resolve(reply({record:body,dependencies:[],writerCoordinationRequired:true}))));return reply({record:body,dependencies:s.dependencies??[],writerCoordinationRequired:true});}
if(s.network)throw Error('SECRET');if(s.waitPost)return new Promise(resolve=>waiters.push(()=>resolve(reply({status:s.lateStatus??503,title:'STRUCTURE_WRITER_COORDINATION_REQUIRED'},s.lateStatus??503))));if(s.unreadable){const r=reply({...body,isActive:false});r.text=async()=>{throw Error('SECRET')};return r;}return reply({status:s.status??409,title:s.status===412?'STALE_STRUCTURE_VERSION':s.status===503?'STRUCTURE_WRITER_COORDINATION_REQUIRED':'STRUCTURE_DEPENDENCIES_EXIST',detail:'SECRET',dependencies:[{kind:'ACTIVE_ROOM',count:2,ids:[3,4]}]},s.status??409);};
let store=createAuthStore(createAuthApi(window.fetch)),visible=true,b=42,kind=s.kind??'floor',id=11,provider=0;const root=createRoot(document.getElementById('root'));function render(){flushSync(()=>root.render(<AuthProvider key={provider} store={store} fallback={<p>Session unavailable</p>}><button onClick={()=>{b=43;render()}}>Change branch</button>{visible&&<StructureDeactivationPanel kind={kind} branchId={b} id={id} dispatchEnabled={s.enabled??false}/>}</AuthProvider>));}render();window.fixture={calls,scenario:s,finish:()=>waiters.splice(0).forEach(f=>f()),replace:()=>store.revalidate(),mount:v=>{visible=v;render()},target:(branch,recordKind,recordId)=>{b=branch;kind=recordKind;id=recordId;render()},provider:principal=>{s.principal=principal;store=createAuthStore(createAuthApi(window.fetch));provider++;render()}};`,
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
    await p.addScriptTag({ content: compiled.outputFiles[0].text });
    await fn(p);
    assert.ok(!(await p.locator("body").innerText()).includes("SECRET"));
  } finally {
    await p.close();
  }
}
async function review(p) {
  await p.getByText("Protected 42", { exact: true }).waitFor();
  await p.getByLabel("Lý do", { exact: true }).fill("Retire unused floor");
  await p.getByRole("button", { name: "Xem xét", exact: true }).click();
}
test("default OFF permits review but no dispatch", () =>
  run({}, async (p) => {
    await review(p);
    assert.equal(
      await p
        .getByRole("button", { name: "Confirm deactivation" })
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
for (const status of [409, 412, 503])
  test("safe distinct error " + status, () =>
    run({ enabled: true, status }, async (p) => {
      await review(p);
      await p.getByRole("button", { name: "Confirm deactivation" }).click();
      await p.getByRole("alert").waitFor();
      const text = await p.getByRole("alert").innerText();
      assert.match(
        text,
        status === 409
          ? /Các ràng buộc/
          : status === 412
            ? /Version changed/
            : /Chưa chấp nhận cơ chế phối hợp ghi\. Không thể vô hiệu hóa\./,
      );
      assert.equal(
        (await p.evaluate(() => window.fixture.calls)).filter(
          (c) => c.method === "POST",
        ).length,
        1,
      );
    }),
  );
test("reason validation precedes review", () =>
  run({}, async (p) => {
    await p.getByText("Protected 42", { exact: true }).waitFor();
    await p.getByLabel("Lý do", { exact: true }).fill("short");
    await p.getByRole("button", { name: "Xem xét", exact: true }).click();
    await p.getByText(/Enter a reason/).waitFor();
    assert.equal(
      await p.getByRole("button", { name: "Confirm deactivation" }).count(),
      0,
    );
  }));
test("double confirm exactly one POST and unknown locks replay", () =>
  run({ enabled: true, waitPost: true }, async (p) => {
    await review(p);
    await p
      .getByRole("button", { name: "Confirm deactivation" })
      .evaluate((b) => {
        b.click();
        b.click();
      });
    await p.waitForFunction(() =>
      window.fixture.calls.some((c) => c.method === "POST"),
    );
    assert.equal(
      (await p.evaluate(() => window.fixture.calls)).filter(
        (c) => c.method === "POST",
      ).length,
      1,
    );
    await p.evaluate(() => window.fixture.finish());
    await p.getByText(/Outcome unknown/).waitFor();
    assert.equal(
      await p.getByRole("button", { name: "Refresh record" }).isDisabled(),
      true,
    );
  }));
test("branch change ignores late previous record", () =>
  run({ late: true }, async (p) => {
    await p.waitForFunction(() =>
      window.fixture.calls.some((c) => c.url.includes("/42/floors")),
    );
    await p.getByRole("button", { name: "Change branch" }).click();
    await p.getByText("Protected 43", { exact: true }).waitFor();
    await p.evaluate(() => window.fixture.finish());
    assert.equal(await p.getByText("Protected 42", { exact: true }).count(), 0);
  }));
test("restricted session sends no protected GET", () =>
  run({ restricted: true }, async (p) => {
    await p
      .getByText("Cần phiên đăng nhập đã xác minh và không bị hạn chế.")
      .waitFor();
    assert.equal(
      (await p.evaluate(() => window.fixture.calls)).filter((c) =>
        c.url.includes("/floors/"),
      ).length,
      0,
    );
  }));
test("session replacement clears pending review", () =>
  run({}, async (p) => {
    await review(p);
    await p.evaluate(() => window.fixture.replace());
    await p.getByText("Protected 42", { exact: true }).waitFor();
    assert.equal(
      await p.getByRole("button", { name: "Confirm deactivation" }).count(),
      0,
    );
  }));
test("anonymous session sends no record request", () =>
  run({ anonymous: true }, async (p) => {
    await p
      .getByText("Cần phiên đăng nhập đã xác minh và không bị hạn chế.")
      .waitFor();
    assert.equal(
      (await p.evaluate(() => window.fixture.calls)).filter((c) =>
        c.url.includes("/floors/"),
      ).length,
      0,
    );
  }));
test("branch change during POST hides old result", () =>
  run({ enabled: true, waitPost: true }, async (p) => {
    await review(p);
    await p.getByRole("button", { name: "Confirm deactivation" }).click();
    await p.waitForFunction(() =>
      window.fixture.calls.some((c) => c.method === "POST"),
    );
    await p.getByRole("button", { name: "Change branch" }).click();
    await p.getByText("Protected 43", { exact: true }).waitFor();
    await p.evaluate(() => window.fixture.finish());
    assert.equal(await p.getByRole("alert").count(), 0);
    assert.equal(await p.getByText("Protected 42", { exact: true }).count(), 0);
    assert.equal(
      (await p.evaluate(() => window.fixture.calls)).filter(
        (c) => c.method === "POST",
      ).length,
      1,
    );
  }));
test("dependency preview displays server counts without granting dispatch", () =>
  run(
    { dependencies: [{ kind: "ACTIVE_ROOM", count: 2, ids: [3, 4] }] },
    async (p) => {
      await p.getByText("ACTIVE_ROOM: 2", { exact: true }).waitFor();
      await review(p);
      assert.equal(
        await p
          .getByRole("button", { name: "Confirm deactivation" })
          .isDisabled(),
        true,
      );
      assert.equal(
        (await p.evaluate(() => window.fixture.calls)).filter(
          (c) => c.method === "POST",
        ).length,
        0,
      );
    },
  ));

test("unknown POST cannot repeat after component remount", () =>
  run({ enabled: true, status: 503 }, async (p) => {
    await review(p);
    await p.getByRole("button", { name: "Confirm deactivation" }).click();
    await p.getByText(/Outcome unknown/).waitFor();
    await p.evaluate(() => window.fixture.mount(false));
    await p.evaluate(() => window.fixture.mount(true));
    await p.waitForFunction(
      () =>
        document.querySelector("textarea") ||
        document.body.textContent.includes("Outcome unknown"),
    );
    if (await p.getByLabel("Lý do", { exact: true }).count()) {
      await review(p);
      await p.getByRole("button", { name: "Confirm deactivation" }).click();
      await p.getByText(/Outcome unknown/).waitFor();
    }
    assert.equal(
      await p.evaluate(
        () => window.fixture.calls.filter((c) => c.method === "POST").length,
      ),
      1,
    );
  }));

const posts = (p) =>
  p.evaluate(
    () => window.fixture.calls.filter((c) => c.method === "POST").length,
  );
async function confirm(p) {
  await p.getByLabel("Lý do", { exact: true }).fill("Retire unused structure");
  await p.getByRole("button", { name: "Xem xét", exact: true }).click();
  await p.getByRole("button", { name: "Confirm deactivation" }).click();
}
for (const kind of ["floor", "area"])
  for (const fault of ["503", "network", "unreadable"])
    for (const transition of ["revalidate", "provider", "remount"])
      test(`${kind} unknown ${fault} survives ${transition} without preview reconciliation`, () =>
        run(
          {
            kind,
            enabled: true,
            status: 503,
            network: fault === "network",
            unreadable: fault === "unreadable",
          },
          async (p) => {
            await confirm(p);
            await p.getByText(/Outcome unknown/).waitFor();
            const reads = await p.evaluate(
              () =>
                window.fixture.calls.filter((c) =>
                  c.url.endsWith("/deactivation-dependencies"),
                ).length,
            );
            assert.equal(reads, 1);
            await p.evaluate(() => {
              window.fixture.scenario.etag = "B";
            });
            if (transition === "revalidate")
              await p.evaluate(() => window.fixture.replace());
            else if (transition === "provider")
              await p.evaluate(() => window.fixture.provider("7"));
            else {
              await p.evaluate(() => window.fixture.mount(false));
              await p.evaluate(() => window.fixture.mount(true));
            }
            await p.getByText(/Outcome unknown/).waitFor();
            assert.equal(
              await p.getByLabel("Lý do", { exact: true }).count(),
              0,
            );
            assert.equal(
              await p
                .getByRole("button", { name: "Refresh record" })
                .isDisabled(),
              true,
            );
            assert.equal(await posts(p), 1);
            assert.equal(
              await p.evaluate(
                () =>
                  window.fixture.calls.filter((c) =>
                    c.url.endsWith("/deactivation-dependencies"),
                  ).length,
              ),
              reads,
            );
          },
        ));

for (const transition of ["revalidate", "provider", "remount"])
  test(`pending POST survives ${transition} and late response cannot restore editor`, () =>
    run({ enabled: true, waitPost: true }, async (p) => {
      await confirm(p);
      await p.waitForFunction(() =>
        window.fixture.calls.some((c) => c.method === "POST"),
      );
      if (transition === "revalidate")
        await p.evaluate(() => window.fixture.replace());
      else if (transition === "provider")
        await p.evaluate(() => window.fixture.provider("7"));
      else {
        await p.evaluate(() => window.fixture.mount(false));
        await p.evaluate(() => window.fixture.mount(true));
      }
      await p.getByText("Đang chờ vô hiệu hóa. Không gửi lại.").waitFor();
      assert.equal(await p.getByLabel("Lý do", { exact: true }).count(), 0);
      await p.evaluate(() => window.fixture.finish());
      await p.getByText(/Outcome unknown/).waitFor();
      assert.equal(await posts(p), 1);
    }));

test("reservation isolates branch, floor/area kind, record ID and principal", () =>
  run({ enabled: true, status: 503 }, async (p) => {
    await confirm(p);
    await p.getByText(/Outcome unknown/).waitFor();
    for (const target of [
      [43, "floor", 11],
      [42, "area", 11],
      [42, "floor", 12],
    ]) {
      await p.evaluate((t) => window.fixture.target(...t), target);
      await confirm(p);
      await p.getByText(/Outcome unknown/).waitFor();
      await p.evaluate(() => window.fixture.target(42, "floor", 11));
      await p.getByText(/Outcome unknown/).waitFor();
      assert.equal(await p.getByLabel("Lý do", { exact: true }).count(), 0);
    }
    await p.evaluate(() => window.fixture.provider("8"));
    await confirm(p);
    await p.getByText(/Outcome unknown/).waitFor();
    assert.equal(await posts(p), 5);
    await p.evaluate(() => window.fixture.provider("7"));
    await p.getByText(/Outcome unknown/).waitFor();
    assert.equal(await posts(p), 5);
    assert.equal(await p.getByLabel("Lý do", { exact: true }).count(), 0);
  }));

test("old principal late 401 cannot modify replacement editor or start recovery", () =>
  run({ enabled: true, waitPost: true, lateStatus: 401 }, async (p) => {
    await confirm(p);
    await p.waitForFunction(() =>
      window.fixture.calls.some((c) => c.method === "POST"),
    );
    await p.evaluate(() => window.fixture.provider("8"));
    await p.getByLabel("Lý do", { exact: true }).fill("New principal reason");
    const sessions = await p.evaluate(
      () =>
        window.fixture.calls.filter((c) => c.url === "/api/auth/session")
          .length,
    );
    await p.evaluate(() => window.fixture.finish());
    // Return to the original principal to synchronize settlement, then check the new one.
    await p.evaluate(() => window.fixture.provider("7"));
    await p.getByText(/Outcome unknown/).waitFor();
    await p.evaluate(() => window.fixture.provider("8"));
    await p.getByLabel("Lý do", { exact: true }).waitFor();
    assert.equal(await p.getByRole("alert").count(), 0);
    assert.equal(
      await p.evaluate(
        () =>
          window.fixture.calls.filter((c) => c.url === "/api/auth/session")
            .length,
      ),
      sessions + 2,
    );
    assert.equal(await posts(p), 1);
  }));

test("CSRF reservation across remount releases only after proven non-dispatch", () =>
  run({ enabled: true, waitCsrf: true, status: 412 }, async (p) => {
    await confirm(p);
    await p.waitForFunction(() =>
      window.fixture.calls.some((c) => c.url === "/api/auth/csrf"),
    );
    await p.evaluate(() => window.fixture.mount(false));
    await p.evaluate(() => window.fixture.mount(true));
    await p.getByText("Đang chờ vô hiệu hóa. Không gửi lại.").waitFor();
    await p.evaluate(() => window.fixture.finish());
    await p.getByLabel("Lý do", { exact: true }).waitFor();
    assert.equal(await posts(p), 0);
    await p.evaluate(() => {
      window.fixture.scenario.waitCsrf = false;
    });
    await confirm(p);
    await p.getByRole("alert").waitFor();
    assert.match(await p.getByRole("alert").innerText(), /Version changed/);
    assert.equal(await posts(p), 1);
  }));

for (const status of [401, 403, 412])
  test(`definitive ${status} retains response semantics without automatic retry`, () =>
    run({ enabled: true, status }, async (p) => {
      await confirm(p);
      await p.getByRole("alert").waitFor();
      assert.match(
        await p.getByRole("alert").innerText(),
        status === 401
          ? /Phiên đăng nhập không hợp lệ/
          : status === 403
            ? /Máy chủ từ chối truy cập\. Màn hình này không tự cấp quyền\./
            : /Version changed/,
      );
      await p.evaluate(() => window.fixture.replace());
      await p.getByLabel("Lý do", { exact: true }).waitFor();
      assert.equal(await posts(p), 1);
    }));
