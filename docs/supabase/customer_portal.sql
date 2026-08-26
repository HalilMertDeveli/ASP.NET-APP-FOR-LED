-- Customer portal: profiles extras, requests, per-request conversations, realtime.

alter table public.profiles
  add column if not exists phone text,
  add column if not exists company text,
  add column if not exists last_login_at timestamptz;

alter table public.profiles
  drop constraint if exists profiles_phone_check;
alter table public.profiles
  add constraint profiles_phone_check
  check (phone is null or char_length(phone) between 7 and 40);

alter table public.profiles
  drop constraint if exists profiles_company_check;
alter table public.profiles
  add constraint profiles_company_check
  check (company is null or char_length(company) <= 160);

create table if not exists public.customer_requests (
  id uuid primary key default gen_random_uuid(),
  customer_id uuid not null references public.profiles (id) on delete cascade,
  subject text not null check (char_length(subject) between 3 and 200),
  description text not null check (char_length(description) between 20 and 4000),
  category text not null default 'genel' check (char_length(category) between 1 and 40),
  system text check (system is null or char_length(system) between 1 and 40),
  phone text check (phone is null or char_length(phone) between 7 and 40),
  company text check (company is null or char_length(company) <= 160),
  status text not null default 'open' check (status in ('open', 'in_progress', 'waiting_customer', 'resolved', 'closed')),
  priority text not null default 'normal' check (priority in ('low', 'normal', 'high')),
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now(),
  closed_at timestamptz
);

create index if not exists customer_requests_customer_idx
  on public.customer_requests (customer_id, created_at desc);
create index if not exists customer_requests_status_idx
  on public.customer_requests (status, created_at desc);

alter table public.conversations
  add column if not exists request_id uuid references public.customer_requests (id) on delete cascade;

alter table public.conversations
  drop constraint if exists conversations_customer_id_key;

create unique index if not exists conversations_request_id_key
  on public.conversations (request_id);

alter table public.messages
  add column if not exists message_type text not null default 'text';

alter table public.messages
  drop constraint if exists messages_message_type_check;
alter table public.messages
  add constraint messages_message_type_check
  check (message_type = 'text');

create or replace function public.handle_new_user()
returns trigger
language plpgsql
security definer
set search_path to 'public', 'private'
as $$
declare
  assigned_role text := 'customer';
begin
  if exists (
    select 1 from private.admin_emails
    where lower(email) = lower(coalesce(new.email, ''))
  ) then
    assigned_role := 'admin';
  end if;

  insert into public.profiles (id, email, full_name, avatar_url, role, last_login_at)
  values (
    new.id,
    coalesce(new.email, ''),
    coalesce(
      new.raw_user_meta_data->>'full_name',
      new.raw_user_meta_data->>'name',
      split_part(coalesce(new.email, 'musteri'), '@', 1)
    ),
    coalesce(
      new.raw_user_meta_data->>'avatar_url',
      new.raw_user_meta_data->>'picture'
    ),
    assigned_role,
    now()
  )
  on conflict (id) do update
    set email = excluded.email,
        full_name = coalesce(nullif(excluded.full_name, ''), public.profiles.full_name),
        avatar_url = coalesce(excluded.avatar_url, public.profiles.avatar_url),
        updated_at = now();

  return new;
end;
$$;

create or replace function public.protect_profile_role()
returns trigger
language plpgsql
as $$
begin
  if tg_op = 'UPDATE' and coalesce(auth.role(), '') <> 'service_role' then
    new.id := old.id;
    new.email := old.email;
    new.role := old.role;
    new.created_at := old.created_at;
  end if;
  new.updated_at := now();
  return new;
end;
$$;

create or replace function public.handle_new_request()
returns trigger
language plpgsql
security definer
set search_path to 'public'
as $$
begin
  insert into public.conversations (customer_id, request_id, status)
  values (new.customer_id, new.id, 'open')
  on conflict (request_id) do nothing;
  return new;
end;
$$;

drop trigger if exists on_customer_request_created on public.customer_requests;
create trigger on_customer_request_created
  after insert on public.customer_requests
  for each row execute function public.handle_new_request();

create or replace function public.touch_request_updated()
returns trigger
language plpgsql
as $$
begin
  new.updated_at := now();
  if new.status in ('resolved', 'closed') and old.status not in ('resolved', 'closed') then
    new.closed_at := coalesce(new.closed_at, now());
  end if;
  if new.status in ('open', 'in_progress', 'waiting_customer') then
    new.closed_at := null;
  end if;
  return new;
end;
$$;

drop trigger if exists customer_requests_touch on public.customer_requests;
create trigger customer_requests_touch
  before update on public.customer_requests
  for each row execute function public.touch_request_updated();

alter table public.customer_requests enable row level security;
revoke all on table public.customer_requests from anon, authenticated;
grant select, insert, update on table public.customer_requests to authenticated;
grant all on table public.customer_requests to service_role;

drop policy if exists customer_requests_select on public.customer_requests;
create policy customer_requests_select on public.customer_requests
  for select using (customer_id = auth.uid() or private.is_admin());

drop policy if exists customer_requests_insert on public.customer_requests;
create policy customer_requests_insert on public.customer_requests
  for insert with check (customer_id = auth.uid());

drop policy if exists customer_requests_update on public.customer_requests;
create policy customer_requests_update on public.customer_requests
  for update using (customer_id = auth.uid() or private.is_admin())
  with check (customer_id = auth.uid() or private.is_admin());

alter table public.messages replica identity full;

do $$
begin
  if not exists (
    select 1
    from pg_publication_tables
    where pubname = 'supabase_realtime'
      and schemaname = 'public'
      and tablename = 'messages'
  ) then
    execute 'alter publication supabase_realtime add table public.messages';
  end if;
end $$;
