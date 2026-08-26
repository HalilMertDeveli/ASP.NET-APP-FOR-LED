-- Support form ingest RPCs (server calls with Support:IngestSecret).
-- Do NOT put the real secret in git. Set it in Vercel as Support__IngestSecret / SUPPORT_INGEST_SECRET
-- and mirror it in private.app_secrets where name='support_ingest'.

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
