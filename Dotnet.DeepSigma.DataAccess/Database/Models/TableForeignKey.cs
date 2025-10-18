

namespace DeepSigma.DataAccess.Database.Models;

/// <summary>
/// Represents a foreign key constraint in a database table.
/// </summary>
public class TableForeignKey()
{
     public string? CONSTRAINT_NAME { get; set; }
     public string? ForeignColumnName { get; set; }
     public string? PrimaryTableSchema { get; set; }
     public string? PrimaryTableName { get; set; }
     public string? PrimaryColumnName { get; set; }
}
    
