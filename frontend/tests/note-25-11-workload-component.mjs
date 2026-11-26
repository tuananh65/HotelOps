import {chromium,expect} from '@playwright/test';
import {mkdir,writeFile} from 'node:fs/promises';
import {fileURLToPath} from 'node:url';
const output=fileURLToPath(new URL('../../.artifacts/note-25-11-workload/',import.meta.url));
await mkdir(output,{recursive:true});
const browser=await chromium.launch({channel:'msedge',headless:true});
const results=[];
try{
 const page=await browser.newPage({viewport:{width:900,height:650}});
 await page.goto('http://127.0.0.1:5173/');
 await page.evaluate(async()=>{
  const [{default:React},{default:ReactDOM},{default:StatChart}]=await Promise.all([import('/node_modules/.vite/deps/react.js'),import('/node_modules/.vite/deps/react-dom_client.js'),import('/src/compact/stat-chart.tsx')]);
  document.body.innerHTML='<main id="component-test"></main>';
  ReactDOM.createRoot(document.getElementById('component-test')).render(React.createElement(StatChart,{title:'Workload component fixture (not database)',rows:[{name:'Đang thực hiện',value:4},{name:'Chờ kiểm tra',value:2}],href:'#cleaning'}));
 });
 await expect(page.locator('.chart-body.bars')).toBeVisible();
 await expect(page.locator('.chart-values strong')).toHaveText(['4','2']);
 const widths=await page.locator('.chart-track > span').evaluateAll(nodes=>nodes.map(n=>n.style.width));
 expect(widths).toEqual(['100%','50%']);
 await expect(page.locator('.chart-body.donut')).toHaveCount(0);
 await page.screenshot({path:output+'horizontal-bars.png'});
 results.push({name:'Actual StatChart renders horizontal workload bars with correct proportions',result:'PASS',scope:'Component fixture only; no claim of live database coverage'});
 await writeFile(output+'results.json',JSON.stringify(results,null,2));
 console.log(JSON.stringify(results,null,2));
}finally{await browser.close();}
