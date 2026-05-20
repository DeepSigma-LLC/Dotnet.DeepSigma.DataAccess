
-- Gets user-defined table names. SQLite has no real schema concept; we report 'main'
-- (the default attached-database name) for parity with the other providers.
SELECT
    'main' AS TableSchema,
    name   AS Name
FROM sqlite_master
WHERE type = 'table'
  AND name NOT LIKE 'sqlite_%'
ORDER BY name;
