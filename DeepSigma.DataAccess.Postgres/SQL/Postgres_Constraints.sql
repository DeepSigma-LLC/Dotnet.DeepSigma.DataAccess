
-- Get non-foreign-key constraints from the public schema
SELECT
    tc.constraint_name AS "ConstraintName",
    tc.table_schema   AS "TableSchema",
    tc.table_name     AS "TableName",
    kcu.column_name   AS "ColumnName"
FROM information_schema.table_constraints AS tc
JOIN information_schema.key_column_usage AS kcu
    ON tc.constraint_name = kcu.constraint_name
   AND tc.table_schema = kcu.table_schema
WHERE tc.constraint_type <> 'FOREIGN KEY'
  AND tc.table_schema = 'public';
