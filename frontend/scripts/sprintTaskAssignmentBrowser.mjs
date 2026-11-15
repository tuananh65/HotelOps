import { chromium } from '@playwright/test';
let input='';for await(const chunk of process.stdin)input+=chunk;const f=JSON.parse(input);input='';
let browser,stage='launch';const checks=[],requests=[];
try{
 browser=await chromium.launch({executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',headless:true});
 const context=await browser.newContext();const page=await context.newPage();page.setDefaultTimeout(15000);
 page.on('response',r=>{const path=new URL(r.url()).pathname;if(/operational-tasks/.test(path))requests.push({path,method:r.request().method(),status:r.status()});});
 async function login(user){await page.goto('https://localhost:7142/operational-tasks');await page.getByLabel('Username or email').fill(user);await page.getByLabel('Password',{exact:true}).fill(f.password);await page.getByRole('button',{name:'Sign in',exact:true}).click();await page.waitForURL('**/operational-tasks');}
 async function logout(){await page.goto('https://localhost:7142/profile');await page.getByRole('button',{name:'Logout',exact:true}).click();await page.waitForURL('**/login');}
 async function detail(){await page.goto(`https://localhost:7142/operational-tasks?branchId=${f.branch}&taskId=${f.task}`);await page.getByRole('heading',{name:'Synthetic assignment demo',exact:true}).waitFor();}
 stage='manager opens pending assignment';await login(f.manager);await detail();const panel=page.getByRole('region',{name:'Task assignment'});await panel.getByText(/Workflow: PENDING_ASSIGNMENT/).waitFor();checks.push(stage);
 stage='eligible executor and scoped workload';await panel.getByLabel('Executor',{exact:true}).selectOption(String(f.assignee));await panel.getByText(/Logged time today/).waitFor();checks.push(stage);
 stage='assign commits and refreshes current owner';await panel.getByRole('button',{name:'Assign task',exact:true}).click();await page.getByText('MSG08: Task assigned. The new executor must accept before starting.',{exact:true}).waitFor();await panel.getByText(new RegExp(`current executor: ${f.assignee}`)).waitFor();checks.push(stage);
 stage='reload preserves assignment';await page.reload();await panel.getByText(new RegExp(`current executor: ${f.assignee}`)).waitFor();await logout();checks.push(stage);
 stage='executor accepts assigned task';await login(f.executor);await detail();await page.getByText('Task execution state: ASSIGNED',{exact:true}).waitFor();await panel.waitFor({state:'hidden'});await page.getByRole('button',{name:'Accept task',exact:true}).click();await page.getByText('MSG08: Task accepted.',{exact:true}).waitFor();await page.getByText('Task execution state: ACCEPTED',{exact:true}).waitFor();checks.push(stage);
 stage='accepted state survives reload';await page.reload();await page.getByText('Task execution state: ACCEPTED',{exact:true}).waitFor();await logout();checks.push(stage);
 stage='manager reassigns Accepted task with reason';await login(f.manager);await detail();await panel.getByLabel('Executor',{exact:true}).selectOption(String(f.nextAssignee));await panel.getByLabel('Reassignment reason',{exact:true}).fill('Synthetic browser coverage transfer');await panel.getByRole('button',{name:'Reassign task',exact:true}).click();await page.getByText('MSG08: Task assigned. The new executor must accept before starting.',{exact:true}).waitFor();await panel.getByText(new RegExp(`current executor: ${f.nextAssignee}`)).waitFor();await logout();checks.push(stage);
 stage='new executor accepts again';await login(f.nextUsername);await detail();await page.getByText('Task execution state: ASSIGNED',{exact:true}).waitFor();await page.getByRole('button',{name:'Accept task',exact:true}).click();await page.getByText('Task execution state: ACCEPTED',{exact:true}).waitFor();await logout();checks.push(stage);
 if(requests.filter(r=>r.path.endsWith('/assignment')&&r.method==='POST'&&r.status===200).length!==2)throw Error('Expected exactly two committed assignments');
 f.password='';console.log(JSON.stringify({success:true,stage,checks,requests}));
}catch(e){console.log(JSON.stringify({success:false,stage,checks,requests,error:String(e.message).slice(0,600)}));process.exitCode=1;}
finally{await browser?.close();}
