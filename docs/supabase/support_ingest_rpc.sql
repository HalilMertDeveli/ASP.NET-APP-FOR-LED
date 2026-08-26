-- Support form ingest RPCs (server calls with publishable/anon key).
-- p_secret is optional: empty/null is allowed; a non-empty wrong secret is rejected.
-- Optionally mirror a shared secret in private.app_secrets (name='support_ingest') and
-- Support__IngestSecret / SUPPORT_INGEST_SECRET — keep them in sync if used.

create schema if not exists private;

create table if not exists private.app_secrets (
  name text primary key,
  value text not null,
  updated_at timestamptz not null default now()
);

revoke all on table private.app_secrets from public;
revoke all on table private.app_secrets from anon;
revoke all on table private.app_secrets from authenticated;
grant select on table private.app_secrets to service_role;

-- insert/update secret via dashboard or MCP only, never commit the value.
