
-- Gets columns for every user-defined table by joining sqlite_master to the
-- pragma_table_info table-valued function (SQLite 3.16+).
-- Notes on the projection:
--   * SQLite is dynamically typed; the `type` column is a hint, not enforced.
--   * Length / precision are not concepts in SQLite, so we return NULL.
--   * `notnull` is 0/1 in pragma_table_info; we map it to the YES/NO strings
--     used by the other providers' IsNullable convention.
SELECT
    'main'                                                  AS TableSchema,
    m.name                                                  AS TableName,
    p.name                                                  AS ColumnName,
    p.type                                                  AS DataType,
    NULL                                                    AS CharacterMaximumLength,
    NULL                                                    AS NumericPrecision,
    CASE p."notnull" WHEN 0 THEN 'YES' ELSE 'NO' END        AS IsNullable,
    p.dflt_value                                            AS ColumnDefault
FROM sqlite_master m
JOIN pragma_table_info(m.name) p
WHERE m.type = 'table'
  AND m.name NOT LIKE 'sqlite_%'
ORDER BY m.name, p.cid;
