
--Gets tables and column info, including the default-value expression where present.
SELECT
	TABLE_SCHEMA              AS TableSchema,
	TABLE_NAME                AS TableName,
	COLUMN_NAME               AS ColumnName,
	DATA_TYPE                 AS DataType,
	CHARACTER_MAXIMUM_LENGTH  AS CharacterMaximumLength,
	NUMERIC_PRECISION         AS NumericPrecision,
	IS_NULLABLE               AS IsNullable,
	COLUMN_DEFAULT            AS ColumnDefault
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'dbo'
ORDER BY TABLE_NAME, ORDINAL_POSITION
