

namespace DeepSigma.DataAccess.Database.Models;

public record class TableForeignKey(
     string CONSTRAINT_NAME,
     string TABLE_SCHEMA,
     string ForeignTableName,
     string ForeignColumnName,
     string PrimaryTableSchema,
     string PrimaryTableName,
     string PrimaryColumnName
    );
