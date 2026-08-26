# Firebase → Supabase migration status

## Firebase usage (historical)

| Area | Purpose | Action |
|------|---------|--------|
| Firestore `supportRequests` | Persist contact/support form | Replaced by Supabase `support_messages` |
| Cloud Functions (`functions/`) | Optional Function-mode submit + Resend | Removed; Direct path only |
| Firebase Admin / Web config | Project + credentials | Removed from ASP.NET app |
| Auth / Storage | Not used by the live site | N/A |

## Data migration

- This environment has **no** Firebase service-account credentials.
- Existing Firestore documents were **not** deleted and **not** auto-migrated.
- If historical tickets are needed, export from Firebase Console and import into Supabase separately.

## New store of record

- Table: `public.support_messages` (see `support_messages.sql`)
- Writer: ASP.NET `SupabaseSupportRequestStore` using **service_role** key (server-only)
