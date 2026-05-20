
-- Get foreign key constraints from the public schema
SELECT
    fk.constraint_name        AS "ConstraintName",
    kcu.column_name           AS "ForeignColumnName",
    pk.table_schema           AS "PrimaryTableSchema",
    pk.table_name             AS "PrimaryTableName",
    pk_kcu.column_name        AS "PrimaryColumnName"
FROM information_schema.referential_constraints AS fk
JOIN information_schema.key_column_usage AS kcu
    ON fk.constraint_name = kcu.constraint_name
   AND fk.constraint_schema = kcu.constraint_schema
JOIN information_schema.table_constraints AS pk
    ON fk.unique_constraint_name = pk.constraint_name
   AND fk.unique_constraint_schema = pk.constraint_schema
JOIN information_schema.key_column_usage AS pk_kcu
    ON pk.constraint_name = pk_kcu.constraint_name
   AND pk.constraint_schema = pk_kcu.constraint_schema
   AND kcu.ordinal_position = pk_kcu.ordinal_position
WHERE pk.table_schema = 'public'
ORDER BY pk.table_name;
