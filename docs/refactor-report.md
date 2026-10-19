# HotelOps refactor completion

The existing React 19 / TypeScript / Vite application was refactored in place. The working sidebar, header, page composition, serif titles, tables and Duc Vuong gold/charcoal/ivory identity were retained. The former routed page monolith was removed after its implementations were extracted and verified.

## Required screens and routing

- Exactly **80 required screen-level TSX files** under `src/screens/`.
- Exactly **80 registered required routes**, with explicit screen imports and validated component order.
- All **FE-01 through FE-15** represented; all **140 UC IDs** mapped to screens and retained service actions.
- Screens own their domain columns, summaries, forms, detail calculations, information fields and mapped workflow actions. Readiness, permissions, assignments, inspections, ledger, counts, shifts, workload and reports retain specialized views.
- `/dev/screens` is a separate development utility, grouped by FE. It provides current demo URLs and eligible roles without bypassing RBAC.
- [Screen matrix](screen-matrix.md) has exactly 80 rows, including source file, route, demo URL, FE, UC, roles and implementation status. [Demo URLs](screen-routes.md) contain real seeded IDs.

## Mock data

| Dataset | Records |
|---|---:|
| Users / roles / branches | 35 / 9 / 2 |
| Floors and areas / room types / rooms / departments | 18 / 10 / 40 / 8 |
| Cleaning / rework / inspections | 25 / 11 / 22 |
| Incidents / preventive schedules / scheduled executions | 21 / 11 / 16 |
| Assets / warehouses / inventory items | 41 / 6 / 32 |
| Stock documents / stocktakes / ledger entries | 49 / 11 / 224 |
| Material requests / replenishment requests | 17 / 11 |
| Operational tasks / shifts / handovers / internal requests | 31 / 17 / 11 / 17 |
| Notifications / audit entries | 55 / 31 |
| Historical workdays / closed work intervals | 102 / 12 |

Seeds use stable IDs. A versioned additive migration enriches existing browser storage while preserving saved records and edits. Posted material issues and replenishment receipts have corresponding ledger movements; documents include receipts, issues, return drafts and transfer drafts. Rooms, work, inspections, incidents, assets, requests and notifications have connected references. Fresh-seed reference integrity, volume and persistence are tested. Maintenance work is a service view combining assigned incidents with scheduled executions; its reserved storage array is not a separate workload source.

## Visual and interaction changes

The original anchor colors remain. Centralized gold/charcoal/warm-neutral shades distinguish filter bands, summaries, data surfaces and workflow panels. Muted sage, bronze, clay and neutral status colors improve hierarchy. Lists have domain-specific columns and KPIs. Detail views include domain-specific calculations and linked activity. Stocktakes show system quantities, counted quantities and variance. Workload groups open/overdue/closed work and effort by employee, with employee drill-down and separate time intervals. Desktop retains the full sidebar; tablet and mobile use the existing navigation as a drawer.

Authentication, CRUD modals, assignment, record arrows, breadcrumbs, sidebar links, notifications and UC actions continue to use the transactional mock service and existing permission checks. Inspection recording leads to the separate Inspection Result route for approval. Typed entity contracts and read views are available under `src/types/` and `src/services/`.

## Validation and limits

`npm install`, lint, typecheck, production build, **14 service tests** and **7 browser tests** passed. Browser smoke covers all 80 routes with eligible actor fixtures; representative workflows use normal seeded accounts. The development index, refresh, responsive layout, CRUD/RBAC, inspection approval, employee workload and report downloads are checked. [Verification artifacts](verification/verification.md) include file inventory, dataset counts and screenshots.

The application remains a browser-backed mock, as requested. UC mapping is complete; tests do not independently certify every alternative/exception path of all 140 UCs. Production authentication, server authorization, APIs, durable jobs and private file storage require backend integration. The production build succeeds with bundle-size warnings for the main application and ExcelJS export chunk.
