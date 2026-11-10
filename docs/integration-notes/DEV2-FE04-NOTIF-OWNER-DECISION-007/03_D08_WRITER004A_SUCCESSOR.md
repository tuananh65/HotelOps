# [HOTELOPS DEV2] FE04-NOTIF-OWNER-DECISION-007

D08=READY_FOR_OWNER_APPROVAL. Successor identity WRITER004B_OWNER007_V1, policy FE04_OWNER007_EXACT_TARGET_V1. Original Writer004A file SHA256 B09D16168882F4D3D9EF1C6A065771552B04B4CA07CAA3734645753B58B434BF and HardExpiry2026-11-10T11:44:09.1792768Z are unchanged. The original owner mapping was a structural proposed contract; no accepted lease issuer is invented.

Reuses FE04 architecture: five independently trusted owner signatures, RSA>=3072/PSS/SHA256 over version+LF+exact payload. Domain-separated successor proof; independently protected accepted public-key policy, not payload/config supplied authority. Only verifier/interfaces/default-closed implementations are supplied. No production signing keys, issued lease or central authority implementation.

D08_SUCCESSOR_CONTRACT.json lists every required field. New lease and retired lease IDs must differ, timing/epoch valid, independently signed successor HardExpiry bounds expiry. Exact common target/GUID, login/user/SID, native and guard module hashes, final composition/script/permission/catalog hashes and allowed use are bound. Runtime authority must enforce real central revocation/ownership before and after observation. Missing/expired/revoked/mismatched contracts reject. Unit keys are ephemeral and never written. Actual new-lease acceptance NOT_RUN.

RuntimeOwner must issue all null OWNER_REQUIRED values from real accepted evidence. Source commit/hash is not acceptance of a deployment script, schema, principal or permission set. Existing FE04 V1 acceptance needs a minimal independently approved version-aware adapter, without changing its old behavior or domain caller.
