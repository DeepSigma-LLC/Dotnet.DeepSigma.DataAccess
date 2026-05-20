
-- Gets table names from the public schema
SELECT
    table_schema AS "TableSchema",
    table_name AS "Name"
FROM information_schema.tables
WHERE table_type = 'BASE TABLE' AND table_schema = 'public'
ORDER BY table_name;
