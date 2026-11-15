namespace HotelOps.Api.DTOs.OperationalTasks;

public sealed record DeclineOperationalTaskRequest(string Reason);
public sealed record TaskDeclineResult(long TaskId, long BranchId, string State, string MessageCode);
