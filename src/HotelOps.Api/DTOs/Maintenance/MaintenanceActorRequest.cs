namespace HotelOps.Api.DTOs.Maintenance;

public abstract class MaintenanceActorRequest
{
    // TODO: Replace temporary integration identity with authenticated current-user context.
    // ActorUserId is not proof of caller identity or branch/role authorization.
    public long ActorUserId { get; init; }
}
