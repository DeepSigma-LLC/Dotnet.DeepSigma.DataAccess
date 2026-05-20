
-- Get foreign key constraints for every user-defined table.
-- SQLite foreign keys are anonymous (no user-given name), so ConstraintName is NULL.
-- Foreign keys are *defined* in CREATE TABLE regardless of whether enforcement is on;
-- enforcement requires `PRAGMA foreign_keys = ON` at the connection level.
SELECT
    NULL          AS ConstraintName,
    fk."from"     AS ForeignColumnName,
    'main'        AS PrimaryTableSchema,
    fk."table"    AS PrimaryTableName,
    fk."to"       AS PrimaryColumnName
FROM sqlite_master m
JOIN pragma_foreign_key_list(m.name) fk
WHERE m.type = 'table'
  AND m.name NOT LIKE 'sqlite_%'
ORDER BY m.name, fk.seq;
