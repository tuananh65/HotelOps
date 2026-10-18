namespace HotelOps.Api.DTOs.Assets;

public abstract class AssetActorRequest
{
    // TODO: Replace integration input with authenticated current-user context.
    // This User ID proves neither caller identity nor role/branch authorization.
    public long ActorUserId { get; init; }
}
