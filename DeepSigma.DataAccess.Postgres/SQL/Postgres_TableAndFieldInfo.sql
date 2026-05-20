
-- Gets tables and column info from the public schema, including the default-value expression where present.
SELECT
    table_schema             AS "TableSchema",
    table_name               AS "TableName",
    column_name              AS "ColumnName",
    data_type                AS "DataType",
    character_maximum_length AS "CharacterMaximumLength",
    numeric_precision        AS "NumericPrecision",
    is_nullable              AS "IsNullable",
    column_default           AS "ColumnDefault"
FROM information_schema.columns
WHERE table_schema = 'public'
ORDER BY table_name, ordinal_position;
