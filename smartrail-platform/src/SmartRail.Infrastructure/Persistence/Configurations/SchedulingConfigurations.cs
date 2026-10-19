using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartRail.Domain.Reference;
using SmartRail.Domain.Scheduling;

namespace SmartRail.Infrastructure.Persistence.Configurations;

internal sealed class ServiceChangeLogConfiguration : IEntityTypeConfiguration<ServiceChangeLog>
{
    public void Configure(EntityTypeBuilder<ServiceChangeLog> b)
    {
        b.ToTable("service_change_log", t => t.HasCheckConstraint("ck_service_change_log_action", "action in ('Created', 'Amended', 'Cancelled')"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
        b.Property(x => x.Action).HasMaxLength(12);
        b.Property(x => x.Actor).HasMaxLength(64);
        b.Property(x => x.Details).HasColumnType("jsonb");
        b.HasIndex(x => x.ServiceId);
        b.HasOne<TrainService>().WithMany().HasForeignKey(x => x.ServiceId).OnDelete(DeleteBehavior.NoAction);
    }
}
