# UC139 End of an existing unscheduled workday

Status remains PARTIAL_IMPLEMENTATION. No new UC completion; counters unchanged.

The existing backend End workflow accepts an owned Working workday with current row version and no open interval, regardless of whether it was scheduled. The frontend previously blocked unscheduled records and rejected their valid End response. The form now permits End for an existing unscheduled workday only when its assignment is null, its branch matches and no interval is open. Scheduled records still require a valid assignment. Response confirmation binds the returned scheduled/unscheduled flag and assignment to the reviewed input, in addition to workday ID, branch, status, timestamps and ETag.

Start remains scheduled-only: null assignment cannot dispatch a Start request. End still sends only pauseAll=false, uses CSRF and If-Match, and does not retry uncertain results. No new backend permission, SQL mutation, schema or server lifecycle semantics.

Actual validation: TypeScript/Vite PASS after correcting the End API nullable assignment signature (initial TS2345 failure resolved). Existing React Router use-client and large-bundle warnings remain. 73 transport tests PASS; three focused mocked browser tests PASS (scheduled Start/End, unscheduled End with shift-read denied, unscheduled open-interval denial). No live HTTPS/SQL evidence and no backend changes in this increment.

Pending contract question: unscheduled employee department/Head recipient read authorization, and approved END_OF_SHIFT reference code for Pause All. Existing unscheduled Start and Pause All remain fail-closed pending those contracts and implementation. This fix does not certify full UC139.
