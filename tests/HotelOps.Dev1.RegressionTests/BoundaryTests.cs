using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using HotelOps.Api.Controllers;
using HotelOps.Api.DTOs.HotelStructure;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.Security;
using HotelOps.Api.Security.Authorization;
using HotelOps.Api.Services.Authentication;
using HotelOps.Api.Services.HotelStructure;
using HotelOps.Api.Services.Housekeeping;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

// Null SQL collaborators intentionally fail immediately if an early boundary reaches persistence.
public sealed class BoundaryTests
{
    private static readonly NoIdentity Identity = new();
    private static SessionReference Ticket() => SessionReference.FromProtectedTicket(1, SessionCredential.Generate(), false, DateTime.UtcNow);
    private static BranchAdministrationService Branch() => new(null!, Identity, null!, null!);
    private static CleaningMonitorService Monitor() => new(null!, Identity, null!, null!, TimeProvider.System);
    private static TurnoverHistoryService History() => new(null!, Identity, null!, null!);
    private static RoomTypeDirectoryService Types() => new(null!, Identity, null!, null!);

    public static TheoryData<CreateBranchRequest> InvalidBranches => new()
    {
        new("", "Name", "Address", null, null), new("X", " ", "Address", null, null),
        new("X", "Name", " ", null, null), new("X!", "Name", "Address", null, null),
        new(new string('x',31), "Name", "Address", null, null), new("X", new string('x',201), "Address", null, null),
        new("X", "Name", new string('x',4001), null, null), new("X", "Name", "Address", "bad", null),
        new("X", "Name", "Address", null, "bad")
    };
    [Theory, MemberData(nameof(InvalidBranches)), Trait("UC", "UC012")]
    public async Task Create_rejects_invalid_fields_before_persistence(CreateBranchRequest input)
        => await Assert.ThrowsAsync<ValidationException>(() => Branch().CreateAsync(null!, 0, input, default));

    [Theory, InlineData(0), InlineData(7), InlineData(9), Trait("UC", "UC012")]
    public async Task Update_requires_eight_byte_rowversion(int length)
        => await Assert.ThrowsAsync<ValidationException>(() => Branch().UpdateAsync(null!, 0, 1, new byte[length], new("N","A",null,null,"valid reason"), default));

    [Theory, InlineData(""), InlineData("short"), Trait("UC", "UC012")]
    public async Task Update_requires_reason(string reason)
        => await Assert.ThrowsAsync<ValidationException>(() => Branch().UpdateAsync(null!, 0, 1, new byte[8], new("N","A",null,null,reason), default));

