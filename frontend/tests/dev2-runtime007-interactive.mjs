// Owner-invoked only. Credentials stay in memory and real UI password inputs; no tracing/storage.
import {chromium} from '@playwright/test';
import {existsSync,readFileSync} from 'node:fs';
import path from 'node:path';
let raw='';for await(const chunk of process.stdin)raw+=chunk;
const input=JSON.parse(raw);raw='';let browser;
try{
  const edge='C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe';
  browser=await chromium.launch({headless:false,...(existsSync(edge)?{executablePath:edge}:{})});
  const origin=new URL(input.frontend);if(!['localhost','127.0.0.1','[::1]'].includes(origin.hostname)||origin.protocol!=='https:')throw Error();
  for(const user of input.users.slice(0,3)){
    const context=await browser.newContext();const page=await context.newPage();
    await page.goto(input.frontend+'/login');
    await page.locator('[name="username"]').fill(user.Username);
    await page.locator('[name="password"]').fill(input.password);
    await page.getByRole('button',{name:/sign in/i}).click();
    await page.waitForURL('**/dashboard');
    await page.goto(input.frontend+'/operations/incidents');
  }
  input.password='';const stop=path.join(input.output,'STOP_REQUEST.json');
  const deadline=Date.now()+2*60*60*1000;
  while(browser.isConnected()&&Date.now()<deadline){
    if(existsSync(stop)){
      const request=JSON.parse(readFileSync(stop,'utf8').replace(/^\uFEFF/,''));
      if(request.marker!==input.marker||request.databaseGuid!=='6e68f686-d7c1-4002-94d9-a58fa73175da')throw Error();
      break;
    }
    await new Promise(resolve=>setTimeout(resolve,1000));
  }
}catch{process.exitCode=1;}finally{input.password='';if(browser)await browser.close();}
