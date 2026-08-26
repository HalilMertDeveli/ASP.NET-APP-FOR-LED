-- LED Support: support_messages (run in Supabase SQL Editor)
-- Backend uses service_role key; no public anon insert.

create extension if not exists pgcrypto;

create table if not exists public.support_messages (
  id uuid primary key default gen_random_uuid(),
  name text not null,
  email text not null,
  phone text,
  company text,
  system text not null,
  subject text not null,
  message text not null,
  client_ip text,
  user_agent text,
  status text not null default 'new',
  email_sent boolean not null default false,
  email_error text,
  created_at timestamptz not null default now()
);

create index if not exists support_messages_created_at_idx
  on public.support_messages (created_at desc);

alter table public.support_messages enable row level security;

-- Intentionally no anon/authenticated policies: only service_role from ASP.NET.
revoke all on table public.support_messages from anon, authenticated;
grant select, insert, update, delete on table public.support_messages to service_role;
