using System.ComponentModel.DataAnnotations;
using HotelOps.Api.DTOs.Housekeeping;
using HotelOps.Api.DTOs.OperationalTasks;
using HotelOps.Api.Services.HotelStructure;
using HotelOps.Api.Services.OperationalTasks;
using Xunit;

namespace HotelOps.Dev1.RegressionTests;

public sealed class TaskRecurrenceScheduleTests
{
    private static DateTime Utc(string value) => DateTime.Parse(value, null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal);
    [Fact]
    public void Weekly_uses_hotel_calendar_and_preserves_time_of_day()
    {
        var first=Utc("2026-11-15T18:30:12.1234567Z"); // Monday in hotel time.
        var rule=TaskRecurrenceSchedule.Normalize(new("WEEKLY",[3,1],null,7,null),first);
        Assert.Equal(new[]{1,3},rule.Weekdays);
        var next=TaskRecurrenceSchedule.Next(first,rule);
        Assert.Equal(Utc("2026-11-17T18:30:12.1234567Z"),next); // Wednesday local.
        Assert.Equal(Utc("2026-11-22T18:30:12.1234567Z"),TaskRecurrenceSchedule.Next(next!.Value,rule));
        Assert.Equal(new DateOnly(2026,11,17),TaskRecurrenceSchedule.GenerationHorizon(Utc("2026-11-15T17:30:00Z")));
    }
    [Theory]
    [InlineData("2027-01-31T03:00:00Z",31,"2027-03-31T03:00:00Z")]
    [InlineData("2028-01-29T03:00:00Z",29,"2028-02-29T03:00:00Z")]
    [InlineData("2027-01-29T03:00:00Z",29,"2027-03-29T03:00:00Z")]
    [InlineData("2026-12-31T03:00:00Z",31,"2027-01-31T03:00:00Z")]
    public void Monthly_never_changes_the_selected_calendar_day(string from,int day,string expected)
    {
        var first=Utc(from);var rule=TaskRecurrenceSchedule.Normalize(new("MONTHLY",[],day,7,null),first);
        Assert.Equal(Utc(expected),TaskRecurrenceSchedule.Next(first,rule));
    }
    [Fact]
    public void End_date_is_inclusive_and_no_occurrence_is_created_after_it()
    {
        var first=Utc("2026-11-16T03:00:00Z");var rule=TaskRecurrenceSchedule.Normalize(new("DAILY",[],null,7,new(2026,11,17)),first);
        var next=TaskRecurrenceSchedule.Next(first,rule);
        Assert.Equal(Utc("2026-11-17T03:00:00Z"),next);
        Assert.Null(TaskRecurrenceSchedule.Next(next!.Value,rule));
    }
    [Fact]
    public void Inconsistent_rules_cannot_silently_change_first_deadline()
    {
        var first=Utc("2026-11-16T03:00:00Z");
        TaskRecurrenceRequest[] invalid=[new("UNKNOWN",[],null,7,null),new("DAILY",[1],null,7,null),new("DAILY",[],1,7,null),
            new("WEEKLY",[],null,7,null),new("WEEKLY",[1,1],null,7,null),new("WEEKLY",[1,7],null,7,null),new("WEEKLY",[2],null,7,null),
            new("MONTHLY",[],0,7,null),new("MONTHLY",[],32,7,null),new("MONTHLY",[],17,7,null),new("DAILY",[],null,0,null),new("DAILY",[],null,7,new(2026,11,15))];
        foreach(var rule in invalid)Assert.Throws<ValidationException>(()=>TaskRecurrenceSchedule.Normalize(rule,first));
    }
    private static CreateOperationalTaskRequest Definition()=>new(Guid.NewGuid(),3,4,"Review result","Line one\nLine two","Expected one\r\nExpected two",
        [new ProgressDefinition("Reviewed",true)],2,null,null,60,null,null,null);
    [Fact]
    public void Occurrence_identity_is_stable_per_series_due_date_and_preserves_relative_plan()
    {
        var first = new DateTimeOffset(2026, 11, 16, 2, 0, 0, TimeSpan.Zero);
        var template = Definition() with { DueAt = first, PlannedStartAt = first.AddHours(-2) };
        var due = first.AddDays(7).UtcDateTime;
        var occurrence = TaskSeriesGeneration.Occurrence(template, 71, due);
        Assert.Equal(occurrence, TaskSeriesGeneration.Occurrence(template, 71, due));
        Assert.NotEqual(occurrence.RequestKey, TaskSeriesGeneration.Occurrence(template, 72, due).RequestKey);
        Assert.NotEqual(occurrence.RequestKey, TaskSeriesGeneration.Occurrence(template, 71, due.AddDays(1)).RequestKey);
        Assert.Equal(first.AddDays(7).AddHours(-2), occurrence.PlannedStartAt);
        Assert.Equal(template.Criteria, occurrence.Criteria);
        Assert.Equal(template.RequestedResult, occurrence.RequestedResult);
    }
    [Fact]
    public void Shared_creation_normalization_keeps_multiline_description_and_outcome()
    {
        var request=Definition();var normalized=RoomTypeCreationService.NormalizeTaskCreation(request);
        Assert.Equal(request.Description,normalized.Description);Assert.Equal(request.RequestedResult,normalized.RequestedResult);
        Assert.Equal(request.RequestKey,normalized.RequestKey);Assert.True(normalized.Criteria![0].Required);
    }
    [Fact]
    public void Multiline_support_does_not_admit_blank_outcome_control_characters_or_missing_criteria()
    {
        var valid=Definition();
        foreach(var input in new[]{valid with{RequestedResult=" \n "},valid with{RequestedResult="x\u0001"},valid with{Description="x\u0001"},valid with{Title="x\ny"},valid with{Criteria=[]}})
            Assert.Throws<ValidationException>(()=>RoomTypeCreationService.NormalizeTaskCreation(input));
    }
}
