
namespace DeepSigma.DataAccess.Database.Models;

/// <summary>
/// Represents a constraint on a database table.
/// </summary>
public class TableConstraint()
{
    public string? CONSTRAINT_NAME { get; set; }
    public string? TABLE_SCHEMA { get; set; } 
    public string? TABLE_NAME { get; set; }
}


