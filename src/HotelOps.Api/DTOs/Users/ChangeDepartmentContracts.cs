using System.Text.Json.Serialization;
namespace HotelOps.Api.DTOs.Users;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ChangeDepartmentRequest(long DepartmentId);
public sealed record DepartmentChangeView(long UserId, long BranchId, long PreviousDepartmentId, long DepartmentId);
public enum DepartmentChangeStatus { Success, SessionInvalid, Forbidden, NotFound, Stale, RoleConflict, InvalidScope, NoChange, OpenWork, CoverageRequired, ReleaseRequired, LastAdministrator }
public sealed record DepartmentChangeResult(DepartmentChangeStatus Status, DepartmentChangeView? Value = null, byte[]? Version = null);
public sealed record DepartmentReview(long UserId, long BranchId, long DepartmentId);
public sealed record DepartmentReviewResult(DepartmentChangeStatus Status, DepartmentReview? Value = null, byte[]? Version = null);
