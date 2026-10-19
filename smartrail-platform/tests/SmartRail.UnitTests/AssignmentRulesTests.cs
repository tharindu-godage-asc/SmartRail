using Microsoft.Extensions.Time.Testing;
using SmartRail.Application.Features.Assignments;
using SmartRail.Domain.Journeys;
using Xunit;

namespace SmartRail.UnitTests;

/// <summary>Spec 17 section 6: the pure rules, and what the handler asks of the operator directory.</summary>
public sealed class AssignmentRulesTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 6, 3, 0, 0, TimeSpan.Zero);

    private static DateTimeOffset At(int minutes) => T0.AddMinutes(minutes);

    [Fact]
    public void An_assignment_covers_its_start_but_not_its_end_and_an_open_end_covers_everything_after()
    {
        Assert.True(AssignmentRules.Covers(At(0), At(60), At(0)));
        Assert.True(AssignmentRules.Covers(At(0), At(60), At(59)));
        Assert.False(AssignmentRules.Covers(At(0), At(60), At(60)));
        Assert.False(AssignmentRules.Covers(At(0), At(60), At(-1)));
        Assert.True(AssignmentRules.Covers(At(0), null, At(100000)));
    }

    [Fact]
    public void Two_ranges_overlap_when_each_starts_before_the_other_ends_and_touching_ranges_do_not()
    {
        Assert.True(AssignmentRules.Overlaps(At(0), At(60), At(30), At(90)));
        Assert.True(AssignmentRules.Overlaps(At(0), null, At(500), null));
        Assert.True(AssignmentRules.Overlaps(At(0), null, At(-10), At(1)));
        Assert.False(AssignmentRules.Overlaps(At(0), At(60), At(60), At(90))); // one ends exactly where the next starts: a crew change
        Assert.False(AssignmentRules.Overlaps(At(0), At(60), At(-30), At(0)));
    }

    [Fact]
    public void The_default_start_is_the_lead_before_departure_but_never_before_now()
    {
        Assert.Equal(At(30), AssignmentRules.DefaultFrom(At(60), TimeSpan.FromMinutes(30), At(0)));
        Assert.Equal(At(50), AssignmentRules.DefaultFrom(At(60), TimeSpan.FromMinutes(30), At(50))); // already inside the lead: now
        Assert.Equal(At(50), AssignmentRules.DefaultFrom(At(10), TimeSpan.FromMinutes(30), At(50))); // already departed: now
    }

    [Fact]
    public void A_start_in_the_past_is_allowed_up_to_the_tolerance_only()
    {
        var tolerance = TimeSpan.FromMinutes(5);

        Assert.False(AssignmentRules.StartsTooFarInThePast(At(-5), At(0), tolerance));
        Assert.True(AssignmentRules.StartsTooFarInThePast(At(-6), At(0), tolerance));
        Assert.False(AssignmentRules.StartsTooFarInThePast(At(10), At(0), tolerance));
    }

    [Fact]
    public void Only_a_lost_device_blocks_a_report_received_at_or_after_the_end()
    {
        Assert.True(AssignmentRules.BlocksLateReport(AssignmentEndReason.LostDevice, At(60), At(60)));
        Assert.True(AssignmentRules.BlocksLateReport(AssignmentEndReason.LostDevice, At(60), At(90)));
        Assert.False(AssignmentRules.BlocksLateReport(AssignmentEndReason.LostDevice, At(60), At(59)));
        Assert.False(AssignmentRules.BlocksLateReport(AssignmentEndReason.CrewChange, At(60), At(90)));
        Assert.False(AssignmentRules.BlocksLateReport(AssignmentEndReason.Other, At(60), At(90)));
        Assert.False(AssignmentRules.BlocksLateReport(null, null, At(90)));
        Assert.False(AssignmentRules.BlocksLateReport(AssignmentEndReason.LostDevice, null, At(90)));
    }

    private sealed class FakeDirectory(params DirectoryOperator[] operators) : IOperatorDirectory
    {
        public Task<IReadOnlyList<DirectoryOperator>> ListAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<DirectoryOperator>>(operators);

        public Task<DirectoryOperator?> FindAsync(string userId, CancellationToken cancellationToken) => Task.FromResult(operators.FirstOrDefault(o => o.UserId == userId));
    }

    private sealed class FailingDirectory : IOperatorDirectory
    {
        public Task<IReadOnlyList<DirectoryOperator>> ListAsync(CancellationToken cancellationToken) => throw new HttpRequestException();

        public Task<DirectoryOperator?> FindAsync(string userId, CancellationToken cancellationToken) => throw new HttpRequestException();
    }

    private sealed class RecordingRepository : IAssignmentRepository
    {
        public int Assigned;

        public Task<IReadOnlyList<JourneyListItem>> ListJourneysAsync(DateOnly date, bool unassignedOnly, DateTimeOffset now, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyDictionary<string, BusyOn>> BusyOperatorsAsync(Guid? journeyId, DateTimeOffset from, DateTimeOffset? to, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<(DateTimeOffset FirstDeparture, DateTimeOffset LastArrival)?> GetJourneyTimesAsync(Guid journeyId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyList<AssignmentView>?> GetAssignmentsAsync(Guid journeyId, CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<AssignOutcome> AssignAsync(AssignCommand command, CancellationToken cancellationToken)
        {
            Assigned++;
            var view = new AssignmentView(Guid.NewGuid(), command.OperatorUserId, command.OperatorUserId, T0, null, command.Actor, null, null, null);
            return Task.FromResult(new AssignOutcome(AssignOutcomeKind.Done, view, null, "Scheduled", null));
        }

        public Task<EndOutcome> EndAsync(Guid journeyId, Guid assignmentId, AssignmentEndReason reason, string? note, string actor, DateTimeOffset now, CancellationToken cancellationToken) =>
            Task.FromResult(new EndOutcome(EndOutcomeKind.NotFound, null, null));
    }

    private static AssignOperatorHandler Handler(IOperatorDirectory directory, RecordingRepository repository) =>
        new(directory, repository, new FakeTimeProvider(T0), new AssignmentSettings());

    [Fact]
    public async Task An_unknown_a_non_operator_and_a_disabled_account_are_refused_before_anything_is_saved()
    {
        var repository = new RecordingRepository();
        var directory = new FakeDirectory(
            new DirectoryOperator("ok", "Ok Operator"),
            new DirectoryOperator("controller", "A Controller", IsOperator: false),
            new DirectoryOperator("gone", "Gone", Enabled: false));
        var handler = Handler(directory, repository);

        Assert.Equal(AssignError.OperatorNotFound, (await handler.HandleAsync(Guid.NewGuid(), "nobody", null, null, false, "c", default)).Error);
        Assert.Equal(AssignError.NotAnOperator, (await handler.HandleAsync(Guid.NewGuid(), "controller", null, null, false, "c", default)).Error);
        Assert.Equal(AssignError.OperatorDisabled, (await handler.HandleAsync(Guid.NewGuid(), "gone", null, null, false, "c", default)).Error);
        Assert.Equal(0, repository.Assigned);

        var ok = await handler.HandleAsync(Guid.NewGuid(), "ok", null, null, false, "c", default);
        Assert.Equal(AssignError.None, ok.Error);
        Assert.Equal("Ok Operator", ok.Assignment!.OperatorName); // the name comes from the directory
        Assert.Equal(1, repository.Assigned);
    }

    [Fact]
    public async Task A_directory_that_does_not_answer_saves_nothing()
    {
        var repository = new RecordingRepository();

        var result = await Handler(new FailingDirectory(), repository).HandleAsync(Guid.NewGuid(), "ok", null, null, false, "c", default);

        Assert.Equal(AssignError.DirectoryUnavailable, result.Error);
        Assert.Equal(0, repository.Assigned);
    }

    [Theory]
    [InlineData("LostDevice", true)]
    [InlineData("CrewChange", true)]
    [InlineData("Other", true)]
    [InlineData("lostdevice", false)]
    [InlineData("Stolen", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public async Task The_end_reason_is_one_of_three_words(string? reason, bool valid)
    {
        var handler = new EndAssignmentHandler(new RecordingRepository(), new FakeDirectory(), new FakeTimeProvider(T0));

        var result = await handler.HandleAsync(Guid.NewGuid(), Guid.NewGuid(), reason, null, "c", default);

        Assert.Equal(!valid, result.Error == EndError.InvalidReason);
    }
}
