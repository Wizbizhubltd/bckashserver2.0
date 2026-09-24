-- Generates (does not run) the SQL that copies every table from the legacy staging database
-- (@src, a raw import of the phpMyAdmin dump) into the EF-migrated app database (@dst).
-- scripts/seed-legacy-dump.sh prepends `SET @src=...; SET @dst=...;` and pipes the output back
-- into the server.
--
-- The legacy and EF schemas share table/column names; EF only adds columns. Per column:
--   * present in both                      -> copied as-is (legacy ids kept, so FKs line up)
--   * nullable in legacy, NOT NULL in EF   -> COALESCE(col, <EF default | 0 | ''>)
--   * EF-only, nullable                    -> NULL
--   * EF-only, NOT NULL                    -> EF default, else 1 for `active` flags, else 0
-- Every EF table except __EFMigrationsHistory is truncated first. Tables the startup seeders
-- own (roles/permissions/role_permissions/countries/...) are refilled by those seeders on the
-- next API boot, since they only insert what's missing.
SET SESSION group_concat_max_len = 1000000;

SELECT 'SET SESSION foreign_key_checks=0; SET SESSION unique_checks=0; SET SESSION sql_mode=''NO_AUTO_VALUE_ON_ZERO'';';

SELECT CONCAT('TRUNCATE TABLE `', @dst, '`.`', table_name, '`;')
FROM information_schema.tables
WHERE table_schema = @dst AND table_name <> '__EFMigrationsHistory';

SELECT CONCAT(
    'SELECT ''copying ', e.table_name, ''' AS progress; ',
    'INSERT INTO `', @dst, '`.`', e.table_name, '` (',
    GROUP_CONCAT(CONCAT('`', e.column_name, '`') ORDER BY e.ordinal_position),
    ') SELECT ',
    GROUP_CONCAT(CASE
        WHEN l.column_name IS NULL THEN
            CASE WHEN e.is_nullable = 'YES' THEN 'NULL'
                 WHEN e.column_default IS NOT NULL THEN e.column_default
                 WHEN e.column_name = 'active' THEN '1'
                 ELSE '0' END
        WHEN l.is_nullable = 'YES' AND e.is_nullable = 'NO' THEN
            CONCAT('COALESCE(`', l.column_name, '`, ',
                   COALESCE(e.column_default, IF(e.data_type IN ('varchar', 'char', 'text', 'longtext'), '''''', '0')), ')')
        ELSE CONCAT('`', l.column_name, '`') END
        ORDER BY e.ordinal_position),
    ' FROM `', @src, '`.`', e.table_name, '`; COMMIT;')
FROM information_schema.columns e
LEFT JOIN information_schema.columns l
    ON l.table_schema = @src AND l.table_name = e.table_name AND l.column_name = e.column_name
WHERE e.table_schema = @dst
  AND e.table_name IN (SELECT table_name FROM information_schema.tables WHERE table_schema = @src)
GROUP BY e.table_name;
