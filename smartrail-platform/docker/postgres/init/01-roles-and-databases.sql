-- Runs once, on a brand-new Postgres volume, as the superuser (POSTGRES_USER). Development-only passwords.
-- The integration tests run this same file once per test container (see tests/SmartRail.IntegrationTests).
--
--   smartrail_app  the API's runtime role: DML only, no DDL. 02-smartrail-privileges.sql gives it its table privileges.
--   keycloak       owns the empty keycloak database for the later Keycloak task (ADR-08). It has no rights on SmartRail's
--                  database, and SmartRail's migrations never touch the keycloak database.

CREATE ROLE smartrail_app LOGIN PASSWORD 'smartrail-app-dev-only';
CREATE ROLE keycloak LOGIN PASSWORD 'keycloak-dev-only';

CREATE DATABASE keycloak OWNER keycloak;
REVOKE CONNECT ON DATABASE keycloak FROM PUBLIC;
