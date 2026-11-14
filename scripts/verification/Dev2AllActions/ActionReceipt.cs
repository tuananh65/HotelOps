using HotelOps.Api.Controllers.Filters;
using HotelOps.Api.Services.Dev2Authorization;

internal sealed class ActionReceipt(Dev2KnownAction action)
{
    public string DecisionId {get;}=action.DecisionId;
    public string Method {get;}=action.HttpMethod;
    public string Route {get;}="/"+action.RouteTemplate;
    public string? FixtureId {get;set;}
    public string Phase {get;set;}="NOT_RUN";
    public string Actor {get;set;}="NOT_RUN";
    public IReadOnlyList<string> PermissionExpression {get;}=Dev2DemoBindings.PermissionCodes(action.DecisionId);
    public string ExpectedHttpStatus {get;set;}="NOT_RUN";
    public int? ActualHttpStatus {get;set;}
    public string ExpectedDatabaseEffect {get;}=action.HttpMethod=="GET"?"Authorized branch/root DTO; no business effects":"Exact per-action persisted effect in Effects.cs and server-attributed audit; atomic commit";
    public string ActualDatabaseEffect {get;set;}="NOT_RUN";
    public string AuthorizationResult {get;set;}="NOT_RUN";
    public string BusinessResult {get;set;}="NOT_RUN";
    public string AuditResult {get;set;}="NOT_RUN";
    public string ConcurrencyResult {get;set;}="NOT_RUN";
    public string RollbackResult {get;set;}="NOT_RUN";
    public string? FailureCategory {get;set;}
    public string Status {get;set;}="NOT_RUN";
    public string Continuation {get;set;}="NOT_APPLICABLE";
    public string ApiDiagnostic {get;set;}="NOT_RUN";
    public string? CorrelationId {get;set;}
    public List<object> Checks {get;}=[];
}
