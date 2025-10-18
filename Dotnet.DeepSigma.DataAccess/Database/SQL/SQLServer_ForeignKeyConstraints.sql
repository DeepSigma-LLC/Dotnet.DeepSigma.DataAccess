
 /*Get foriegn key constraints*/
   SELECT
        fk.CONSTRAINT_NAME,
        --fk.TABLE_SCHEMA AS ForeignTableSchema,
        --fk.TABLE_NAME AS ForeignTableName,
        kcu.COLUMN_NAME AS ForeignColumnName,
        pk.TABLE_SCHEMA AS PrimaryTableSchema,
        pk.TABLE_NAME AS PrimaryTableName,
        pk_kcu.COLUMN_NAME AS PrimaryColumnName
    FROM
        INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS AS fk
    JOIN
        INFORMATION_SCHEMA.KEY_COLUMN_USAGE AS kcu
        ON fk.CONSTRAINT_NAME = kcu.CONSTRAINT_NAME
    JOIN
        INFORMATION_SCHEMA.TABLE_CONSTRAINTS AS pk
        ON fk.UNIQUE_CONSTRAINT_NAME = pk.CONSTRAINT_NAME
    JOIN
        INFORMATION_SCHEMA.KEY_COLUMN_USAGE AS pk_kcu
        ON pk.CONSTRAINT_NAME = pk_kcu.CONSTRAINT_NAME
        AND kcu.ORDINAL_POSITION = pk_kcu.ORDINAL_POSITION
     Order by PrimaryTableName