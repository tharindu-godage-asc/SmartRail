using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SmartRail.Domain.Reference;
using SmartRail.Infrastructure.Seeding;

namespace SmartRail.Infrastructure.Persistence.Configurations;

// Tables and columns follow _specs/smartrail-schema.sql. Reference rows come from ReferenceData (HasData).

internal sealed class LanguageConfiguration : IEntityTypeConfiguration<Language>
{
    public void Configure(EntityTypeBuilder<Language> b)
    {
        b.ToTable("language");
        b.HasKey(x => x.Code);
        b.Property(x => x.Code).HasMaxLength(5);
        b.Property(x => x.Name).HasMaxLength(50);
        b.Property(x => x.IsDefault).HasDefaultValue(false);
        // Exactly one default (fallback) language.
        b.HasIndex(x => x.IsDefault).IsUnique().HasFilter("is_default").HasDatabaseName("ux_language_default");
        b.HasData(ReferenceData.Languages);
    }
}

internal sealed class LineConfiguration : IEntityTypeConfiguration<Line>
{
    public void Configure(EntityTypeBuilder<Line> b)
    {
        b.ToTable("line");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
        b.Property(x => x.Code).HasMaxLength(20);
        b.Property(x => x.Name).HasMaxLength(100);
        b.Property(x => x.OperationsPhone).HasMaxLength(30);
        b.HasIndex(x => x.Code).IsUnique();
        b.HasData(ReferenceData.Lines);
    }
}

