export interface TimeLogEntry { intervalId:number; kind:string; workItemId:number; start:string; end:string|null; closedSeconds:number; provisionalSeconds:number; version:string }
export interface TimeLog { branchId:number; date:string; asOf:string; workdayClosedSeconds:number; workdayProvisionalSeconds:number; workClosedSeconds:number; workProvisionalSeconds:number; total:number; page:number; pageSize:number; items:TimeLogEntry[] }
export class TimeLogFailure extends Error { constructor(readonly status:number){super("Nhật ký thời gian chưa khả dụng.");} }
const object=(v:unknown):v is Record<string,unknown>=>!!v&&typeof v==="object"&&!Array.isArray(v);
const count=(v:unknown):v is number=>Number.isSafeInteger(v)&&Number(v)>=0;
const id=(v:unknown)=>count(v)&&v>0;
const timestamp=(v:unknown)=>typeof v==="string"&&/(Z|\+00:00)$/.test(v)&&Number.isFinite(Date.parse(v));
export function decodeTimeLog(v:unknown,branch:number,date:string,page:number):TimeLog {
  if(!object(v)||v.branchId!==branch||v.date!==date||v.page!==page||v.pageSize!==50||!timestamp(v.asOf)
    ||![v.total,v.workdayClosedSeconds,v.workdayProvisionalSeconds,v.workClosedSeconds,v.workProvisionalSeconds].every(count)
    ||!Array.isArray(v.items)||v.items.length>50||v.items.length>Number(v.total)
    ||!v.items.every(i=>object(i)&&id(i.intervalId)&&id(i.workItemId)&&typeof i.kind==="string"&&i.kind.length>0
      &&timestamp(i.start)&&(i.end===null||timestamp(i.end))&&count(i.closedSeconds)&&count(i.provisionalSeconds)
      &&typeof i.version==="string"&&/^[A-Za-z0-9+/]{11}=$/.test(i.version)))throw new Error("Phản hồi nhật ký thời gian không hợp lệ.");
  if(new Set(v.items.map(i=>i.intervalId)).size!==v.items.length)throw new Error("Khoảng thời gian bị trùng.");
  return v as unknown as TimeLog;
}
export async function readTimeLog(branch:number,date:string,page:number,signal:AbortSignal):Promise<TimeLog>{
  if(!id(branch)||!id(page)||!/^\d{4}-\d{2}-\d{2}$/.test(date))throw new Error("Chọn chi nhánh và ngày.");
  const response=await fetch(`/api/branches/${branch}/workdays/own-time-log?date=${encodeURIComponent(date)}&page=${page}&pageSize=50`,
    {credentials:"same-origin",cache:"no-store",redirect:"error",headers:{Accept:"application/json"},signal});
  if(!response.ok)throw new TimeLogFailure(response.status);
  return decodeTimeLog(await response.json(),branch,date,page);
}
