import { chromium } from '@playwright/test';
let input='';for await(const chunk of process.stdin)input+=chunk;const f=JSON.parse(input);input='';
let browser,page,stage='launch',seriesId=null,taskId=null;const checks=[],requests=[];
try{
 browser=await chromium.launch({executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',headless:true});
 const context=await browser.newContext();page=await context.newPage();page.setDefaultTimeout(15000);
 page.on('response',r=>{const path=new URL(r.url()).pathname;if(path.includes('/operational-task'))requests.push({path,method:r.request().method(),status:r.status()});});
 async function login(user){await page.goto('https://localhost:7142/operational-tasks');await page.getByLabel('Username or email').fill(user);await page.getByLabel('Password',{exact:true}).fill(f.password);await page.getByRole('button',{name:'Sign in',exact:true}).click();await page.waitForURL('**/operational-tasks');}
 async function logout(){await page.goto('https://localhost:7142/profile');await page.getByRole('button',{name:'Logout',exact:true}).click();await page.waitForURL('**/login');}
 const title='Public browser recurring task';
 stage='manager creates recurring series from public form';await login(f.manager);
 await page.goto(`https://localhost:7142/operational-tasks?branchId=${f.branch}`);
 const form=page.getByRole('region',{name:'Create operational task'});
 await form.getByLabel('Department',{exact:true}).selectOption(String(f.department));await form.getByLabel('Work category',{exact:true}).selectOption(String(f.category));
 await form.getByLabel('Task title',{exact:true}).fill(title);await form.getByLabel('Task description',{exact:true}).fill('First review step\nSecond review step');
 await form.getByLabel('Expected outcome',{exact:true}).fill('Daily result reviewed\nResult recorded');await form.getByLabel('Criterion 1',{exact:true}).fill('Recurring acceptance criterion');
 await form.getByLabel('Priority',{exact:true}).selectOption(String(f.priority));
 const due=new Date(Date.parse(f.due)+7*3600000).toISOString().slice(0,16);
 await form.getByLabel('Deadline',{exact:true}).fill(due);await form.getByLabel('Repeat task',{exact:true}).check();
 await form.getByLabel('Default executor',{exact:true}).selectOption(String(f.assignee));
 if(await form.getByRole('button',{name:'Save and assign',exact:true}).isEnabled())throw Error('Series permits unrelated assignment action');
 const createdResponse=page.waitForResponse(r=>new URL(r.url()).pathname===`/api/branches/${f.branch}/operational-task-series`&&r.request().method()==='POST');
 await form.getByRole('button',{name:'Save recurring series',exact:true}).click();const response=await createdResponse;if(response.status()!==201)throw Error('Create series failed '+response.status());
 const created=(await response.json()).series;seriesId=created.seriesId;if(created.occurrenceCount!==1)throw Error('Initial occurrence not generated');checks.push(stage);
 const series=page.getByRole('region',{name:'Recurring task series'});
 stage='series and generated task survive reload';await series.getByRole('button',{name:`Stop series: ${title}`,exact:true}).waitFor();await page.reload();
 await series.getByRole('button',{name:`Stop series: ${title}`,exact:true}).waitFor();
 await page.getByRole('button',{name:new RegExp(`^TASK-.*: ${title}$`)}).click();taskId=Number(new URL(page.url()).searchParams.get('taskId'));
 await page.getByRole('heading',{name:title,exact:true}).waitFor();await page.getByRole('region',{name:'Task assignment'}).getByText(new RegExp(`current executor: ${f.assignee}`)).waitFor();checks.push(stage);await logout();
 stage='default executor can open generated task without manager series controls';await login(f.executor);await page.goto(`https://localhost:7142/operational-tasks?branchId=${f.branch}&taskId=${taskId}`);
 await page.getByText('Task execution state: ASSIGNED',{exact:true}).waitFor();await page.getByRole('button',{name:'Accept task',exact:true}).waitFor();
 if(await page.getByRole('button',{name:'Save task plan',exact:true}).count())throw Error('Executor sees manager plan controls');checks.push(stage);await logout();
 stage='manager confirms Stop Series from its generated task plan';await login(f.manager);await page.goto(`https://localhost:7142/operational-tasks?branchId=${f.branch}&taskId=${taskId}`);
 await series.getByRole('button',{name:`Stop series: ${title}`,exact:true}).click();
 if(await series.getByRole('button',{name:'Confirm stop series',exact:true}).isEnabled())throw Error('Stop lacks confirmation');
 await series.getByLabel('Stop series reason',{exact:true}).fill('Recurring routine retired');await series.getByLabel('Confirm stop future occurrences',{exact:true}).check();
 const stoppedResponse=page.waitForResponse(r=>new URL(r.url()).pathname===`/api/branches/${f.branch}/operational-task-series/${seriesId}/stop`&&r.request().method()==='POST');
 await series.getByRole('button',{name:'Confirm stop series',exact:true}).click();if((await stoppedResponse).status()!==200)throw Error('Stop failed');
 await series.getByText('MSG08: Series stopped. Already generated tasks remain unchanged.',{exact:true}).waitFor();checks.push(stage);
 stage='inactive series persists while existing assigned task remains usable';await page.reload();
 const row=series.getByRole('listitem').filter({has:page.getByText(title,{exact:true})});await row.getByText(/Inactive/).waitFor();
 if(await row.getByRole('button',{name:`Stop series: ${title}`,exact:true}).count())throw Error('Stopped series still mutable');
 await page.goto(`https://localhost:7142/operational-tasks?branchId=${f.branch}&taskId=${taskId}`);await page.getByRole('heading',{name:title,exact:true}).waitFor();
 await page.getByRole('region',{name:'Task assignment'}).getByText(new RegExp(`current executor: ${f.assignee}`)).waitFor();checks.push(stage);await logout();
 f.password='';console.log(JSON.stringify({success:true,stage,seriesId,taskId,checks,requests}));
}catch(e){console.log(JSON.stringify({success:false,stage,seriesId,taskId,checks,requests,error:String(e.message).slice(0,700),visibleText:await page?.locator('body').innerText().catch(()=>'unavailable')}));process.exitCode=1;}
finally{await browser?.close();}
