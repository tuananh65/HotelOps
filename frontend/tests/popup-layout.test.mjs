import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { existsSync } from "node:fs";
import { build } from "esbuild";
import { chromium } from "@playwright/test";
const bundle = await build({
  stdin: {
    resolveDir: process.cwd(),
    loader: "tsx",
    contents: `
import React,{useState} from 'react';import {createRoot} from 'react-dom/client';import {BrowserRouter} from 'react-router-dom';
import {AppModal,AppDrawer,AppButton} from './src/components/design-system';
import {ActionLink,ActionLinks} from './src/components/ActionLink';
import './src/styles.css';import './src/styles/design-system.css';
function App(){const [kind,setKind]=useState('');const Modal=kind==='drawer'?AppDrawer:AppModal;return <main className="cacao-content">
<ActionLinks><ActionLink variant="primary" to="#execution">Mở thực hiện công việc</ActionLink><ActionLink to="#progress">Cập nhật tiến độ</ActionLink></ActionLinks>
<button onClick={()=>setKind('modal')}>Mở form</button><button onClick={()=>setKind('drawer')}>Mở chi tiết</button>
<Modal open={!!kind} title="Chi tiết công việc" onClose={()=>setKind('')} size="large" footer={<AppButton onClick={()=>setKind('')}>Hoàn tất xem</AppButton>}>
{Array.from({length:40},(_,i)=><p key={i}>Thông tin công việc {i} – Nội dung dài cần cuộn bên trong hộp thoại.</p>)}
<div style={{width:1800}}>Bảng nội dung rộng</div></Modal></main>};
createRoot(document.getElementById('root')).render(<BrowserRouter><App/></BrowserRouter>);`,
  },
  bundle: true,
  write: false,
  outfile: "test.js",
  format: "iife",
  jsx: "automatic",
  define: { "process.env.NODE_ENV": '"test"' },
});
let browser;
before(async () => {
  const edge = "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe";
  browser = await chromium.launch({
    headless: true,
    ...(existsSync(edge) ? { executablePath: edge } : {}),
  });
});
after(async () => browser?.close());
for (const viewport of [
  { width: 1440, height: 900 },
  { width: 390, height: 844 },
  { width: 667, height: 375 },
])
  test(`modal and detail stay centered within ${viewport.width}x${viewport.height}`, async () => {
    const page = await browser.newPage({ viewport });
    const errors = [];
    page.on("pageerror", (e) => errors.push(e.message));
    try {
      await page.route("https://popup.test/**", (r) =>
        r.fulfill({
          contentType: "text/html",
          body: '<html><meta name="viewport" content="width=device-width,initial-scale=1"><div id="root"></div></html>',
        }),
      );
      await page.goto("https://popup.test");
      for (const f of bundle.outputFiles.filter((f) => f.path.endsWith(".css")))
        await page.addStyleTag({ content: f.text });
      await page.addScriptTag({
        content: bundle.outputFiles.find((f) => f.path.endsWith(".js")).text,
      });
      const actions = await page
        .locator(".ds-action-links a")
        .evaluateAll((es) =>
          es.map((e) => {
            const r = e.getBoundingClientRect();
            return {
              x: r.x,
              y: r.y,
              width: r.width,
              height: r.height,
              color: getComputedStyle(e).color,
            };
          }),
        );
      assert.equal(actions.length, 2);
      assert.ok(actions.every((a) => a.height >= 40));
      assert.ok(
        actions[1].y >= actions[0].y + actions[0].height + 8 ||
          actions[1].x >= actions[0].x + actions[0].width + 8,
      );
      assert.equal(actions[0].color, "rgb(255, 255, 255)");
      for (const name of ["Mở form", "Mở chi tiết"]) {
        await page.getByRole("button", { name, exact: true }).click();
        await page.locator("dialog[open]").waitFor();
        const g = await page.locator("dialog[open]").evaluate((d) => {
          const r = d.getBoundingClientRect(),
            b = d.querySelector(".ds-dialog-body"),
            f = d.querySelector("footer").getBoundingClientRect();
          return {
            x: r.x,
            y: r.y,
            w: r.width,
            h: r.height,
            bottom: f.bottom,
            scroll: b.scrollHeight > b.clientHeight,
          };
        });
        assert.ok(
          g.x >= 0 &&
            g.y >= 0 &&
            g.x + g.w <= viewport.width + 1 &&
            g.y + g.h <= viewport.height + 1,
          JSON.stringify(g),
        );
        assert.ok(
          Math.abs(g.x + g.w / 2 - viewport.width / 2) < 2 &&
            Math.abs(g.y + g.h / 2 - viewport.height / 2) < 2,
          JSON.stringify(g),
        );
        assert.ok(g.scroll);
        assert.ok(g.bottom <= viewport.height);
        await page.keyboard.press("Escape");
        assert.equal(await page.locator("dialog[open]").count(), 0);
      }
      assert.deepEqual(errors, []);
    } finally {
      await page.close();
    }
  });
