import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { build } from "esbuild";
import { chromium } from "@playwright/test";
const bundle = await build({
  stdin: {
    resolveDir: process.cwd(),
    loader: "tsx",
    contents: `import React,{useState}from'react';import{createRoot}from'react-dom/client';import{BrowserRouter}from'react-router-dom';import{SearchableSelect}from'./src/components/SearchableSelect';import{StatusBadge}from'./src/components/design-system';import{DataView}from'./src/dev2/DataView';import{userMessage}from'./src/i18n/userMessage';import'./src/styles/design-system.css';function App(){const[v,set]=useState('laundry');return <BrowserRouter><label>Bộ phận<SearchableSelect aria-label="Bộ phận" {...(location.search ? {} : {value:v})} onChange={e=>set(e.target.value)}><option value="laundry">Giặt là</option><option value="security">An ninh</option><option value="hk">Buồng phòng</option></SearchableSelect></label><output>{v}</output>{['Bản nháp','Chờ tiếp nhận','Đang thực hiện','Đã duyệt','Đã hoàn tất','Đã đóng'].map(s=><StatusBadge key={s}>{s}</StatusBadge>)}<p role="status">{userMessage('MSG09: Đã báo sự cố.')}</p><DataView data={{incidentId:15,branchId:1,branchName:'Bùi Viện',roomId:4,roomNumber:'203',reportedBy:6,reportedByName:'Đặng Thị Lan',rowVersion:'secret-version',canAssign:false,events:[{eventId:19,incidentId:15,actorId:6,actorName:'Đặng Thị Lan',eventType:'CREATED',toStatus:'OPEN'}]}}/></BrowserRouter>}createRoot(document.getElementById('root')).render(<App/>);`,
  },
  bundle: true,
  write: false,
  outfile: "ui.js",
  format: "iife",
  jsx: "automatic",
  define: { "process.env.NODE_ENV": '"test"' },
});
let browser;
before(
  async () =>
    (browser = await chromium.launch({
      executablePath:
        "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
      headless: true,
    })),
);
after(async () => browser?.close());
async function run(check, suffix = "") {
  const p = await browser.newPage();
  try {
    await p.route("http://ui.test/**", (r) =>
      r.fulfill({ contentType: "text/html", body: '<div id="root"></div>' }),
    );
    await p.goto("http://ui.test" + suffix);
    for (const file of bundle.outputFiles) {
      if (file.path.endsWith(".css"))
        await p.addStyleTag({ content: file.text });
      else await p.addScriptTag({ content: file.text });
    }
    await check(p);
  } finally {
    await p.close();
  }
}
test("select supports accent-insensitive typing, native selection and preserves committed value", () =>
  run(async (p) => {
    await p.getByRole("searchbox").fill("an ninh");
    assert.equal(await p.locator("select option:not([hidden])").count(), 1);
    assert.equal(await p.locator("output").textContent(), "laundry");
    await p.getByRole("combobox").selectOption("security");
    assert.equal(await p.locator("output").textContent(), "security");
    assert.equal(await p.getByRole("searchbox").inputValue(), "");
    await p.getByRole("searchbox").fill("buong");
    assert.equal(
      await p.locator("select option:not([hidden])").textContent(),
      "Buồng phòng",
    );
  }));
test("status colors differ and message codes and secondary IDs stay out of presentation", () =>
  run(async (p) => {
    const colors = await p
      .locator(".ds-badge")
      .evaluateAll((nodes) =>
        nodes.slice(0, 6).map((n) => getComputedStyle(n).backgroundColor),
      );
    assert.equal(new Set(colors).size, 6);
    assert.equal(
      await p.locator("p[role=status]").textContent(),
      "Đã báo sự cố.",
    );
    const body = await p.locator("body").innerText();
    assert.ok(!body.includes("MSG09"));
    assert.ok(!body.includes("secret-version"));
    assert.ok(body.includes("Đặng Thị Lan"));
    assert.deepEqual(await p.locator("thead th").allTextContents(), [
      "EventID",
      "Người thực hiện",
      "Sự kiện",
      "Trạng thái sau",
    ]);
    assert.deepEqual(await p.locator("tbody td").allTextContents(), [
      "19",
      "Đặng Thị Lan",
      "Đã tạo",
      "Đang mở",
    ]);
  }));

test("uncontrolled select preserves implicit first choice and native selection while searching", () => run(async p => {
  assert.equal(await p.getByRole("combobox").inputValue(), "laundry");
  await p.getByRole("searchbox").fill("an ninh");
  assert.equal(await p.getByRole("combobox").inputValue(), "laundry");
  await p.getByRole("combobox").selectOption("security");
  await p.getByRole("searchbox").fill("buong");
  assert.equal(await p.getByRole("combobox").inputValue(), "security");
}, "?uncontrolled"));