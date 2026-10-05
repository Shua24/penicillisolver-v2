using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using penicillisolver_v2.Domain.Entities;

namespace penicillisolver_v2.Data.Configurations;

/// <summary>
/// Maps <see cref="AntibioticAbbreviation"/> to the AntibioticAbbreviations
/// table. The unique index is COMPOSITE — on the upload and the abbreviation
/// together — because the same abbreviation string may carry different meanings
/// in different uploads. A global unique index on the abbreviation alone would
/// silently force one meaning everywhere and lose the others.
/// </summary>
public sealed class AntibioticAbbreviationConfiguration
    : IEntityTypeConfiguration<AntibioticAbbreviation>
{
    /// <summary>The table name used for antibiotic abbreviation mappings.</summary>
    public const string TableName = "AntibioticAbbreviations";

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AntibioticAbbreviation> builder)
    {
        builder.ToTable(TableName);

        builder.HasKey(mapping => mapping.Id);

        builder.Property(mapping => mapping.SpreadsheetUploadId)
            .IsRequired();

        builder.Property(mapping => mapping.Abbreviation)
            .IsRequired()
            .HasMaxLength(MaximumAbbreviationLength);

        builder.Property(mapping => mapping.FullName)
            .IsRequired()
            .HasMaxLength(MaximumFullNameLength);

        builder.Property(mapping => mapping.CreatedByUserId)
            .IsRequired()
            .HasMaxLength(MaximumUserIdLength);

        builder.Property(mapping => mapping.CreatedAtUtc)
            .IsRequired();

        builder.Property(mapping => mapping.LastModifiedByUserId)
            .IsRequired()
            .HasMaxLength(MaximumUserIdLength);

        builder.Property(mapping => mapping.LastModifiedAtUtc)
            .IsRequired();

        // Composite uniqueness: one meaning per abbreviation PER UPLOAD.
        builder.HasIndex(mapping => new { mapping.SpreadsheetUploadId, mapping.Abbreviation })
            .IsUnique()
            .HasDatabaseName("IX_AntibioticAbbreviations_Upload_Abbreviation");

        builder.HasOne(mapping => mapping.SpreadsheetUpload)
            .WithMany(upload => upload.AntibioticAbbreviations)
            .HasForeignKey(mapping => mapping.SpreadsheetUploadId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private const int MaximumAbbreviationLength = 100;
    private const int MaximumFullNameLength = 300;
    private const int MaximumUserIdLength = 450;
}
