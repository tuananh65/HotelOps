# UC032 — frontend integration mapping

## Reuse and boundary

Screen26 `CleaningTaskDetailScreen` is reused. `/cleaning/tasks/<numeric-id>?branchId=<positive-id>` renders `CleaningProgressPanel`, keyed by branch/task to unmount/cancel stale work. Without branchId the existing prototype remains visibly labelled; no real API fallback to prototype on errors. No changes to shared DetailPage/Shell/SignIn, FE02 room master, Workday or DEV2 source.

| Existing prototype | API version | Contract / behavior |
|---|---|---|
| `service.visible("cleaning")`, mock RecordData | `readCleaningProgress` | GET known-ID progress + ETag; no task-list API invented |
| criteria string/completedItems | dto.items id/label/required/done/note | Frozen per-cleaning-task definitions; never inspection criteria |
| mock progress editing | `saveCleaningProgress` | PUT percent/items/note/photos; server snapshot IDs only |
| `service.perform`/start/complete | No equivalent added | UC032 only;100 remains IN_PROGRESS |
| mock rowVersion | Response ETag + exact If-Match |16 uppercaseHEX in quotes; no Base64/date/hash generated client-side |
| prototype currentUser | Browser cookies + server authority | No actor/role/ownership payload; branch query remains untrusted filter |
| local mock success | PUT response then fresh GET | Success label only after valid response. If refresh fails after success, say saved and lock until reload, never call write failed or resend automatically |

## States and validation

- Initial loading; failed/unauthorized/forbidden read leaves no edit form. Invalid shape/branch/task/state/tag rejected before render.
- Percent integer0..100; empty number input safely renders blank; backend revalidates. Notes one line, global4000/item1000.
- Checklist empty state; current unchecked/null item becomes an explicit false when edited/saved. Required flag displayed; progress100 does not fill missing checks or transition phase.
- File input: max5, each max10MiB, JPG/PNG/PDF MIME+name+extension+magic checks before request. Actual server validates content and creates private storage links. No base64 or tokens logged/stored.
- Saving disables fieldset/button. CSRF bootstrap fresh perSave; exact strong If-Match; credentials include/no-store/same-origin; no automatic PUT retry.
- 400 validation keeps editable draft; CSRF400 requires reload. 401/403/404/409/412/428/503/network/malformed success lock save until reload; draft stays visible when a read snapshot existed. Reload explicitly labelled “bỏ bản sửa”. New edits are never silently replayed over a new version.
- PUT success uses returned DTO/revision, then GET refresh. GET failure after committed response has distinct message and locks save. Network/invalid-response after PUT is ambiguous: reload first, no assertion of rollback.
- Read/save AbortControllers canceled on branch/task change or unmount. Cancellation after send is not proof of SQL rollback; live transaction acceptance still needed.

## Tests and remaining scope

Node tests cover source routes/cookies/CSRF/ETag/DTO/ownership shape/invalid fields/status propagation/file negatives/no retry/canceled bootstrap. Browser interception covers save→GET refresh,100%,412 draft/reload,400/403/409/428/503, wrong-branch DTO, post-save refresh failure, loading/empty/401 and stale board branch cancellation. Existing FS-002 board tests remain in same suite.

Interception is not server authorization or SQL write acceptance. Missing live test account, schema, current assignment and real session are external gates. Known-ID/query mode is a development slice, not a complete Cleaning Task Detail replacement; task collection/navigation/source-read, timeline/gallery/private download UI and shared login remain pending. No UC credit.
