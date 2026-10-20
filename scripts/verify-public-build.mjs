import { chromium, expect } from '@playwright/test';
import { readFile, writeFile } from 'node:fs/promises';
const origin = process.env.PROTOTYPE_PREVIEW_ORIGIN || 'http://127.0.0.1:4173';
const catalog = JSON.parse(await readFile('src/catalog.json','utf8'));
const browser = await chromium.launch({executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe'});
try {
 const source = await browser.newContext({viewport:{width:1440,height:1000},permissions:['clipboard-read','clipboard-write']});
 const sourcePage = await source.newPage();
 await sourcePage.goto(origin);
 await expect(sourcePage).toHaveURL(/\/dev\/screens$/);
 const links = await sourcePage.locator('a[data-screen-id]').evaluateAll(nodes=>nodes.map(node=>({id:Number(node.getAttribute('data-screen-id')),href:node.getAttribute('href')})));
 expect(links).toHaveLength(80);
 await sourcePage.screenshot({path:'docs/verification/public-directory.png'});
 await sourcePage.locator('.screen-card').first().getByRole('button',{name:'Copy link',exact:true}).click();
 expect(await sourcePage.evaluate(()=>navigator.clipboard.readText())).toBe(origin+'/login');
 const reviewer = await browser.newContext();
 const page = await reviewer.newPage();
 const errors=[];
 page.on('pageerror',error=>errors.push(error.message));
 for(const link of links){
   await page.goto(new URL(link.href,origin).href);
   await expect(page.locator(link.id<=3?'.auth-panel h1':'.page-header h1')).toHaveText(catalog.screens[link.id-1].name);
   await expect(page.locator('.table-section .empty')).toHaveCount(0);
 }
 await page.goto(origin+'/rooms');
 await page.getByLabel('Branch context').selectOption('Cao Thang');
 await expect(page.locator('.summary-grid .metric').first()).not.toHaveText('');
 const directRoom=links.find(link=>link.id===20);
 await page.goto(origin+directRoom.href);
 await page.reload();
 await expect(page.locator('.page-header h1')).toHaveText('Room Detail');
 await page.goto(origin+'/dev/design-system');
 await page.reload();
 await expect(page.locator('.swatch')).toHaveCount(31);
 expect(errors).toEqual([]);
 const result={builtScreens:80,crossBrowserRoutes:80,copyLink:true,rootDirectory:true,directRefresh:true,designSystem:true,pageErrors:errors,previewOrigin:origin,publicDeploymentUrl:null};
 await writeFile('docs/verification/production-preview.json',JSON.stringify(result,null,2));
 console.log(JSON.stringify(result));
} finally { await browser.close(); }
