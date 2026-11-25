# Refill rejection integration

Integrated optional HOTELOPS_REFILL_REJECTION_BROWSER=1 into the existing insufficient-stock/rejection test. The browser result feeds the original persisted reason/history and unchanged SQL inventory/allocation assertions. No production code or DB changed.

node --check passed for runner and helper. Runtime NOT RUN: current child execution instruction requires returning immediately after module integration and syntax verification. Parent may run after fresh TEST backup with HOTELOPS_REWORK_POSITIVE_PROBE=1, HOTELOPS_REFILL_CONTINUATION_PROBE=1 and HOTELOPS_REFILL_REJECTION_BROWSER=1 using the finish-realtime build. This replaces one existing test action rather than adding an assertion count; expect the same25 top-level checks.

Open questions: none.
