# Docker, CI and AWS deployment preparation

Source: user-shared https://chatgpt.com/s/t_6b0550ba200881918c41d17a03820259, retrieved 24 November 2026. User authorizes committing and pushing current P0 changes plus deployment work to feature/project2-role-alignment-20261123. No cloud provisioning requested; automatic AWS deployment activates only after explicit repository configuration. Preserve local Production5080 and TEST services. No subagents.

- [x] Read shared chat, current host/schema/auth/installer and Git scope.
- [x] Implement images, persistent Compose, trusted proxy/HTTPS, guarded database backup/init/migration, deploy/rollback and health checks.
- [x] Implement CI and optional AWS OIDC/ECR/S3/SSM delivery with immutable SHA images.
- [x] Validate builds/tests/scripts locally; Docker runtime validation on GitHub Linux runner (Docker unavailable locally).
- [x] Review all authorized P0/deployment files, scan secrets, commit and push current feature branch.
- [x] Inspect Actions results; fix failures and report actual verified versus pending AWS configuration. Final run 45368592341 passed build-test and docker-smoke at source commit 4313be40; AWS deploy intentionally skipped until configured.

Database: 3.0.0 current P0. Existing 07–10 scripts contain historical demo/data-specific operations and must not be replayed on each deployment. Initial server setup restores a verified backup into an absent database, or installs an empty schema for isolated CI only. Application deploy never invokes migration or restores over a live database. Schema migration is a separate backup-first, writers-stopped operation.