    [Theory, InlineData("isActive"), InlineData("timezone"), InlineData("branchId"), Trait("UC", "UC012")]
    public void Create_dto_rejects_server_owned_fields(string field)
        => Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CreateBranchRequest>("{\"Code\":\"X\",\"Name\":\"N\",\"Address\":\"A\",\"" + field + "\":false}"));

    [Fact, Trait("UC", "UC012")]
    public void Update_dto_rejects_code_mutation()
        => Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<UpdateBranchRequest>("{\"Name\":\"N\",\"Address\":\"A\",\"Reason\":\"valid reason\",\"Code\":\"X\"}"));

    public static TheoryData<CleaningMonitorQuery> InvalidMonitor => new()
    {
        new(Page:0), new(PageSize:0), new(PageSize:101), new(Page:int.MaxValue,PageSize:100),
        new(FloorId:0), new(AssigneeId:0), new(PriorityId:0), new(Kind:"unknown"),
        new(Search:new string('x',151)), new(Search:"a\nb"), new(Status:"BAD!"),
        new(FromDate:new(2026,2,2),ToDate:new(2026,2,1)), new(FromDate:DateOnly.MinValue), new(ToDate:DateOnly.MaxValue)
    };
    [Theory, MemberData(nameof(InvalidMonitor)), Trait("UC", "UC037")]
    public async Task Monitor_rejects_invalid_filters(CleaningMonitorQuery query)
        => await Assert.ThrowsAsync<ValidationException>(() => Monitor().ListAsync(null!, 1, 1, query, default));

    [Theory, InlineData(0,100), InlineData(1,0), InlineData(1,101), InlineData(int.MaxValue,100), Trait("UC", "UC037")]
    public async Task Timeline_rejects_invalid_pagination(int page, int size)
        => await Assert.ThrowsAsync<ValidationException>(() => Monitor().DetailAsync(null!,1,1,"cleaning",1,new(page,size),default));

    [Theory, InlineData(1,1), InlineData(2,100), InlineData(int.MaxValue,1), InlineData(int.MaxValue/100,100), Trait("UC", "UC037")]
    public async Task Valid_timeline_boundaries_still_deny_missing_identity(int page, int size)
    {
        var result = await Monitor().DetailAsync(null!,1,1,"cleaning",1,new(page,size),default);
        Assert.Equal(CleaningMonitorStatus.SessionInvalid,result.Status); Assert.Null(result.Value);
    }

    public static TheoryData<OperationalHistoryQuery> InvalidHistory => new()
    {
        new(Page:0), new(PageSize:0), new(PageSize:101), new(Page:int.MaxValue,PageSize:100),
        new(BranchId:0), new(RoomId:2), new(InspectorId:1), new(HousekeeperId:1), new(Search:"x"), new(Result:"INVALID"),
        new(From:new DateTime(2026,1,2,0,0,0,DateTimeKind.Utc),To:new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc)),
        new(From:new DateTime(2026,1,1))
    };
    [Theory, MemberData(nameof(InvalidHistory)), Trait("UC", "UC038")]
    public async Task History_rejects_unsupported_or_invalid_filters(OperationalHistoryQuery query)
        => await Assert.ThrowsAsync<ValidationException>(() => History().TurnoverHistoryAsync(null!,1,1,query,null,default));

    [Theory, InlineData(1,1), InlineData(2,100), InlineData(int.MaxValue,1), Trait("UC", "UC038")]
    public async Task History_valid_pages_deny_missing_identity(int page,int size)
    {
        var result = await History().TurnoverHistoryAsync(Ticket(),1,1,new(Page:page,PageSize:size),null,default);
        Assert.Equal(TurnoverHistoryStatus.SessionInvalid,result.Status); Assert.Null(result.Value);
    }

    [Theory, InlineData(0,20,"code"), InlineData(1,0,"code"), InlineData(1,101,"code"), InlineData(int.MaxValue,100,"code"), InlineData(1,20,"unknown"), Trait("UC", "UC142")]
    public async Task Room_types_reject_invalid_pagination_sort(int page,int size,string sort)
        => await Assert.ThrowsAsync<ValidationException>(() => Types().ListAsync(null!,1,null,null,null,page,size,sort,false,default));

    [Theory, InlineData("a\nb"), InlineData("a\tb"), Trait("UC", "UC142")]
    public async Task Room_types_reject_control_search(string search)
        => await Assert.ThrowsAsync<ValidationException>(() => Types().ListAsync(null!,1,null,null,search,1,20,"code",false,default));

    [Theory, InlineData(true,"code",1,1), InlineData(false,"name",2,100), InlineData(null,"status",int.MaxValue,1), Trait("UC", "UC142")]
    public async Task Valid_room_type_filters_do_not_bypass_identity(bool? active,string sort,int page,int size)
    {
        var result = await Types().ListAsync(Ticket(),1,null,active,"search",page,size,sort,true,default);
        Assert.Equal(RoomTypeDirectoryStatus.SessionInvalid,result.Status); Assert.Null(result.Value);
    }

    [Theory, InlineData(false,400), InlineData(true,401)]
    public async Task Controllers_reject_plaintext_and_anonymous_before_service(bool https,int status)
    {
        var context = new ControllerContext { HttpContext = new DefaultHttpContext() };
        context.HttpContext.Request.Scheme = https ? "https" : "http";
        var branch = new BranchAdministrationController(null!) { ControllerContext=context };
        var history = new TurnoverHistoryController(null!,null!,null!) { ControllerContext=context };
        var types = new RoomTypeDirectoryController(null!) { ControllerContext=context };
        Assert.Equal(status,Assert.IsAssignableFrom<ObjectResult>(await branch.Read(1,default)).StatusCode);
        Assert.Equal(status,Assert.IsAssignableFrom<ObjectResult>(await history.Turnovers(1,new(),default)).StatusCode);
        Assert.Equal(status,Assert.IsAssignableFrom<ObjectResult>(await types.Detail(1,null,default)).StatusCode);
    }
}

internal sealed class NoIdentity : ICurrentUserAccessor
{
    public bool TryGetCurrentUser(out CurrentUserInfo user) { user=default; return false; }
}

