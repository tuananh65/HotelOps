import {chromium} from '@playwright/test';
let input='';for await(const chunk of process.stdin)input+=chunk;
const fixture=JSON.parse(input);input='';
let browser,stage='launch';const checks=[];
try{
 browser=await chromium.launch({executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',headless:true});
 const page=await browser.newPage();page.setDefaultTimeout(15000);
 stage='task manager login';await page.goto('https://localhost:7142/operational-tasks');
 await page.getByLabel('Username or email').fill(fixture.username);await page.getByLabel('Password',{exact:true}).fill(fixture.password);fixture.password='';
 await page.getByRole('button',{name:'Sign in',exact:true}).click();await page.waitForURL('**/operational-tasks');checks.push(stage);
 stage='open task criteria setup';await page.getByLabel('Branch ID').fill(String(fixture.branch));
 await page.getByRole('button',{name:'Load tasks',exact:true}).click();
 await page.getByRole('button',{name:fixture.code+': Synthetic task 0',exact:true}).click();
 await page.getByRole('button',{name:'Set up completion criteria',exact:true}).click();checks.push(stage);
 stage='define required and optional criteria';
 await page.getByLabel('Criterion 1',{exact:true}).fill('Synthetic required criterion');
 await page.getByRole('button',{name:'Add criterion',exact:true}).click();
 await page.getByLabel('Criterion 2',{exact:true}).fill('Synthetic optional criterion');
 await page.getByLabel('Required criterion 2',{exact:true}).uncheck();
 await page.getByLabel('Setup reason',{exact:true}).fill('Synthetic task criteria setup for runtime acceptance');
 await page.getByRole('button',{name:'Initialize criteria',exact:true}).click();
 await page.getByText('Completion criteria initialized.',{exact:true}).waitFor();
 await page.getByLabel('Criterion result: Synthetic required criterion',{exact:true}).waitFor();
 await page.getByLabel('Criterion result: Synthetic optional criterion',{exact:true}).waitFor();checks.push(stage);
 stage='logout';await page.goto('https://localhost:7142/profile');await page.getByRole('button',{name:'Logout',exact:true}).click();await page.waitForURL('**/login');checks.push(stage);
 process.stdout.write(JSON.stringify({success:true,checks}));
}catch(e){process.stdout.write(JSON.stringify({success:false,stage,checks,diagnostic:String(e?.message??'Browser failed').split('\n')[0]}));process.exitCode=1;}
finally{await browser?.close();}
