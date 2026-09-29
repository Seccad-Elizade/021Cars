# 🔥 FIREBASE SQL CONNECT — **DÜZƏLİŞ PROMPTU 2** (nəticə yoxlamasından sonra ✓)

> **Nəticə:** ✅ 14 cədvəl düzgün yaradıldı ✓ · audit sütunları hər cədvəldə var ✓✓✓
> **Çatışmayan / düzəldilməli:** ❌ **4 qrup** (aşağıda ✓)

---

## ❌ NƏ ÇATIŞMIR — YOXLAMA CƏDVƏLİ ✓

| # | Problem | Nəticə | Həll |
|---|---|---|---|
| 1 | 💰 **`Float!` pul üçün** ✗ | `alisQiymeti` · `satisQiymeti` · `mebleg` … **double precision** olur ✗ → **qəpik itkisi** ✗✓✓ | `Decimal` (numeric(14,2)) ✓ |
| 2 | 🏷️ **`status: String!`** ✗ | yanlış dəyər yazıla bilər ✗ (`"Satildi"` ✗ vs `"Satıldı"` ✓) | **enum** tipləri ✓ |
| 3 | 🔍 **İndekslər / UNIQUE** ✗ | 100 000 sətirdə sinxronizasiya **yavaş** ✗ · dublikat mümkün ✗ | indeks + unikal ✓ |
| 4 | 📊 **3 cədvəl + 3 görünüş + 1 funksiya + seed** ✗ | zibil/bərpa ✗ · sinxronizasiya ✗ · hesabatlar ✗ · istifadəçilər ✗ | əlavə et ✓ |

> ✅ Düzgün olanlar: `id` UUID ✓ · `updatedAt` ✓ · `updatedBy` ✓ · `isDeleted` ✓ · `deletedAt` ✓
> **HƏR CƏDVƏLDƏ** var ✓✓✓ · əlaqələr (FK ✓) `barterCar` · `customer` · `sale` · `credit` · `partner` ✓

---

## 📋 COPY-PASTE — DÜZƏLİŞ PROMPTU (İNGİLİSCƏ) ✓

```text
The schema looks great. Now apply these FIXES and ADDITIONS to it.

FIX 1 — MONEY MUST NOT BE FLOAT (critical):
Replace every Float money and percent field with a proper decimal type
(Decimal → PostgreSQL numeric(14,2) for money, numeric(9,4) for percents).
Fields to change:
- Car: alisQiymeti, xerclerCemi, mayaDeyeri, satisQiymeti, barterDeyeri
- Sale: satisQiymeti, mayaDeyeri, menfeet, barterMebleg, nagdMebleg
- CreditTerm: mebleg, ilkinOdenis, kreditlesdirilen, faizDerecesi,
  kreditQiymeti, ayligOdenis, odenilmis, qaliqBorc
- CreditTransaction: mebleg
- Partner: faiz
- PartnerShare: faiz, mebleg, baza
- PartnerPayment: mebleg
If the DSL has no Decimal type, add a follow-up raw SQL migration that converts
those columns to numeric(14,2) / numeric(9,4) with USING column::numeric.

FIX 2 — REPLACE STATUS STRINGS WITH ENUMS:
create these enums and use them instead of String for the matching fields:
- CarStatus: Stokda, Satisda, Kreditde, Satildi, BarterEdildi, TransferEdildi, Temirde
- SalePayment: Nagd, Kredit, Barter, Transfer
- CreditStatus: Aktiv, Bagli, Gecikmis
- TransactionType: Gelir, Gecikme, Barter, Transfer, VaxtindanTezBaglama, TransferOlunmaq
- PartnerPaymentType: Odenis, Avans, Diger
- UserRole: Admin, Asif, Sahil
- ExpenseDestination: Avtomobil, Ofis, Diger
- MediaRefType: Avtomobil, Kredit, KreditEmeliyyati, Satis

FIX 3 — ADD INDEXES AND UNIQUE CONSTRAINTS:
- every table: index on (isDeleted, updatedAt) — used by the sync engine
- every foreign key column: index (carId, customerId, creditId, saleId,
  partnerId, receivedCarId, barterCarId, refId)
- Car.qeydiyyatNisani UNIQUE, Car.vin index
- Customer.telefon index, Customer.finKod index
- Sale.muqavileNomresi UNIQUE, CreditTerm.muqavileNomresi UNIQUE
- Partner.ad UNIQUE, User.username UNIQUE, UsbDevice.token UNIQUE
  (UsbDevice.token is the security key that identifies the customer's USB)
- AppSetting.key UNIQUE (rename the column to settingKey if "key" is reserved)
- PartnerShare.UNIQUE (refType, refId, partner)
- MediaAttachment: index (refType, refId)

FIX 4 — ADD THE MISSING TABLES:

TrashBackup @table {
  refType: String!
  refId: UUID!
  payload: Json!
  silinmeTarixi: Timestamp!
  silen: String!
  sebeb: String!
  updatedAt: Timestamp!
  updatedBy: String!
  isDeleted: Boolean!
  deletedAt: Timestamp
}

SyncState @table(key: ["tableName", "targetName"]) {
  tableName: String!
  targetName: String!
  lastSyncedAt: Timestamp!
  lastRunAt: Timestamp!
  sentCount: Int!
  receivedCount: Int!
  conflictCount: Int!
  errorCount: Int!
  updatedAt: Timestamp!
  updatedBy: String!
  isDeleted: Boolean!
  deletedAt: Timestamp
}

SyncLog @table {
  runAt: Timestamp!
  peerName: String!
  phase: String!
  tableName: String!
  sent: Int!
  received: Int!
  conflicts: Int!
  success: Boolean!
  message: String!
  updatedAt: Timestamp!
  updatedBy: String!
  isDeleted: Boolean!
  deletedAt: Timestamp
}
```

