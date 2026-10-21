using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartRail.Domain.Reference;
using SmartRail.Domain.Scheduling;

namespace SmartRail.Infrastructure.Persistence.Configurations;

internal sealed class ServiceChangeLogConfiguration : IEntityTypeConfiguration<ServiceChangeLog>
{
    public void Configure(EntityTypeBuilder<ServiceChangeLog> b)
    {
        b.ToTable("service_change_log", t => t.HasCheckConstraint("ck_service_change_log_action", "action in ('Created', 'Amended', 'Cancelled', 'Restored')"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
        b.Property(x => x.Action).HasMaxLength(12);
        b.Property(x => x.Actor).HasMaxLength(64);
        b.Property(x => x.Details).HasColumnType("jsonb");
        b.HasIndex(x => x.ServiceId);
        b.HasOne<TrainService>().WithMany().HasForeignKey(x => x.ServiceId).OnDelete(DeleteBehavior.NoAction);
    }
}

internal sealed class ServiceCancellationConfiguration : IEntityTypeConfiguration<ServiceCancellation>
{
    public void Configure(EntityTypeBuilder<ServiceCancellation> b)
    {
        b.ToTable("service_cancellation", t => t.HasCheckConstraint("ck_service_cancellation_range", "to_date IS NULL OR to_date >= from_date"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
        b.Property(x => x.TrainNumber).HasMaxLength(10);
        b.Property(x => x.Reason).HasMaxLength(200);
        b.Property(x => x.CreatedBy).HasMaxLength(64);
        b.Property(x => x.RestoredBy).HasMaxLength(64);
        b.HasIndex(x => new { x.LineId, x.TrainNumber, x.FromDate });
        b.HasOne<Line>().WithMany().HasForeignKey(x => x.LineId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<TrainService>().WithMany().HasForeignKey(x => x.ServiceId).OnDelete(DeleteBehavior.NoAction);
    }
}
