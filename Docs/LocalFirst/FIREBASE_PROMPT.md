# 🔥 FIREBASE STUDIO (SQL CONNECT) — SCHEMA GENERATOR PROMPTU

> **İstifadə:** Firebase Studio → **SQL Connect** → *Schema generator* →
> aşağıdaki mətni **olduğu kimi** yapışdırın (kopyala ✓) → **Generate schema** ✓
>
> ⚠ Prompt **ingilis dilindədir** ✓ — Gemini ən dəqiq nəticəni belə verir ✓
> (Aşağıda Azərbaycanca izahı da var ✓)

---

## 📋 COPY-PASTE PROMPT (İNGİLİSCƏ) ✓

```text
Design a production-grade PostgreSQL (Supabase / Firebase SQL Connect) schema
for a LOCAL-FIRST VEHICLE DEALERSHIP MANAGEMENT SYSTEM (car dealership ERP).

Business domain (real workflows the app supports):
- Buy cars, track purchase price, expenses per car, cost basis, sale price, status.
- Sell cars cash, by installment CREDIT (down payment + monthly payments),
  by BARTER (car exchanged for car) or by TRANSFER (car handed to a partner person).
- Credit portfolio: financed amount, interest, full payable, monthly installment,
  paid-to-date, remaining debt, closed / early-closed credits.
- Extra credit income/expense ledger: income payments, DELAY PENALTIES
  (paid / unpaid), barter, transfer, EARLY CREDIT CLOSURE.
- Partner profit sharing: every sale / credit / ledger entry is split between
  partners by percent (percent partners) or equally (residual partners).
- Partner payouts (money given to partners), partner cards and ledger journal.
- Documents & media archive: PDFs, invoices, contracts, photos attached to
  a car, a credit or a ledger entry (stored on an external USB drive).
- Automatic synchronisation between: local SQLite (desktop), external USB SQLite
  and this cloud PostgreSQL. Two-way sync, conflict resolution by last-write-wins.
- Users with roles (admin / manager / basic), DATA USB device registration.
- Trash / restore (deleted records must be recoverable).
- Filters and reports by date range: daily, monthly, custom periods, all-time.

HARD REQUIREMENTS (apply to EVERY table, no exceptions):
1. Primary key:  id uuid PRIMARY KEY DEFAULT gen_random_uuid()
2. Audit columns on EVERY table:
   updated_at  timestamptz NOT NULL DEFAULT now(),
   updated_by  text        NOT NULL DEFAULT '',
   is_deleted  boolean     NOT NULL DEFAULT false,
   deleted_at  timestamptz NULL
   -> NEVER physically delete rows. Deletion = is_deleted = true + deleted_at.
   -> ALL application reads must filter is_deleted = false.
3. Money: numeric(14,2). Percents: numeric(9,4). Dates: timestamptz (UTC).
   Text: text NOT NULL DEFAULT '' (never nullable strings unless business-null).
4. snake_case names, plural table names, singular column names.
5. Foreign keys with ON DELETE SET NULL, ON UPDATE CASCADE.
6. Indexes: every table gets (is_deleted, updated_at);
   plus foreign key indexes and frequently filtered columns
   (status, dates, registration plate, phone, contract number).
7. Add a UNIQUE constraint where business-unique:
   car registration plate, contract number, partner name, username,
   (ref_type, ref_id, partner_id) inside partner_shares, sync_state tuple.
8. Add CHECK constraints: status enums (use CHECK IN (...)), non-negative amounts,
   percent between 0 and 100, ref_type limited to allowed values.
9. Provide CREATE OR REPLACE VIEW for every business table returning
   only active rows (WHERE NOT is_deleted): v_cars, v_customers, v_sales,
   v_credit_terms, v_credit_transactions, v_expenses, v_partners, ...
10. Enable Row Level Security on all tables with a read policy for authenticated
    users and a write policy for the service role. Add short COMMENT ON
    statements for every table and column.
11. Include a trigger set_updated_at() BEFORE UPDATE on every table that sets
    updated_at = now() when it was not explicitly provided.
```

