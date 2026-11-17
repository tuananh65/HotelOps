disposition: fix

Review performed inline because no callable subagent tool is available. Scope: C+8 shell and representative desktop/mobile screens. Evidence: .impeccable/review/cacao/-users-1440.png, -users-390.png, -operations-incidents-1440.png, login-390.png. Code-led approved reference: C+8 interactive preview. No raster comp or quality-bar card supplied.

## persistence
Product exists; direction recorded in cacao-direction.md; DESIGN.md reflects the implemented palette.

## fidelity
TYPE: match, system sans and compact working headings. MATERIAL: match, plain CSS controls and SVG navigation. GROUND: match, cocoa rail and cream ground. Two-level desktop navigation: match. Mobile drawer: tested. User directory mobile table: adaptation, all fields retained with local scrolling. Incident form: contradicted, narrow fieldset wastes most of the content width.

## ceiling
Structured form layout remains underused in the incident workspace.

## material_fixes
1. Make the generated incident fieldset fill the form region so its existing responsive field grid can use available width.

## keep
Preserve backend-owned permissions, routes, cacao surfaces, semantic statuses and retained authentication feedback.

## Final verdict pass

The incident fieldset width fix is resolved: the final desktop capture shows four usable columns across the form, and mobile smoke still passes. No new regression is visible in the reviewed captures. Remaining: real backend/database verification and legacy failing tests, outside this visual verdict.

disposition: ship (reviewed presentation and listed fix only; not approval of all business workflows)
