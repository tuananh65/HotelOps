import {useEffect,useState} from "react";
import {useAuth} from "../auth/AuthProvider";
import type {AuthorityGeneration} from "../auth/authStore";
import {InboxFailure,readInbox} from "./inboxApi";

// Rendered only alongside the server-authorized Notifications navigation link.
export function NotificationUnreadBadge(){
  const {state,actions}=useAuth();
  const owner=state.status==="AUTHENTICATED"&&state.pending===null&&!state.session?.mustChangePassword?state.authorityGeneration:null;
  const [result,setResult]=useState<{owner:AuthorityGeneration;count:number}|null>(null);
  useEffect(()=>{
    if(!owner)return;
    let controller:AbortController|null=null;let generation=0;
    const refresh=async()=>{
      controller?.abort();controller=new AbortController();const signal=controller.signal;const revision=++generation;
      setResult(null);
      try{const inbox=await readInbox("page=1&pageSize=1",signal);if(!signal.aborted&&revision===generation)setResult({owner,count:inbox.unreadCount});}
      catch(error){if(!signal.aborted&&error instanceof InboxFailure&&error.status===401)actions.invalidateSession(owner);}
    };
    const onFocus=()=>{void refresh();};void refresh();window.addEventListener("focus",onFocus);
    const timer=window.setInterval(onFocus,30000);
    return()=>{++generation;controller?.abort();window.clearInterval(timer);window.removeEventListener("focus",onFocus);};
  },[owner,actions]);
  if(!owner||result?.owner!==owner)return null;
  return <span role="status" aria-label={`${result.count} unread notifications`} style={{display:"inline-block",padding:"0.1em 0.45em",border:"1px solid currentColor",borderRadius:"1em",marginLeft:"0.25em"}}>{result.count}</span>;
}
