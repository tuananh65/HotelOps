import { useEffect, useRef, useState } from "react";
import { useAuth } from "../auth/AuthProvider";

export type AssetRegistrationInput = { assetCode: string; assetName: string; assetTypeId: number; branchId: number; currentDepartmentId: number;
  serialNumber: string | null; brand: string | null; model: string | null; purchaseDate: string | null; warrantyExpiryDate: string | null;
  description: string | null; photos: { name: string; mimeType: string; base64: string }[] };
type Catalog = { branches: { id: number; name: string }[]; departments: { id: number; name: string; branchId: number }[]; categories: { id: number; name: string }[] };
// Principal-bound registration writer; no legacy ActorUserId transport.
export function AssetRegistrationForm({ save, busy }: { save: (input: AssetRegistrationInput, current: () => boolean) => Promise<void>; busy: boolean }) {
  const { state, actions } = useAuth();
  const owner = state.status === "AUTHENTICATED" && !state.pending && !state.session?.mustChangePassword ? state.authorityGeneration : null;
  const live = useRef(owner); live.current = owner;
  const alive = useRef(true);
  const [catalog, setCatalog] = useState<{ owner: typeof owner; data: Catalog } | null>(null);
  const [branch, setBranch] = useState(""), [department, setDepartment] = useState(""), [category, setCategory] = useState("");
  const [fields, setFields] = useState({ assetCode: "", assetName: "", serialNumber: "", brand: "", model: "", purchaseDate: "", warrantyExpiryDate: "", description: "" });
  const [files, setFiles] = useState<File[]>([]), [message, setMessage] = useState(""), [preparing, setPreparing] = useState(false);
  const preparingRef = useRef(false);
  useEffect(() => {
    alive.current = true;
    setCatalog(null); setBranch(""); setDepartment(""); setCategory(""); setFiles([]); setMessage("");
    setFields({ assetCode: "", assetName: "", serialNumber: "", brand: "", model: "", purchaseDate: "", warrantyExpiryDate: "", description: "" });
    if (!owner) return;
    const abort = new AbortController();
    void fetch("/api/asset-registration/catalog", { signal: abort.signal, credentials: "same-origin", cache: "no-store", redirect: "error" })
      .then(async r => { if (r.status === 401) actions.invalidateSession(owner); const value = await r.json();
        if (!r.ok) throw Error(value.detail ?? `Registration unavailable (${r.status}).`);
        if (!Array.isArray(value.branches) || !Array.isArray(value.departments) || !Array.isArray(value.categories)) throw Error("Invalid registration catalog.");
        if (!abort.signal.aborted && live.current === owner) setCatalog({ owner, data: value });
      }).catch(e => { if (!abort.signal.aborted && live.current === owner) setMessage(String(e)); });
    return () => { alive.current = false; abort.abort(); };
  }, [owner, actions]);
  const visible = owner && catalog?.owner === owner ? catalog.data : null;
  async function submit() {
    if (!owner || !visible || busy || preparingRef.current) return;
    preparingRef.current = true; setPreparing(true); setMessage("");
    try {
      if (!visible.branches.some(b => b.id === Number(branch)) || !visible.departments.some(d => d.id === Number(department) && d.branchId === Number(branch))
        || !visible.categories.some(c => c.id === Number(category)) || !fields.assetCode.trim() || !fields.assetName.trim()) throw Error("Select an authorized branch, department and category; enter code and name.");
      if (fields.purchaseDate && fields.warrantyExpiryDate && fields.warrantyExpiryDate < fields.purchaseDate) throw Error("Warranty expiry cannot precede purchase date.");
      const photos = await Promise.all(files.map(file => new Promise<{ name: string; mimeType: string; base64: string }>((resolve, reject) => {
        if (!["image/jpeg", "image/png"].includes(file.type)) { reject(Error("Photos must be JPG or PNG.")); return; }
        const reader = new FileReader(); reader.onerror = () => reject(Error("Cannot read photo."));
        reader.onload = () => resolve({ name: file.name, mimeType: file.type, base64: String(reader.result).split(",")[1] }); reader.readAsDataURL(file);
      })));
      if (!alive.current || live.current !== owner) return;
      const optional = (value: string) => value.trim() || null;
      await save({ assetCode: fields.assetCode.trim(), assetName: fields.assetName.trim(), assetTypeId: Number(category), branchId: Number(branch), currentDepartmentId: Number(department),
        serialNumber: optional(fields.serialNumber), brand: optional(fields.brand), model: optional(fields.model), purchaseDate: optional(fields.purchaseDate), warrantyExpiryDate: optional(fields.warrantyExpiryDate), description: optional(fields.description), photos }, () => alive.current && live.current === owner);
    } catch (e) { if (alive.current && live.current === owner) setMessage(String(e)); }
    finally { preparingRef.current = false; setPreparing(false); }
  }
  return <section><h1>Register asset</h1>{message && <p role="alert">{message}</p>}{visible && <fieldset disabled={busy || preparing}>
    <legend>Asset details</legend><label>Branch <select value={branch} onChange={e => { setBranch(e.target.value); setDepartment(""); }}><option value="">Select</option>{visible.branches.map(b => <option key={b.id} value={b.id}>{b.name}</option>)}</select></label>
    <label>Custodian department <select value={department} onChange={e => setDepartment(e.target.value)}><option value="">Select</option>{visible.departments.filter(d => d.branchId === Number(branch)).map(d => <option key={d.id} value={d.id}>{d.name}</option>)}</select></label>
    <label>Category <select value={category} onChange={e => setCategory(e.target.value)}><option value="">Select</option>{visible.categories.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}</select></label>
    {(["assetCode", "assetName", "serialNumber", "brand", "model", "purchaseDate", "warrantyExpiryDate"] as const).map(name => <label key={name}>{name}<input type={name.endsWith("Date") ? "date" : "text"} value={fields[name]} maxLength={name === "assetCode" ? 50 : name === "assetName" ? 200 : 100} onChange={e => setFields(v => ({ ...v, [name]: e.target.value }))} /></label>)}
    <label>Description <textarea value={fields.description} onChange={e => setFields(v => ({ ...v, description: e.target.value }))} /></label>
    <label>Photos <input key={String(owner)} type="file" multiple accept="image/jpeg,image/png" onChange={e => setFiles(Array.from(e.target.files ?? []))} /></label>
    <p>After registration, assign the asset to its first room or area.</p><button onClick={() => void submit()}>Register and assign location</button>
  </fieldset>}</section>;
}
