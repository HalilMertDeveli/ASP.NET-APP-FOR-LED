-- Applied remotely as customer_profiles_messaging (+ harden_account_grants_admin_emails_rls).
-- Snapshot for the repo. Do not put secrets here.

create schema if not exists private;

create table if not exists private.admin_emails (
  email text primary key
);

insert into private.admin_emails (email)
values ('halilmertdeveliii@gmail.com')
on conflict (email) do nothing;

alter table private.admin_emails enable row level security;
revoke all on table private.admin_emails from public, anon, authenticated;
grant all on table private.admin_emails to postgres, service_role;

create table if not exists public.profiles (
  id uuid primary key references auth.users (id) on delete cascade,
  email text not null,
  full_name text not null default '',
  avatar_url text,
  role text not null default 'customer' check (role in ('customer', 'admin')),
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now()
);

create table if not exists public.conversations (
  id uuid primary key default gen_random_uuid(),
  customer_id uuid not null unique references public.profiles (id) on delete cascade,
  status text not null default 'open' check (status in ('open', 'closed')),
  last_message text,
  last_message_at timestamptz,
  last_sender_role text,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now()
);

create table if not exists public.messages (
  id uuid primary key default gen_random_uuid(),
  conversation_id uuid not null references public.conversations (id) on delete cascade,
  sender_id uuid references public.profiles (id) on delete cascade,
  sender_role text not null check (sender_role in ('customer', 'admin')),
  body text not null check (char_length(body) between 1 and 4000),
  created_at timestamptz not null default now(),
  read_at timestamptz
);

create index if not exists messages_conversation_created_idx
  on public.messages (conversation_id, created_at);

alter table public.profiles enable row level security;
alter table public.conversations enable row level security;
alter table public.messages enable row level security;

revoke all on table public.profiles from anon, authenticated;
revoke all on table public.conversations from anon, authenticated;
revoke all on table public.messages from anon, authenticated;

grant select, update on table public.profiles to authenticated;
grant select, insert, update on table public.conversations to authenticated;
grant select, insert, update on table public.messages to authenticated;
