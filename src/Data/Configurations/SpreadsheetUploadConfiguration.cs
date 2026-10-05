using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using penicillisolver_v2.Domain.Entities;

namespace penicillisolver_v2.Data.Configurations;

/// <summary>
/// Maps <see cref="SpreadsheetUpload"/> to the SharedSpreadsheetUploads table,
/// fixing the maximum length of every string column and the required fields.
/// </summary>
public sealed class SpreadsheetUploadConfiguration : IEntityTypeConfiguration<SpreadsheetUpload>
{
    /// <summary>The table name used for spreadsheet uploads.</summary>
    public const string TableName = "SpreadsheetUploads";

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<SpreadsheetUpload> builder)
    {
        builder.ToTable(TableName);

        builder.HasKey(upload => upload.Id);

        builder.Property(upload => upload.OriginalFileName)
            .IsRequired()
            .HasMaxLength(MaximumFileNameLength);

        builder.Property(upload => upload.StoredFilePath)
            .IsRequired()
            .HasMaxLength(MaximumStoredPathLength);

        builder.Property(upload => upload.ContentHash)
            .IsRequired()
            .HasMaxLength(MaximumContentHashLength);

        builder.Property(upload => upload.UploadedAtUtc)
            .IsRequired();

        builder.Property(upload => upload.UploadedByUserId)
            .IsRequired()
            .HasMaxLength(MaximumUserIdLength);

        builder.Property(upload => upload.FileFormat)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(MaximumEnumNameLength);

        builder.Property(upload => upload.Orientation)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(MaximumEnumNameLength);

        builder.Property(upload => upload.OrganismCount)
            .IsRequired();

        builder.Property(upload => upload.AntibioticCount)
            .IsRequired();

        builder.HasIndex(upload => upload.ContentHash)
            .HasDatabaseName("IX_SpreadsheetUploads_ContentHash");

        builder.HasIndex(upload => upload.UploadedAtUtc)
            .HasDatabaseName("IX_SpreadsheetUploads_UploadedAtUtc");
    }

    private const int MaximumFileNameLength = 260;
    private const int MaximumStoredPathLength = 512;
    private const int MaximumContentHashLength = 128;
    private const int MaximumUserIdLength = 450;
    private const int MaximumEnumNameLength = 40;
}
