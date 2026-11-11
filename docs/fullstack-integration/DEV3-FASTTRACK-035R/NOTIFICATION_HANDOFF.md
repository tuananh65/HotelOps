# DEV3 -> DEV2 FE14: notification boundary only

Source pin: integration 70e1d4ee2c4b63b14543f90c1e315bec39585d80 (unchanged production source in this delivery).

UC138: Services/WorkTime/ShiftPlanSaveService.cs defines IShiftPlanNotificationParticipant.StageAsync(HotelOpsDbContext, Guid transactionId, long actorId, IReadOnlyList<ShiftAssignmentNotice>, CancellationToken) -> Task<int>. Each notice has EmployeeId, persisted ShiftId and persisted AssignmentId, created in the SAME caller-owned Serializable transaction. Caller requires staged count == notice count, unchanged current transaction, final authority/conflict rereads and atomic audit before caller commit. No producer commit, external send or second DbContext is permitted.

UC139: WorkdayService.cs and SqlWorkTimeTransactionParticipant.cs own WorkdayId/UserId/BranchId and caller transaction. Scheduled Start/End trial does not add notification semantics. Unscheduled Start currently returns UNSCHEDULED_NOTIFICATION_UNAVAILABLE before persistence; there is no approved durable unscheduled operation/reservation identity to fabricate.

Required DEV2 response:
- Concrete implementation compatibility for the exact StageAsync interface above, including context/transaction ownership, durable storage identity, cancellation, rollback and no dispatch before commit.
- Reserve: owner-approved reservation/idempotency identity and caller-lock boundary BEFORE unscheduled mutation, if FS013 requires reservation. No Reserve interface exists in this DEV3 source pin.
- Stage: bind actual persisted WorkdayId/unscheduled operation identity (or UC138 persisted AssignmentId) in the same transaction after insertion; no guessed IDs or client-selected recipient authority.
- Verify: define exact durable completeness/revalidation API before caller commit; current UC138 caller checks count and transaction identity only. No Verify interface exists here; do not treat the count as independent SQL/outbox acceptance.

Please supply exact FE14 source commit, interface signatures, no-SQL receipts and remaining SQL race/rollback gates. Do not rewrite FS013 or activate either writer for this handoff. This dependency does not block the independent scheduled SQL/HTTPS trial preparation package.
