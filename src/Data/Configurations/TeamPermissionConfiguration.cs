using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using penicillisolver_v2.Domain.Entities;

namespace penicillisolver_v2.Data.Configurations;

/// <summary>
/// Maps <see cref="TeamPermission"/> to the TeamPermissions table. The team
/// name carries a unique index because exactly one row is allowed per team.
/// </summary>
public sealed class TeamPermissionConfiguration : IEntityTypeConfiguration<TeamPermission>
{
    /// <summary>The table name used for per-team permissions.</summary>
    public const string TableName = "TeamPermissions";

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<TeamPermission> builder)
    {
        builder.ToTable(TableName);

        builder.HasKey(permission => permission.Id);

        builder.Property(permission => permission.TeamName)
            .IsRequired()
            .HasMaxLength(MaximumTeamNameLength);

        builder.Property(permission => permission.CanUpdateSpreadsheet)
            .IsRequired();

        builder.Property(permission => permission.CanDeleteSpreadsheet)
            .IsRequired();

        builder.Property(permission => permission.LastModifiedByUserId)
            .IsRequired()
            .HasMaxLength(MaximumUserIdLength);

        builder.Property(permission => permission.LastModifiedAtUtc)
            .IsRequired();

        builder.HasIndex(permission => permission.TeamName)
            .IsUnique()
            .HasDatabaseName("IX_TeamPermissions_TeamName");
    }

    private const int MaximumTeamNameLength = 200;
    private const int MaximumUserIdLength = 450;
}
