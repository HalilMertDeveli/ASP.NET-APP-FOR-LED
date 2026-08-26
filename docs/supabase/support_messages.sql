-- LED Support: support_messages
-- Backend uses service_role key; no public anon insert.

create table if not exists public.support_messages (
  id uuid primary key default gen_random_uuid(),
  idempotency_key uuid unique,
  name text not null check (char_length(name) between 2 and 120),
  email text not null check (char_length(email) between 3 and 254),
  phone text check (phone is null or char_length(phone) between 7 and 40),
  company text check (company is null or char_length(company) <= 160),
  system text not null check (char_length(system) between 1 and 40),
  subject text not null check (char_length(subject) between 3 and 200),
  message text not null check (char_length(message) between 20 and 4000),
  client_ip text check (client_ip is null or char_length(client_ip) <= 64),
  user_agent text check (user_agent is null or char_length(user_agent) <= 512),
  email_status text not null default 'pending' check (email_status in ('pending', 'sending', 'sent', 'failed')),
  email_sent_at timestamptz,
  error_message text,
  created_at timestamptz not null default now()
);

create index if not exists support_messages_created_at_idx
  on public.support_messages (created_at desc);

alter table public.support_messages enable row level security;

revoke all on table public.support_messages from anon, authenticated;
grant select, insert, update, delete on table public.support_messages to service_role;
