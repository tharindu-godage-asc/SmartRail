-- Runs in the SmartRail database (Compose: POSTGRES_DB; tests: every fresh test database), as the admin user that
-- later runs the migrations. Default privileges are per database, which is why this is a separate file.
--
-- Every table the admin user creates from now on is usable by the API role without a grant in the migration (rule R9).
-- Migration 0001 then revokes what must be narrower: UPDATE and DELETE on train_event, writes on __EFMigrationsHistory.
-- TRUNCATE and DDL are never granted.

DO $$
BEGIN
    -- Only the API role may connect to this database (the keycloak role cannot).
    EXECUTE format('REVOKE CONNECT ON DATABASE %I FROM PUBLIC', current_database());
    EXECUTE format('GRANT CONNECT ON DATABASE %I TO smartrail_app', current_database());
END
$$;

GRANT USAGE ON SCHEMA public TO smartrail_app;

ALTER DEFAULT PRIVILEGES FOR ROLE CURRENT_USER IN SCHEMA public
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO smartrail_app;
