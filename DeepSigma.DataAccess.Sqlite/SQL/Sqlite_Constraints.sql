
-- Get non-foreign-key constraints (primary key + user-defined unique indexes).
-- SQLite does not expose CHECK constraints as a queryable list; they live only in the
-- CREATE TABLE text. Also, SQLite-generated indexes (origin = 'pk' / 'c') are not
-- "constraints" in the same sense as user-declared UNIQUEs and are excluded.
--
-- Primary keys come from pragma_table_info.pk (> 0 for PK columns).
-- Unique constraints come from pragma_index_list rows where origin = 'u' (user-declared
-- via a UNIQUE clause in CREATE TABLE), joined to pragma_index_info for the column name.

SELECT
    NULL              AS ConstraintName,
    'PRIMARY KEY'     AS ConstraintType,
    'main'            AS TableSchema,
    m.name            AS TableName,
    p.name            AS ColumnName
FROM sqlite_master m
JOIN pragma_table_info(m.name) p
WHERE m.type = 'table'
  AND m.name NOT LIKE 'sqlite_%'
  AND p.pk > 0

UNION ALL

SELECT
    i.name            AS ConstraintName,
    'UNIQUE'          AS ConstraintType,
    'main'            AS TableSchema,
    m.name            AS TableName,
    ic.name           AS ColumnName
FROM sqlite_master m
JOIN pragma_index_list(m.name) i
JOIN pragma_index_info(i.name) ic
WHERE m.type = 'table'
  AND m.name NOT LIKE 'sqlite_%'
  AND i.origin = 'u'

ORDER BY TableName, ColumnName;
