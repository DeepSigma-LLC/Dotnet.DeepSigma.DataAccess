
-- Gets tables and column info from the public schema
SELECT
    table_schema AS "TableSchema",
    column_name AS "ColumnName",
    data_type AS "DataType",
    character_maximum_length AS "CharacterMaximumLength",
    numeric_precision AS "NumericPrecision",
    is_nullable AS "IsNullable",
    CASE
        WHEN column_default IS NULL THEN 0
        WHEN length(column_default) > 0 THEN 1
        ELSE 0
    END AS "ColumnDefault"
FROM information_schema.columns
WHERE table_schema = 'public'
ORDER BY table_name;