```text
Create exactly these TABLES with these columns (add audit columns to each):

1) cars — cars
   sira_nomresi int, marka text, qeydiyyat_nisani text UNIQUE, vin text,
   il int, yurus int, yanacaq text, alis_tarixi timestamptz,
   alis_usulu text, alis_qiymeti numeric(14,2), xercler_cemi numeric(14,2),
   maya_deyeri numeric(14,2), satis_qiymeti numeric(14,2),
   status text CHECK (status IN ('Stokda','Satışda','Kreditdə','Satıldı',
   'Barter edildi','Transfer edildi','Təmirdə')),
   kredit_nomresi text, barter_tesviri text, barter_deyeri numeric(14,2),
   barter_car_id uuid REFERENCES cars(id), customer_id uuid REFERENCES customers(id),
   sened_sayi int, is_barter boolean

2) customers — customers
   ad text, soyad text, ata_adi text, telefon text, fin_kod text,
   seriya_nomre text, unvan text, qeyd text, musteri_nomresi text

3) expenses — expenses (per car and office)
   car_id uuid, tarix timestamptz, qrup text, kategoriya text, teyinat text,
   odenis_usulu text, mebleg numeric(14,2), qeyd text,
   teyinat_novu text CHECK (teyinat_novu IN ('Avtomobil','Ofis','Digər'))

4) sales — sales
   muqavile_nomresi text UNIQUE, car_id uuid, customer_id uuid, musteri text,
   satis_tarixi timestamptz, satis_qiymeti numeric(14,2),
   maya_deyeri numeric(14,2), menfeet numeric(14,2),
   odenis_usulu text CHECK (odenis_usulu IN ('Nağd','Kredit','Barter','Transfer')),
   barter_mebleg numeric(14,2), nagd_mebleg numeric(14,2),
   barter_tesviri text, received_car_id uuid REFERENCES cars(id), qeyd text

5) credit_terms — installment contracts
   sale_id uuid, car_id uuid, customer_id uuid,
   muqavile_nomresi text UNIQUE, mebleg numeric(14,2) (contract amount),
   ilkin_odenis numeric(14,2) (down payment), kreditlesdirilen numeric(14,2),
   faiz_derecesi numeric(9,4), kredit_qiymeti numeric(14,2),
   aylig_odenis numeric(14,2), muddet_ay int, baslama_tarixi timestamptz,
   odenilmis numeric(14,2), qaliq_borc numeric(14,2),
   status text CHECK (status IN ('Aktiv','Bağlı','Gecikmiş')),
   erken_baglanma boolean, baglanma_tarixi timestamptz, qeyd text

6) credit_transactions — extra credit income/expense ledger
   credit_id uuid, car_id uuid, nov text CHECK (nov IN
   ('Gəlir','Gecikmə','Barter','Transfer','Vaxtından tez bağlama','Transfer olunmaq')),
   mebleg numeric(14,2), tarix timestamptz, installment_no int,
   mohlet_tarixi timestamptz, gecikme_tarixi timestamptz,
   odenilib boolean DEFAULT false, tesvir text,
   transfer_sexs text, gecikme_gun int, qeyd text

7) partners — partners
   ad text UNIQUE, faiz numeric(9,4), qalig_payi boolean,
   aktiv boolean DEFAULT true, sira int, qeyd text

8) partner_shares — profit split rows (polymorphic)
   ref_type text CHECK (ref_type IN ('Sale','Credit','CreditTransaction')),
   ref_id uuid, partner_id uuid REFERENCES partners(id), terefdas text,
   faiz numeric(9,4), qalig_payi boolean, mebleg numeric(14,2),
   baza numeric(14,2), aktiv boolean DEFAULT true, sira int,
   UNIQUE (ref_type, ref_id, partner_id)
```

