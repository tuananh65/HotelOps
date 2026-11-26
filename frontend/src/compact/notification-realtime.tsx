import { useEffect, useState } from "react";
import { HubConnectionBuilder, HttpTransportType, LogLevel } from "@microsoft/signalr";
import { api, sessionToken } from "./api";
import { AlertPopup } from "./alert-popup";

type Pulse = { eventId: string; unreadCount: number; notificationIds: number[] };

export function useNotificationRealtime(branch: number) {
  const [count, setCount] = useState(0);
  const [notice, setNotice] = useState<{id: string; text: string} | null>(null);
  useEffect(() => {
    let disposed = false, retry: ReturnType<typeof setTimeout> | undefined;
    let subscription: { dispose(): void } | undefined;
    const seen = new Set<number>();
    let initialized = false, lastEvent = "";
    setCount(0); setNotice(null);
    const refreshCount=()=>api<{count:number}>(`notifications/unread${branch?`?branchId=${branch}`:''}`).then(data=>{if(!disposed)setCount(data.count);}).catch(()=>{});
    void refreshCount();
    const countTimer=setInterval(refreshCount,30000);
    window.addEventListener('compact:notification-read',refreshCount);
    const connection = new HubConnectionBuilder()
      .withUrl("/api/v2/realtime/notifications", {
        accessTokenFactory: sessionToken,
        transport: HttpTransportType.LongPolling,
        withCredentials: false,
      })
      .withAutomaticReconnect([0, 2000, 10000, 30000])
      .configureLogging(LogLevel.None)
      .build();
    const subscribe = () => {
      if (disposed) return;
      subscription?.dispose();
      subscription = connection.stream<Pulse>("Subscribe", branch || null).subscribe({
        next: pulse => {
          if (disposed || pulse.eventId === lastEvent) return;
          lastEvent = pulse.eventId;
          const added = pulse.notificationIds.filter(id => !seen.has(id));
          pulse.notificationIds.forEach(id => seen.add(id));
          setCount(pulse.unreadCount);
          if (initialized && added.length) setNotice({id: pulse.eventId, text: `Bạn có ${added.length} thông báo mới. Mở Thông báo để xem chi tiết.`});
          if (seen.size > 2000) { seen.clear(); pulse.notificationIds.forEach(id => seen.add(id)); }
          initialized = true;
          window.dispatchEvent(new Event("compact:notification-change"));
        },
        error: () => { if (!disposed) void connection.stop(); },
        complete: () => {},
      });
    };
    async function start() {
      clearTimeout(retry); retry = undefined;
      if (disposed || !sessionToken()) return;
      try { await connection.start(); if (disposed) await connection.stop(); else subscribe(); }
      catch { scheduleRetry(); }
    }
    function scheduleRetry() {
      if (!disposed && sessionToken() && !retry) retry = setTimeout(start, 15000);
    }
    connection.onreconnected(subscribe);
    connection.onclose(scheduleRetry);
    void start();
    return () => { disposed = true; clearTimeout(retry);clearInterval(countTimer);window.removeEventListener('compact:notification-read',refreshCount); subscription?.dispose(); void connection.stop(); };
  }, [branch]);
  return { count, popup: notice ? <AlertPopup key={notice.id}>{notice.text}</AlertPopup> : null };
}
