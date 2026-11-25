import { useEffect, useState } from 'react';
import { api, type Row } from './api';

/** Read every authorized page before client filtering/sorting; never sort only a visible page. */
export function usePagedRows(path: string | null, revision = 0) {
  const [notificationRevision, refresh] = useState(0);
  useEffect(() => {
    if (!path) return;
    const changed = () => refresh(value => value + 1);
    window.addEventListener('compact:notification-change', changed);
    return () => window.removeEventListener('compact:notification-change', changed);
  }, [path]);
  const [state, set] = useState<{path:string|null;data?:Row[];loading:boolean;error?:unknown}>({path:null,loading:false});
  useEffect(() => {
    const controller = new AbortController();
    if (!path) { set({path,loading:false}); return; }
    set({path,loading:true});
    void (async () => {
      const rows:Row[]=[]; let page=1;
      for (;;) {
        const result = await api(`${path}&page=${page}&pageSize=100`,undefined,'GET',controller.signal);
        rows.push(...result.items.map((row:Row)=>row.fields||row));
        if (rows.length >= result.total || !result.items.length) break;
        page++;
      }
      if (!controller.signal.aborted) set({path,data:rows,loading:false});
    })().catch(error=>{if(!controller.signal.aborted)set({path,error,loading:false});});
    return ()=>controller.abort();
  },[path,revision,notificationRevision]);
  return state.path===path?state:{loading:!!path,data:undefined,error:undefined};
}
