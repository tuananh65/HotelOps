# FE-04 Tester handover

FS010 scope: UC028 Create / UC029 Assign first. Candidate UC029 is PROVISIONAL_NOT_ACCEPTED. No TESTER_ACCEPTED claim. All 38 manual cases start NOT_RUN; Tester owns actual results, evidence, defects and acceptance.

1. Read TEST_ENVIRONMENT.md and KNOWN_ISSUES.md before executing.
2. Open UNIT_TEST_CASES.xlsx (67 executed parameterized cases / 31 distinct methods, from real TRX; no invented methods).
3. Read UNIT_TEST_REPORT.md, then execute TESTER_TEST_CASES.xlsx (38 cases) with TEST_EXECUTION_GUIDE.md.
4. Use UC_TEST_COVERAGE_MATRIX.md to identify uncovered UCs and integration gates.
5. Return TESTER_FEEDBACK_TEMPLATE.md plus workbook/evidence; accepted shared publisher remains a separate prerequisite.

TEST_EVIDENCE includes actual xUnit TRX and workbook cell-by-cell reopen validation. Live/fault receipts are linked under ../../fs010. UNIT, COMPONENT, INTERCEPTION, LIVE_API_SQL and REAL_BROWSER_E2E are distinct evidence levels. Workbook content is developer-prepared test specification, not a Tester run. Full module TESTER_READY is conditional: accepted integration case blocked, dedicated inactive/source fixtures may need provisioning. Candidate-package execution is possible on the retained verified local trial; full acceptance is not.

Generate/validate workbooks with: node scripts/verification/FS010/Build-TestHandover.cjs. Run after checking out the tested implementation SHA (see UNIT_TEST_REPORT). No coverage percentage measured.
