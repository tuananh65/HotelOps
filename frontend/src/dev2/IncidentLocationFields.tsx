import { SearchableSelect } from "../components/SearchableSelect";
import { useEffect, useId, useState } from "react";
import { useAuth } from "../auth/AuthProvider";
import { createHttpClient, isObject } from "../auth/http";
import type { Values } from "./contracts";

type Choice = { id: number; name: string; areaId?: number | null; roomId?: number | null; code?: string };
type Locations = { rooms: Choice[]; areas: Choice[]; assets: Choice[] };
const request = createHttpClient();
function decode(raw: unknown): Locations | undefined {
  if (!isObject(raw) || ![raw.rooms, raw.areas, raw.assets].every(list => Array.isArray(list)
    && list.every(v => isObject(v) && Number.isSafeInteger(v.id) && Number(v.id) > 0 && typeof v.name === "string"))) return;
  return raw as unknown as Locations;
}

export function IncidentLocationFields({ values, onChange }: { values: Values; onChange: (values: Values) => void }) {
  const { state, actions } = useAuth();
  const owner = state.status === "AUTHENTICATED" && !state.pending ? state.authorityGeneration : null;
  const branch = String(values.branchId ?? "");
  const key = `${owner}:${branch}`;
  const [loaded, setLoaded] = useState<{ key: string; data?: Locations }>();
  const [revision, setRevision] = useState(0);
  const [typed, setTyped] = useState<{ key: string; text: string }>();
  const listId = useId();
  useEffect(() => {
    if (!owner || !/^[1-9]\d*$/.test(branch)) return;
    const abort = new AbortController();
    setLoaded(undefined);
    void request({ method: "GET", url: `/api/incidents/creation-branches/${branch}/locations`, expectedStatus: 200, decode, signal: abort.signal }).then(result => {
      if (abort.signal.aborted) return;
      setLoaded({ key, data: result.ok ? result.data : undefined });
      if (!result.ok && result.error.kind === "http" && result.error.status === 401) void actions.revalidate();
    });
    return () => abort.abort();
  }, [owner, branch, key, revision, actions]);
  const current = loaded?.key === key ? loaded : undefined;
  const data = current?.data;
  const room = data?.rooms.find(r => String(r.id) === String(values.roomId));
  const area = data?.areas.find(a => String(a.id) === String(room?.areaId ?? values.areaId));
  const text = room?.name ?? (typed?.key === key ? typed.text : "");
  const matches = data?.rooms.filter(r => r.name.toLocaleLowerCase().startsWith(text.trim().toLocaleLowerCase())) ?? [];
  const assets = data?.assets.filter(a => room ? a.roomId === room.id : area ? a.areaId === area.id : false) ?? [];
  const asset = assets.find(a => String(a.id) === String(values.assetId));
  return <section aria-label="Vị trí sự cố">
    {!data && <p role="status">{current ? "Không tải được vị trí. Vui lòng thử lại." : "Đang tải phòng và tài sản…"}</p>}
    {current && !data && <button type="button" onClick={() => setRevision(v => v + 1)}>Tải lại vị trí</button>}
    <div className="dev2-fields">
      <label>Số phòng<input name="roomNumber" list={listId} autoComplete="off" placeholder="Ví dụ: gõ 2 để tìm phòng 203"
        value={text} required={!area} onChange={e => {
          const text = e.target.value;
          setTyped({ key, text });
          const chosen = data?.rooms.find(r => r.name === text);
          onChange({ ...values, roomId: chosen ? String(chosen.id) : "", areaId: "", assetId: "" });
        }} ref={input => input?.setCustomValidity(!data ? "Chờ tải danh sách vị trí." : text && !room || values.roomId && !room ? "Vui lòng chọn phòng trong danh sách." : "")} />
        <datalist id={listId}>{matches.map(r => <option key={r.id} value={r.name} />)}</datalist>
        {data && text && !matches.length && <small>Không có phòng bắt đầu bằng “{text}”.</small>}
      </label>
      <label>Khu vực {room ? "của phòng" : "chung"}<SearchableSelect name="areaId" disabled={!!room} value={area ? String(area.id) : ""}
        required={!room && !text} onChange={e => { setTyped({ key, text: "" }); onChange({ ...values, roomId: "", areaId: e.target.value, assetId: "" }); }}>
        <option value="">{room ? "Phòng không gắn khu vực" : "Chọn sảnh, hành lang hoặc khu vực khác"}</option>
        {data?.areas.map(a => <option key={a.id} value={a.id}>{a.name}</option>)}
      </SearchableSelect></label>
      <label>Tài sản liên quan (không bắt buộc)<SearchableSelect name="assetId" value={asset ? String(asset.id) : ""}
        required={!!values.assetId && !asset} disabled={!room && !area}
        onChange={e => onChange({ ...values, assetId: e.target.value })}>
        <option value="">Không chọn tài sản</option>
        {assets.map(a => <option key={a.id} value={a.id}>{a.name}{a.code ? ` (${a.code})` : ""}</option>)}
      </SearchableSelect></label>
    </div>
  </section>;
}
