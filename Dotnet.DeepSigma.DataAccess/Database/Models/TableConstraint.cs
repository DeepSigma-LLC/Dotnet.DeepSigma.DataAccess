
namespace DeepSigma.DataAccess.Database.Models;

public record class TableConstraint
(
    string CONSTRAINT_NAME,
    string TABLE_SCHEMA,
    string TABLE_NAME,
    string COLUMN_NAME
);