internal sealed class LineTranslationConfiguration : IEntityTypeConfiguration<LineTranslation>
{
    public void Configure(EntityTypeBuilder<LineTranslation> b)
    {
        b.ToTable("line_translation");
        b.HasKey(x => new { x.LineId, x.LanguageCode });
        b.Property(x => x.LanguageCode).HasMaxLength(5);
        b.Property(x => x.Name).HasMaxLength(100);
        b.HasOne<Line>().WithMany().HasForeignKey(x => x.LineId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Language>().WithMany().HasForeignKey(x => x.LanguageCode).OnDelete(DeleteBehavior.NoAction);
        b.HasData(ReferenceData.LineTranslations);
    }
}

internal sealed class StationConfiguration : IEntityTypeConfiguration<Station>
{
    public void Configure(EntityTypeBuilder<Station> b)
    {
        b.ToTable("station");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
        b.Property(x => x.Code).HasMaxLength(10);
        b.Property(x => x.Name).HasMaxLength(100);
        b.Property(x => x.ContactPhone).HasMaxLength(30);
        b.Property(x => x.HasCateringFacility).HasDefaultValue(false);
        b.HasIndex(x => x.Code).IsUnique();
        b.HasData(ReferenceData.Stations);
    }
}

internal sealed class StationTranslationConfiguration : IEntityTypeConfiguration<StationTranslation>
{
    public void Configure(EntityTypeBuilder<StationTranslation> b)
    {
        b.ToTable("station_translation");
        b.HasKey(x => new { x.StationId, x.LanguageCode });
        b.Property(x => x.LanguageCode).HasMaxLength(5);
        b.Property(x => x.Name).HasMaxLength(100);
        b.HasOne<Station>().WithMany().HasForeignKey(x => x.StationId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Language>().WithMany().HasForeignKey(x => x.LanguageCode).OnDelete(DeleteBehavior.NoAction);
        b.HasData(ReferenceData.StationTranslations);
    }
}

internal sealed class PlatformConfiguration : IEntityTypeConfiguration<Platform>
{
    public void Configure(EntityTypeBuilder<Platform> b)
    {
        b.ToTable("platform");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
        b.Property(x => x.Code).HasMaxLength(10);
        b.HasIndex(x => new { x.StationId, x.Code }).IsUnique();
        // Target of the composite key on service_stop: keeps a planned platform inside the stop's own station.
        b.HasAlternateKey(x => new { x.Id, x.StationId });
        b.HasOne<Station>().WithMany().HasForeignKey(x => x.StationId).OnDelete(DeleteBehavior.Cascade);
        b.HasData(ReferenceData.Platforms);
    }
}

internal sealed class LineStationConfiguration : IEntityTypeConfiguration<LineStation>
{
    public void Configure(EntityTypeBuilder<LineStation> b)
    {
        b.ToTable("line_station", t =>
        {
            t.HasCheckConstraint("ck_line_station_sequence", "sequence > 0");
            t.HasCheckConstraint("ck_line_station_distance", "distance_km >= 0");
        });
        b.HasKey(x => new { x.LineId, x.StationId });
        b.Property(x => x.DistanceKm).HasPrecision(6, 1);
        b.HasIndex(x => new { x.LineId, x.Sequence }).IsUnique();
        b.HasOne<Line>().WithMany().HasForeignKey(x => x.LineId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Station>().WithMany().HasForeignKey(x => x.StationId).OnDelete(DeleteBehavior.NoAction);
        b.HasData(ReferenceData.LineStations);
    }
}

internal sealed class TrainServiceConfiguration : IEntityTypeConfiguration<TrainService>
{
    public void Configure(EntityTypeBuilder<TrainService> b)
    {
        b.ToTable("train_service", t =>
        {
            t.HasCheckConstraint("ck_train_service_type", EnumSql.InList<ServiceType>("service_type"));
            t.HasCheckConstraint("ck_train_service_category", EnumSql.InList<ServiceCategory>("service_category"));
            t.HasCheckConstraint("ck_train_service_direction", EnumSql.InList<ServiceDirection>("direction"));
            t.HasCheckConstraint("ck_train_service_unreserved", "unreserved_capacity >= 0");
            t.HasCheckConstraint("ck_train_service_kind", EnumSql.InList<Domain.Scheduling.ServiceKind>("kind"));
            t.HasCheckConstraint("ck_train_service_version", "version >= 1");
        });
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
        b.Property(x => x.TrainNumber).HasMaxLength(10);
        b.Property(x => x.ServiceType).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.ServiceCategory).HasConversion<string>().HasMaxLength(20).HasDefaultValue(ServiceCategory.Regional);
        b.Property(x => x.Direction).HasConversion<string>().HasMaxLength(10);
        b.Property(x => x.RunsOn).HasMaxLength(50);
        b.Property(x => x.UnreservedCapacity).HasDefaultValue(0);
        b.Property(x => x.Kind).HasConversion<string>().HasMaxLength(10).HasDefaultValue(Domain.Scheduling.ServiceKind.Weekly);
        b.Property(x => x.Version).HasDefaultValue(1);
        b.Property(x => x.EffectiveFrom).HasDefaultValue(new DateOnly(2000, 1, 1));
        // A train number has several rows now: versions of a weekly service and one row per date of an extra train (spec 13). Whether two
        // active services of one number overlap in time is checked by the application, inside the create transaction.
        b.HasIndex(x => new { x.TrainNumber, x.Version, x.EffectiveFrom }).IsUnique();
        b.HasIndex(x => new { x.LineId, x.Kind, x.EffectiveFrom });
        b.HasOne<TrainService>().WithMany().HasForeignKey(x => x.SupersededServiceId).OnDelete(DeleteBehavior.NoAction);
        b.HasOne<Line>().WithMany().HasForeignKey(x => x.LineId).OnDelete(DeleteBehavior.NoAction);
        b.HasData(ReferenceData.TrainServices);
    }
}

internal sealed class ServiceStopConfiguration : IEntityTypeConfiguration<ServiceStop>
{
    public void Configure(EntityTypeBuilder<ServiceStop> b)
    {
        b.ToTable("service_stop", t => t.HasCheckConstraint("ck_service_stop_sequence", "sequence > 0"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
        b.Property(x => x.ArrivalReportRequired).HasDefaultValue(false);
        b.Property(x => x.CollectionEligible).HasDefaultValue(false);
        b.HasIndex(x => new { x.ServiceId, x.Sequence }).IsUnique();
        b.HasIndex(x => new { x.ServiceId, x.StationId }).IsUnique();
        b.HasOne<TrainService>().WithMany().HasForeignKey(x => x.ServiceId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Station>().WithMany().HasForeignKey(x => x.StationId).OnDelete(DeleteBehavior.NoAction);
        // Optional planned platform, which must belong to this stop's own station (composite key, MATCH SIMPLE).
        b.HasOne<Platform>().WithMany()
            .HasForeignKey(x => new { x.PlannedPlatformId, x.StationId })
            .HasPrincipalKey(p => new { p.Id, p.StationId })
            .OnDelete(DeleteBehavior.NoAction);
        b.HasData(ReferenceData.ServiceStops);
    }
}
