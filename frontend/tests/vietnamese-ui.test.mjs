import { test, before, after } from "node:test";
import assert from "node:assert/strict";
import { existsSync } from "node:fs";
import { build } from "esbuild";
import { chromium } from "@playwright/test";

const bundle = await build({
  stdin: {
    resolveDir: process.cwd(), loader: "tsx",
    contents: `import React, {useState} from "react";
      import {createRoot} from "react-dom/client";
      import {Fields} from "./src/dev2/Fields";
      import {displayCode,fieldLabel,fieldValue} from "./src/i18n/vi";
      import {operations} from "./src/dev2/catalog.generated";
      window.labels={displayCode,fieldLabel,fieldValue};
      window.operations=operations;
      function App(){const [values,setValues]=useState({mode:"FullRemaining",status:"OPEN"});
        return <Fields fields={[{name:"mode",type:"string",optional:false,options:["Partial","FullRemaining"]},
          {name:"status",type:"string",optional:false,options:["OPEN","CLOSED"]}]}
          actor="42" values={values} onChange={v=>{window.submitted=v;setValues(v)}}/>}
      createRoot(document.getElementById("root")).render(<App/>);`,
  }, bundle: true, write: false, format: "iife", platform: "browser", jsx: "automatic",
  define: { "process.env.NODE_ENV": '"test"' },
});
let browser;
before(async () => {
  const executablePath = process.env.HOTELOPS_TEST_BROWSER || [
    "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
    "C:/Program Files/Google/Chrome/Application/chrome.exe",
  ].find(existsSync);
  browser = await chromium.launch({headless:true,...(executablePath?{executablePath}:{})});
});
after(async () => browser?.close());
async function pageFor(run){const page=await browser.newPage();const errors=[];page.on("pageerror",e=>errors.push(e.message));try{
  await page.setContent('<html lang="vi"><body><div id="root"></div></body></html>');
  await page.addScriptTag({content:bundle.outputFiles[0].text});await page.locator('select[name="mode"]').waitFor();
  await run(page);assert.deepEqual(errors,[]);
}finally{await page.close()}}
test("Vietnamese option labels retain canonical API values after selection",async()=>pageFor(async page=>{
  await page.locator('select[name="mode"]').selectOption({label:"Cấp một phần"});
  await page.locator('select[name="status"]').selectOption({label:"Đã đóng"});
  assert.deepEqual(await page.evaluate(()=>window.submitted),{mode:"Partial",status:"CLOSED"});
  assert.match(await page.locator("body").innerText(),/Trạng thái/);
}));
test("Display translation preserves user content, unknown codes and permission identifiers",async()=>pageFor(async page=>{
  assert.deepEqual(await page.evaluate(()=>{
    const l=window.labels;return [l.displayCode("MAINTENANCE_LEAD"),l.displayCode("INCIDENT_MANAGE"),
      l.fieldValue("description","OPEN"),l.fieldValue("incidentTitle","Test incident"),
      l.fieldValue("status","OPEN"),l.fieldValue("enabled",true),l.fieldValue("enabled",false)];
  }),["Trưởng bảo trì","INCIDENT_MANAGE","OPEN","Test incident","Đang mở","Có","Không"]);
}));
test("Every generated form field and enumerated option has a Vietnamese label",async()=>pageFor(async page=>{
  const missing=await page.evaluate(()=>{
    const missing=[];function visit(fields){for(const f of fields){
      const fallback=f.name.replace(/([a-z])([A-Z])/g,"$1 $2").replace(/^./,x=>x.toUpperCase());
      if(window.labels.fieldLabel(f.name)===fallback)missing.push(f.name);
      for(const option of f.options??[])if(window.labels.displayCode(option)===option)missing.push(option);
      if(f.fields)visit(f.fields);
    }}window.operations.forEach(op=>visit(op.fields));return [...new Set(missing)];
  });assert.deepEqual(missing,[]);
}));
