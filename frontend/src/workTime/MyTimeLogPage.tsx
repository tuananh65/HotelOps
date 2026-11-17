import {CorrectionRequestForm} from "./CorrectionRequestForm";
import {useEffect,useState} from "react";
import {Link} from "react-router-dom";
import {useAuth} from "../auth/AuthProvider";
import {readTimeLog,TimeLogFailure,type TimeLog} from "./timeLogApi";

const duration=(seconds:number)=>`${Math.floor(seconds/3600)}h ${Math.floor(seconds%3600/60)}m ${seconds%60}s`;
const hotelDate=()=>new Intl.DateTimeFormat("en-CA",{timeZone:"Asia/Ho_Chi_Minh",year:"numeric",month:"2-digit",day:"2-digit"}).format(new Date());
const stamp=(value:string)=>new Intl.DateTimeFormat("en-GB",{timeZone:"Asia/Ho_Chi_Minh",dateStyle:"short",timeStyle:"medium"}).format(new Date(value));
export function MyTimeLogPage(){
  const {state,actions}=useAuth();
  const owner=state.status==="AUTHENTICATED"&&state.pending===null&&!state.session?.mustChangePassword?state.authorityGeneration:null;
  const [message,setMessage]=useState("");
  const [branch,setBranch]=useState("");const [date,setDate]=useState(hotelDate);
  const [query,setQuery]=useState<{branch:number;date:string;page:number;revision:number}|null>(null);
  const [result,setResult]=useState<{owner:typeof owner;key:string;data?:TimeLog;error?:string}|null>(null);
  const key=JSON.stringify(query);
  useEffect(()=>{
    setResult(null);if(!owner||!query)return;
    const controller=new AbortController();
    void readTimeLog(query.branch,query.date,query.page,controller.signal).then(data=>{
      if(!controller.signal.aborted)setResult({owner,key,data});
    }).catch((e:unknown)=>{
      if(controller.signal.aborted)return;
      if(e instanceof TimeLogFailure&&e.status===401)actions.invalidateSession(owner);
      setResult({owner,key,error:e instanceof TimeLogFailure&&e.status===403?"You no longer have access to this branch's time log.":"Time log unavailable. Check the branch and date, then retry."});
    });return()=>controller.abort();
  },[owner,key,actions]);
  if(!owner)return null;
  const visible=result?.owner===owner&&result.key===key?result:null;const data=visible?.data;
  return <main><Link to="/">Home</Link><h1>My Time Log</h1><Link to="/time-corrections">Correction requests and decisions</Link>
    <p>Times use hotel time (UTC+7). Running time is provisional as of the last refresh.</p>
    <form onSubmit={e=>{e.preventDefault();const id=Number(branch);if(!Number.isSafeInteger(id)||id<=0||!date)return;setQuery({branch:id,date,page:1,revision:(query?.revision??0)+1});}}>
      <label>Branch ID<input required type="number" min="1" step="1" value={branch} onChange={e=>{setBranch(e.target.value);setQuery(null);}}/></label>
      <label>Work date<input required type="date" min="1900-01-01" value={date} onChange={e=>{setDate(e.target.value);setQuery(null);}}/></label>
      <button type="submit">Load time log</button>
    </form>
    {message&&<p role="status">{message}</p>}
    {query&&!visible&&<p role="status">Loading time log…</p>}
    {visible?.error&&<p role="alert">{visible.error}</p>}
    {data&&<section aria-label="Own daily time log"><p>Snapshot: {stamp(data.asOf)}</p>
      <dl><dt>Recorded work time</dt><dd>{duration(data.workClosedSeconds)}</dd><dt>Running work time</dt><dd>{duration(data.workProvisionalSeconds)}</dd>
        <dt>Completed workday time</dt><dd>{duration(data.workdayClosedSeconds)}</dd><dt>Running workday time</dt><dd>{duration(data.workdayProvisionalSeconds)}</dd></dl>
      {data.total===0?<p>No work intervals for this day.</p>:<table><caption>My work intervals</caption><thead><tr><th>Kind</th><th>Work item</th><th>Start</th><th>End</th><th>Recorded duration</th><th>Provisional duration</th><th>Correction</th></tr></thead>
        <tbody>{data.items.map(i=><tr key={i.intervalId}><td>{i.kind}</td><td>{i.workItemId}</td><td>{stamp(i.start)}</td><td>{i.end?stamp(i.end):"Running"}</td><td>{duration(i.closedSeconds)}</td><td>{duration(i.provisionalSeconds)}</td><td><CorrectionRequestForm key={`${owner}:${data.branchId}:${i.intervalId}:${i.version}:${query?.revision}`} branch={data.branchId} interval={i} owner={owner} onSaved={()=>{setMessage("MSG09: Correction request saved. Review its status in Correction requests and decisions.");setQuery(q=>q&&({...q,revision:q.revision+1}));}}/></td></tr>)}</tbody></table>}
      <p>Page {data.page} · {data.total} intervals</p>
      <button disabled={data.page===1} onClick={()=>setQuery(q=>q&&({...q,page:q.page-1}))}>Previous</button>
      <button disabled={data.page*data.pageSize>=data.total} onClick={()=>setQuery(q=>q&&({...q,page:q.page+1}))}>Next</button>
    </section>}
  </main>;
}