```text
9) partner_payments — money paid out to partners
   partner_id uuid, mebleg numeric(14,2), tarix timestamptz,
   odenis_usulu text, qeyd text, nov text CHECK (nov IN ('Ödəniş','Avans','Digər'))

10) transfer_recipients — people cars are transferred to
    ad text UNIQUE, telefon text, qeyd text, aktiv boolean DEFAULT true

11) media_attachments — documents & photos (stored on external USB)
    ref_type text CHECK (ref_type IN ('Avtomobil','Kredit','KreditEmeliyyati','Satis')),
    ref_id uuid, file_name text, stored_path text, size_bytes bigint,
    tarix timestamptz, qeyd text, usb_token text

12) users — application users
    username text UNIQUE, full_name text, password_hash text, password_salt text,
    role text CHECK (role IN ('Admin','Asif','Sahil')),
    aktiv boolean DEFAULT true, last_login_at timestamptz

13) usb_devices — registered external DATA USB drives (token based)
    token text UNIQUE, ad text, son_herf text, qeydiyyat_tarixi timestamptz,
    son_gorunme_tarixi timestamptz, aktiv boolean DEFAULT true

14) app_settings — key/value application settings
    key text UNIQUE, value text, qrup text, qeyd text

15) trash_backups — restore snapshots of deleted rows
    ref_type text, ref_id uuid, payload jsonb, silinme_tarixi timestamptz,
    silen text, sebeb text

16) sync_state — per table per peer synchronisation cursor
    table_name text, target_name text CHECK (target_name IN ('Flash','Cloud','Local')),
    last_synced_at timestamptz, last_run_at timestamptz, sent_count int,
    received_count int, conflict_count int, error_count int,
    UNIQUE (table_name, target_name)

17) sync_log — synchronisation audit trail
    run_at timestamptz, peer_name text, phase text, table_name text,
    sent int, received int, conflicts int, success boolean, message text

ALSO GENERATE:
A. View "v_credit_portfolio" — per active credit: contract no, customer, car,
   financed amount, down payment, interest, full payable, paid-to-date,
   remaining debt, delay penalty (paid / unpaid), status.
B. View "v_delay_summary" — total penalty, paid penalty, unpaid penalty,
   delay count (per contract and overall).
C. View "v_monthly_stats" — per month: car count, sold count, sales revenue,
   sales profit, credit payments, credit interest profit, expenses, net profit.
D. Function "fn_partner_totals(from_date timestamptz, to_date timestamptz)"
   returning per partner: total share amount, number of splits, percent.
E. Seed data: 3 users (Seccad = Admin, Asif = Asif, Sahil = Sahil — hashes
   as placeholders); 5 partners (Zaur 6%, Eşqin 5%, Asiman 5%,
   Asif + Musa = residual); default app_settings
   (auto_refresh_seconds = 5, currency = AZN).
F. All statements idempotent (CREATE TABLE IF NOT EXISTS,
   CREATE OR REPLACE VIEW/FUNCTION, DROP TRIGGER IF EXISTS ...).

Output the complete, ordered SQL migration script with English comments,
ready to run on PostgreSQL 15+ / Supabase / Firebase SQL Connect.
```

---

## 🇦🇿 AZƏRBAYCANCA İZAH ✓

| Nə istəyirik | Prompt hansı cədvəli yaradır |
|---|---|
| 🚘 Avtomobil idarəetməsi | `cars` + xərclər `expenses` ✓ |
| 💰 Satış (nağd · kredit · barter · transfer) | `sales` ✓ |
| 💳 Kredit müqavilələri | `credit_terms` — avans · faiz · aylıq ödəniş · **qalıq borc** ✓ |
| 🏷️ Kredit əlavə gəlir/xərc + **gecikmə** | `credit_transactions` ✓ |
| 👥 Tərəfdaş bölgüsü + ödənişlər | `partners` · `partner_shares` · `partner_payments` ✓ |
| 📤 Transfer edilən şəxslər | `transfer_recipients` ✓ |
| 📎 Sənəd & media arxivi (USB) | `media_attachments` ✓ |
| 🔐 İstifadəçi rolları (Seccad · Asif · Sahil) | `users` ✓ |
| 💾 DATA USB qeydiyyatı (token) | `usb_devices` ✓ |
| 🗑️ Zibil / geri qaytarma | `trash_backups` ✓ |
| 🔄 Sinxronizasiya | `sync_state` · `sync_log` ✓ |
| 📊 Maliyyə hesabatları | `v_credit_portfolio` · `v_delay_summary` · `v_monthly_stats` · `fn_partner_totals` ✓ |
| 🛡️ **Zero Data Loss** | HƏR cədvəldə `updated_at` · `updated_by` · `is_deleted` ✓ — **fiziki silinmə YOX** ✗ ✓ |

> ℹ️ Bu sxem artıq hazırladığımız **`Docs/LocalFirst/Schema_PostgreSQL.sql`** ilə
> tam uyğundur ✓ — lokal SQLite (`Schema_SQLite.sql`) · USB baza · bu bulud bazası
> **1:1 sinxronlaşır** ✓✓✓ (LWW + MERGE mühərriki: `LocalFirstSyncService.cs.txt` ✓)
