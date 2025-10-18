

/*Get constraints*/
  SELECT
        tc.CONSTRAINT_NAME ConstraintName,
        tc.TABLE_SCHEMA TableSchema,
        tc.TABLE_NAME TableName,
        kcu.COLUMN_NAME ColumnName
    FROM
        INFORMATION_SCHEMA.TABLE_CONSTRAINTS AS tc
    JOIN
        INFORMATION_SCHEMA.KEY_COLUMN_USAGE AS kcu
        ON tc.CONSTRAINT_NAME = kcu.CONSTRAINT_NAME
    WHERE
        tc.CONSTRAINT_TYPE != 'Foreign KEY'