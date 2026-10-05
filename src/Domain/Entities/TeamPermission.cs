namespace penicillisolver_v2.Domain.Entities;

/// <summary>
/// The per-team permissions record. Exactly one row exists per team, holding
/// whether that team may update the shared spreadsheet and whether it may
/// delete it, together with an audit trail of the last change.
/// </summary>
public class TeamPermission
{
    /// <summary>The database generated primary key.</summary>
    public int Id { get; set; }

    /// <summary>The name of the team this row describes. Unique across all rows.</summary>
    public string TeamName { get; set; } = string.Empty;

    /// <summary>Whether the team may replace the shared spreadsheet.</summary>
    public bool CanUpdateSpreadsheet { get; set; }

    /// <summary>Whether the team may delete the shared spreadsheet.</summary>
    public bool CanDeleteSpreadsheet { get; set; }

    /// <summary>The identifier of the account that most recently changed this row.</summary>
    public string LastModifiedByUserId { get; set; } = string.Empty;

    /// <summary>When this row was most recently changed, in coordinated universal time.</summary>
    public DateTimeOffset LastModifiedAtUtc { get; set; }
}
