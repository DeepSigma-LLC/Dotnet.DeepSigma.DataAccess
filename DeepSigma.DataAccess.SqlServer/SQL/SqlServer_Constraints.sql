
/* Get non-foreign-key constraints (primary key, unique, check). */
SELECT
    tc.CONSTRAINT_NAME  AS ConstraintName,
    tc.CONSTRAINT_TYPE  AS ConstraintType,
    tc.TABLE_SCHEMA     AS TableSchema,
    tc.TABLE_NAME       AS TableName,
    kcu.COLUMN_NAME     AS ColumnName
FROM
    INFORMATION_SCHEMA.TABLE_CONSTRAINTS AS tc
LEFT JOIN
    INFORMATION_SCHEMA.KEY_COLUMN_USAGE AS kcu
    ON tc.CONSTRAINT_NAME = kcu.CONSTRAINT_NAME
   AND tc.TABLE_SCHEMA   = kcu.TABLE_SCHEMA
WHERE
    tc.CONSTRAINT_TYPE <> 'FOREIGN KEY'
