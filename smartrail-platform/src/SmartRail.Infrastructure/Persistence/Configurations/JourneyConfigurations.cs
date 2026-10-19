using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartRail.Domain.Journeys;
using SmartRail.Domain.Reference;

namespace SmartRail.Infrastructure.Persistence.Configurations;

// Operational tables follow _specs/smartrail-schema.sql. No seed rows here: the demo layer is added by DemoSeeder.

internal sealed class JourneyConfiguration : IEntityTypeConfiguration<Journey>
{
    public void Configure(EntityTypeBuilder<Journey> b)
    {
        b.ToTable("journey", t =>
        {
            t.HasCheckConstraint("ck_journey_status", EnumSql.InList<JourneyStatus>("status"));
            t.HasCheckConstraint("ck_journey_version", "version >= 0");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).HasDefaultValue(JourneyStatus.Unassigned);
        b.Property(x => x.Version).HasDefaultValue(0).IsConcurrencyToken();
        b.Property(x => x.CurrentDelayMinutes).HasDefaultValue(0);
        b.Property(x => x.IsOverdue).HasDefaultValue(false);
        b.HasIndex(x => new { x.ServiceId, x.OperatingDate }).IsUnique();
        b.HasIndex(x => x.OperatingDate).HasDatabaseName("ix_journey_operating_date");
        b.HasOne<TrainService>().WithMany().HasForeignKey(x => x.ServiceId).OnDelete(DeleteBehavior.NoAction);
    }
}

internal sealed class OperatorAssignmentConfiguration : IEntityTypeConfiguration<OperatorAssignment>
{
    public void Configure(EntityTypeBuilder<OperatorAssignment> b)
    {
        b.ToTable("operator_assignment", t =>
        {
            t.HasCheckConstraint("ck_operator_assignment_window", "valid_to IS NULL OR valid_to > valid_from");
            t.HasCheckConstraint("ck_operator_assignment_end_reason", EnumSql.InList<AssignmentEndReason>("end_reason"));
            t.HasCheckConstraint("ck_operator_assignment_ended_by", "end_reason IS NULL OR ended_by IS NOT NULL");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
        b.Property(x => x.OperatorUserId).HasMaxLength(64);
        b.Property(x => x.AssignedBy).HasMaxLength(64);
        b.Property(x => x.EndedBy).HasMaxLength(64);
        b.Property(x => x.EndReason).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.EndNote).HasMaxLength(500);
        b.HasIndex(x => x.JourneyId).HasDatabaseName("ix_assignment_journey");
        b.HasIndex(x => x.OperatorUserId).HasDatabaseName("ix_assignment_operator");
        b.HasOne<Journey>().WithMany().HasForeignKey(x => x.JourneyId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class JourneyStopStateConfiguration : IEntityTypeConfiguration<JourneyStopState>
{
    public void Configure(EntityTypeBuilder<JourneyStopState> b)
    {
        b.ToTable("journey_stop_state", t =>
            t.HasCheckConstraint("ck_journey_stop_state_confirmation", EnumSql.InList<StopConfirmation>("confirmation")));
        b.HasKey(x => new { x.JourneyId, x.ServiceStopId });
        b.Property(x => x.Confirmation).HasConversion<string>().HasMaxLength(10).HasDefaultValue(StopConfirmation.Pending);
        b.HasOne<Journey>().WithMany().HasForeignKey(x => x.JourneyId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<ServiceStop>().WithMany().HasForeignKey(x => x.ServiceStopId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<Platform>().WithMany().HasForeignKey(x => x.PlatformId).OnDelete(DeleteBehavior.NoAction);
    }
}

internal sealed class JourneyExceptionConfiguration : IEntityTypeConfiguration<JourneyException>
{
    public void Configure(EntityTypeBuilder<JourneyException> b)
    {
        b.ToTable("journey_exception", t =>
        {
            t.HasCheckConstraint("ck_journey_exception_type", EnumSql.InList<ExceptionType>("type"));
            t.HasCheckConstraint("ck_journey_exception_severity", EnumSql.InList<ExceptionSeverity>("severity"));
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
        b.Property(x => x.Type).HasConversion<string>().HasMaxLength(40);
        b.Property(x => x.Severity).HasConversion<string>().HasMaxLength(10);
        b.Property(x => x.CreatedAt).HasDefaultValueSql("now()");
        b.Property(x => x.ResolvedBy).HasMaxLength(64);
        b.Property(x => x.ResolvedByName).HasMaxLength(64);
        b.Property(x => x.Resolution).HasMaxLength(200);
        // One report, one exception of a type (D-44, AC-E05-02 2). Null event IDs (future types) never collide.
        b.HasIndex(x => new { x.RelatedEventId, x.Type }).IsUnique().HasDatabaseName("ux_exception_event_type");
        // The open queue: severity, then age (AC-E05-02 3).
        b.HasIndex(x => new { x.Severity, x.CreatedAt }).HasDatabaseName("ix_exception_open").HasFilter("resolved_at IS NULL");
        b.HasOne<Journey>().WithMany().HasForeignKey(x => x.JourneyId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<TrainEvent>().WithMany().HasForeignKey(x => x.RelatedEventId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<Station>().WithMany().HasForeignKey(x => x.StationId).OnDelete(DeleteBehavior.NoAction);
    }
}

internal sealed class TrainEventConfiguration : IEntityTypeConfiguration<TrainEvent>
{
    public void Configure(EntityTypeBuilder<TrainEvent> b)
    {
        b.ToTable("train_event", t =>
        {
            t.HasCheckConstraint("ck_train_event_type", EnumSql.InList<EventType>("event_type"));
            t.HasCheckConstraint("ck_train_event_source", EnumSql.InList<EventSource>("source"));
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
        b.Property(x => x.EventType).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.Source).HasConversion<string>().HasMaxLength(15);
        b.Property(x => x.CreatedBy).HasMaxLength(64);
        b.Property(x => x.Payload).HasColumnType("jsonb");
        b.Property(x => x.ReceivedAt).HasDefaultValueSql("now()");
        // Duplicate identity (D-35): one row per journey and client event ID.
        b.HasIndex(x => new { x.JourneyId, x.ClientEventId }).IsUnique();
        b.HasIndex(x => new { x.JourneyId, x.OccurredAt }).HasDatabaseName("ix_event_journey_occurred");
        b.HasOne<Journey>().WithMany().HasForeignKey(x => x.JourneyId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<ServiceStop>().WithMany().HasForeignKey(x => x.ServiceStopId).OnDelete(DeleteBehavior.NoAction);
    }
}