```text
FIX 5 — ADD THESE VIEWS (the desktop finance dashboard reads them directly):

A. v_credit_portfolio — for every ACTIVE credit (isDeleted = false):
   contract number, customer name, car name + plate, financed amount,
   down payment, interest amount, full payable, paid-to-date,
   remaining debt, paid delay penalty, unpaid delay penalty, status.

B. v_delay_summary — per contract and an overall row:
   delay count, total penalty, paid penalty, unpaid penalty, last delay date.

C. v_monthly_stats — grouped by month of the sale/payment date:
   cars bought, cars sold, sales revenue, sales profit, credit payments,
   credit interest profit, expenses, net profit.

FIX 6 — ADD THIS FUNCTION:
fn_partner_totals(fromDate Timestamp, toDate Timestamp)
returns per partner: partner name, total share amount, split count, percent.

FIX 7 — ADD SEED DATA:
- Users: Seccad (role Admin), Asif (role Asif), Sahil (role Sahil) —
  password hashes as placeholders.
- Partners: Zaur 6%, Esqin 5%, Asiman 5%, Asif residual, Musa residual.
- AppSettings: auto_refresh_seconds = 5, currency = AZN,
  usb_folder = 021Cars, backup_file = 021cars_data.db.

FIX 8 — ADD TRIGGERS:
set_updated_at() BEFORE UPDATE on every table: if updatedAt was not changed
explicitly, set it to now(). Use DROP TRIGGER IF EXISTS then CREATE TRIGGER
so the script stays idempotent.

FIX 9 — ADD ROW LEVEL SECURITY:
enable RLS on every table; policy "read_all" FOR SELECT TO authenticated
USING (true); policy "write_service" FOR ALL TO service_role USING (true).

Finally output the complete, ordered, idempotent SQL migration script again,
including: enums, tables, indexes, unique constraints, views, function,
triggers, RLS and seed data — with English comments.
```

---

## 🇦🇿 NƏTİCƏ — GENERASİYADAN SONRA ✓

| Addım | Nə etməli |
|---|---|
| 1️⃣ | Yuxarıdaki **FIX 1-4** mətnini Firebase chat-ına yapışdırın ✓ → **Generate** ✓ |
| 2️⃣ | **FIX 5-9** mətnini yapışdırın ✓ → **Generate** ✓ |
| 3️⃣ | Çıxan SQL-i `Docs/LocalFirst/Schema_PostgreSQL.sql` ilə **müqayisə edin** ✓ (hazır referans sxemimiz ✓) |
| 4️⃣ | ✅ Nəticədə **17 cədvəl + 3 enum qrupu + 3 görünüş + 1 funksiya + seed** olmalıdır ✓✓✓ |

> ℹ️ **«Key» sözü** ✗ — bəzi SQL bazalarında **ehtiyat sözdür** ✗ → `AppSetting.key` sahəsini
> `settingKey` adlandırın ✓ (yuxarıdaki FIX 3-də göstərilib ✓)

> ⚠️ **`Float` → `Decimal`** ən **vacib** düzəlişdir ✗✓✓ — `0.1 + 0.2 = 0.30000000000000004` ✗
> kimi xətalar pul hesablamalarında **ciddi** problem yaradır ✗ (maliyyə paneli · bölgü ✓)
