# Stack: ASP.NET Core + Supabase + Vercel

Firebase is not used. Auth, data, and realtime all go through Supabase.

| Area | Store |
|------|--------|
| Contact form | `public.support_messages` (service_role, server-only) |
| Customer accounts | Supabase Auth (Google) + `public.profiles` |
| Support chat | `public.conversations`, `public.messages` |
| Live updates | Supabase Realtime on `messages` |
| Mail | Resend API from ASP.NET |

SQL:

- `docs/supabase/support_messages.sql`
- `docs/supabase/account_messaging.sql`
