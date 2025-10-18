
--Gets tables and column info
SELECT 
	TABLE_SCHEMA TableSchema, 
	TABLE_NAME TableName, 
	COLUMN_NAME ColumnName, 
	DATA_TYPE DataType, 
	CHARACTER_MAXIMUM_LENGTH CharacterMaximumLength, 
	NUMERIC_PRECISION NumericPrecision,
	IS_NULLABLE IsNullable, 
	Case 
		When COLUMN_DEFAULT is null Then 0
		When len(COLUMN_DEFAULT) > 0 Then 1
		Else 0
	End ColumnDefault
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'dbo'
Order by TABLE_NAME
