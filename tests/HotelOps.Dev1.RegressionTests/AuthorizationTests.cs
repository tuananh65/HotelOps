using System.Security.Claims;
using HotelOps.Api.Security;
using HotelOps.Api.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

// Service tests: the real PermissionService evaluates supplied persistence facts.
// These synthetic paths are not claims about the SQL permission catalog.
public sealed class AuthorizationTests
{
    [Theory]
    [InlineData("ALL",null,2,false,true)]
    [InlineData("ALL",1L,1,false,true)]
    [InlineData("ALL",1L,2,false,false)]
    [InlineData("BRANCH",1L,1,false,true)]
    [InlineData("BRANCH",1L,2,false,false)]
    [InlineData("OWN",1L,1,true,true)]
    [InlineData("OWN",1L,1,false,false)]
    [InlineData("OWN",1L,2,true,false)]
    [InlineData("DEPARTMENT",1L,1,true,false)]
    public async Task Resource_scope_uses_verified_branch_and_ownership(string scope,long? assignedBranch,long resourceBranch,bool owns,bool granted)
    {
        var service = new PermissionService(new Identity(),new Facts(scope,assignedBranch),new Resolver(resourceBranch,owns),TimeProvider.System);
        var result = await service.EvaluateResourceAsync("CLEANING_VIEW",new("cleaning_task",10),default);
        Assert.Equal(granted,result.IsGranted);
    }

    [Theory, InlineData("ALL",true), InlineData("BRANCH",false), InlineData("OWN",false)]
    public async Task Global_check_does_not_borrow_branch_or_own_scope(string scope,bool granted)
    {
        var service = new PermissionService(new Identity(),new Facts(scope,scope=="ALL"?null:1),new Resolver(1,true),TimeProvider.System);
        Assert.Equal(granted,(await service.EvaluateGlobalAsync("CLEANING_VIEW",default)).IsGranted);
    }

    [Theory, InlineData(false,true), InlineData(true,false)]
    public async Task Inactive_user_or_missing_permission_denied(bool active,bool path)
    {
        var service = new PermissionService(new Identity(),new Facts("ALL",null){Active=active,HasPath=path},new Resolver(1,true),TimeProvider.System);
        Assert.Equal(PermissionDecision.Denied,(await service.EvaluateGlobalAsync("CLEANING_VIEW",default)).Decision);
    }

    [Theory, InlineData(false), InlineData(true)]
    public async Task Missing_or_restricted_identity_never_reads_store(bool restricted)
    {
        ICurrentUserAccessor identity = restricted ? new Identity(true) : new NoIdentity();
        var service = new PermissionService(identity,null!,null!,TimeProvider.System);
        Assert.Equal(restricted?PermissionDecision.Restricted:PermissionDecision.Unauthenticated,
            (await service.EvaluateGlobalAsync("CLEANING_VIEW",default)).Decision);
    }

    [Theory, InlineData(false), InlineData(true)]
    public void Role_name_without_validated_cookie_snapshot_never_creates_identity(bool authenticated)
    {
        var http = new DefaultHttpContext {User=new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim(ClaimTypes.Role,"SYS_ADMIN"),new Claim(ClaimTypes.NameIdentifier,"1")}, authenticated?HotelOpsCookieTicket.Scheme:null))};
        Assert.False(new CurrentUserAccessor(new HttpContextAccessor{HttpContext=http}).TryGetCurrentUser(out _));
    }

    [Fact]
    public async Task Anonymous_authorization_handler_does_not_consult_permissions()
    {
        var http = new DefaultHttpContext();
        var requirement = new PermissionRequirement("CLEANING_VIEW",PermissionCheck.Global);
        var context = new AuthorizationHandlerContext(new[]{requirement},http.User,http);
        await new PermissionAuthorizationHandler(null!,new NoIdentity(),new HttpContextAccessor{HttpContext=http}).HandleAsync(context);
        Assert.True(context.HasFailed); Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task Cancellation_propagates_before_store_access()
    {
        using var cts=new CancellationTokenSource(); cts.Cancel();
        var service=new PermissionService(new Identity(),null!,null!,TimeProvider.System);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>service.EvaluateGlobalAsync("CLEANING_VIEW",cts.Token));
    }

    private sealed class Identity(bool restricted=false):ICurrentUserAccessor
    {
        public bool TryGetCurrentUser(out CurrentUserInfo user) {user=new(1,1,restricted);return true;}
    }
    private sealed class Facts(string scope,long? branch):IPermissionDataSource
    {
        public bool Active {get;init;}=true;
        public bool HasPath {get;init;}=true;
        public Task<bool> IsActiveUserAsync(long id,CancellationToken ct)=>Task.FromResult(Active);
        public Task<bool> IsActiveBranchAsync(long id,CancellationToken ct)=>Task.FromResult(true);
        public Task<IReadOnlyList<PermissionGrantPath>> ReadPathsAsync(long id,string code,DateTime now,CancellationToken ct)
            =>Task.FromResult<IReadOnlyList<PermissionGrantPath>>(HasPath?[new(code,scope,branch,true,true,now.AddDays(-1),null,true,true)]:[]);
    }
    private sealed class Resolver(long branch,bool owns):IPermissionResourceResolver
    {
        public Task<VerifiedPermissionResource?> ResolveAsync(string code,PermissionResource resource,long user,CancellationToken ct)
            =>Task.FromResult<VerifiedPermissionResource?>(new(branch,owns));
    }
}

