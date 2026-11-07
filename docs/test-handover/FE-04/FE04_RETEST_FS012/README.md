# FS012 versioned developer retest addendum

Code checkpoint: 0a4139fa3520577e82c388fab0893b508da9b820. Not a replacement for FS011 Tester handover, not accepted A01/A02 and not a deployed least-privilege login package.

FS011 frozen ZIP SHA256: 8e661796fe83b7b15c16d84f79547ce72e4cdd6272f7996ce2fc659003141974. Its 38-case functional workbook is included unchanged as FS011_FUNCTIONAL_CASES_REFERENCE.xlsx; its commit/environment remain FS011. FS012_RETEST_CASES.xlsx contains8 affected-case groups, all NOT_RUN; Tester owns actuals/status. Current executed unit mapping is77 cases/34 methods,137/137 suite; all original75 cases/34 methods found with exact source method and parameter input.

Reproduce DEV only on a NEW exact FS012 trial: build backend/helper, New-ApprovedTrial -ExecuteApprovedCreate -AllowLocalSqlCertificateException, helper seed --approved-seed --local-sql-certificate-exception, Install-Candidate, then owned Start-LiveEnvironment and Run-LiveSuite. Existing target refuses overwrite; reuse only owned start/stop and append capacity if needed. Fault proofs are DEVELOPER_ONLY, never on FS011 Tester/shared/historical DB. Source manifest covers tracked non-docs files including solution/config/harness dependencies; checkout CODE_COMMIT, validate hashes, then build. Local SQL connection is supplied by owned child environment, never committed.

API live uses the developer Windows identity. Restricted-user tests use NOLOGIN user impersonation and nonpooled explicitly reverted connections. Privileged writes remain outside boundary. Tester execution pending; full FE04 not merge-ready. See included scope/limitations and failed attempt receipts.
