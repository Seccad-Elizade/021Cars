-- ============================================================================
--  ☁️  LOCAL-FIRST AVTOSALON SİSTEMİ — POSTGRESQL SXEMİ (Bulud / Mərkəzi)
-- ----------------------------------------------------------------------------
--  SQLite ilə EYNİ məntiq ✓ → sinxronizasiya 1:1 uyğun ✓
--   • Id            uuid PRIMARY KEY            (SQLite-da TEXT GUID ✓)
--   • UpdatedAt     timestamptz                 (SQLite-da ISO-8601 mətn ✓)
--   • IsDeleted     boolean                     (SQLite-da 0/1 ✓)
--   • Pul sahələri  numeric(14,2)               (SQLite-da REAL ✓)
--   • Sync sayğacı: sync_state · sync_log
-- ============================================================================

CREATE EXTENSION IF NOT EXISTS "pgcrypto";     -- gen_random_uuid() ✓
CREATE EXTENSION IF NOT EXISTS "citext";       -- registr-duyarsız mətn (telefon/FIN) ✓

-- ---------------------------------------------------------------------------
--  👤 customers — Müştərilər
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS customers (
    id           uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    ad           text        NOT NULL DEFAULT '',
    soyad        text        NOT NULL DEFAULT '',
    ata_adi      text        NOT NULL DEFAULT '',
    telefon      text        NOT NULL DEFAULT '',
    fin_kod      text        NOT NULL DEFAULT '',
    seriya_nomre text        NOT NULL DEFAULT '',
    unvan        text        NOT NULL DEFAULT '',
    qeyd         text        NOT NULL DEFAULT '',
    updated_at   timestamptz NOT NULL DEFAULT now(),   -- ✓ LWW açarı
    updated_by   text        NOT NULL DEFAULT '',       -- ✓ DeviceId
    is_deleted   boolean     NOT NULL DEFAULT false,    -- 🗑️ soft delete
    deleted_at   timestamptz NULL
);

CREATE INDEX IF NOT EXISTS ix_customers_sync    ON customers (is_deleted, updated_at);
CREATE INDEX IF NOT EXISTS ix_customers_telefon ON customers (telefon);

-- ---------------------------------------------------------------------------
--  🚘 cars — Avtomobillər
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS cars (
    id               uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    sira_nomresi     integer      NOT NULL DEFAULT 0,
    marka            text         NOT NULL DEFAULT '',
    qeydiyyat_nisani text         NOT NULL DEFAULT '',
    vin              text         NOT NULL DEFAULT '',
    il               integer      NOT NULL DEFAULT 0,
    yurus            integer      NOT NULL DEFAULT 0,
    yanacaq          text         NOT NULL DEFAULT '',
    alis_tarixi      timestamptz  NULL,
    alis_usulu       text         NOT NULL DEFAULT 'Nağd',
    alis_qiymeti     numeric(14,2) NOT NULL DEFAULT 0,   -- 💵 alış qiyməti
    xercler_cemi     numeric(14,2) NOT NULL DEFAULT 0,   -- 🧾 xərclər cəmi
    maya_deyeri      numeric(14,2) NOT NULL DEFAULT 0,   -- 💎 maya dəyəri
    satis_qiymeti    numeric(14,2) NOT NULL DEFAULT 0,   -- 💰 satış qiyməti
    status           text         NOT NULL DEFAULT 'Stokda',
    kredit_nomresi   text         NOT NULL DEFAULT '',
    barter_tesviri   text         NOT NULL DEFAULT '',
    barter_deyeri    numeric(14,2) NOT NULL DEFAULT 0,
    customer_id      uuid         NULL REFERENCES customers(id) ON DELETE SET NULL,
    updated_at       timestamptz  NOT NULL DEFAULT now(),
    updated_by       text         NOT NULL DEFAULT '',
    is_deleted       boolean      NOT NULL DEFAULT false,
    deleted_at       timestamptz  NULL
);

CREATE INDEX IF NOT EXISTS ix_cars_sync      ON cars (is_deleted, updated_at);
CREATE INDEX IF NOT EXISTS ix_cars_status    ON cars (status);
CREATE INDEX IF NOT EXISTS ix_cars_qeydiyyat ON cars (qeydiyyat_nisani);

-- ---------------------------------------------------------------------------
--  🧾 expenses — Xərclər
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS expenses (
    id           uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    car_id       uuid          NULL REFERENCES cars(id) ON DELETE SET NULL,
    tarix        timestamptz   NOT NULL DEFAULT now(),
    qrup         text          NOT NULL DEFAULT '',
    kategoriya   text          NOT NULL DEFAULT '',
    teyinat      text          NOT NULL DEFAULT '',
    odenis_usulu text          NOT NULL DEFAULT '',
    mebleg       numeric(14,2) NOT NULL DEFAULT 0,
    qeyd         text          NOT NULL DEFAULT '',
    updated_at   timestamptz   NOT NULL DEFAULT now(),
    updated_by   text          NOT NULL DEFAULT '',
    is_deleted   boolean       NOT NULL DEFAULT false,
    deleted_at   timestamptz   NULL
);

CREATE INDEX IF NOT EXISTS ix_expenses_sync  ON expenses (is_deleted, updated_at);
CREATE INDEX IF NOT EXISTS ix_expenses_car   ON expenses (car_id);
CREATE INDEX IF NOT EXISTS ix_expenses_tarix ON expenses (tarix);

-- ---------------------------------------------------------------------------
--  💰 sales — Satışlar
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sales (
    id               uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    muqavile_nomresi text          NOT NULL DEFAULT '',
    car_id           uuid          NULL REFERENCES cars(id) ON DELETE SET NULL,
    customer_id      uuid          NULL REFERENCES customers(id) ON DELETE SET NULL,
    musteri          text          NOT NULL DEFAULT '',
    satis_tarixi     timestamptz   NOT NULL DEFAULT now(),
    satis_qiymeti    numeric(14,2) NOT NULL DEFAULT 0,   -- 💰 necəyə satılıb
    maya_deyeri      numeric(14,2) NOT NULL DEFAULT 0,
    menfeet          numeric(14,2) NOT NULL DEFAULT 0,
    odenis_usulu     text          NOT NULL DEFAULT '',
    barter_mebleg    numeric(14,2) NOT NULL DEFAULT 0,
    nagd_mebleg      numeric(14,2) NOT NULL DEFAULT 0,
    qeyd             text          NOT NULL DEFAULT '',
    updated_at       timestamptz   NOT NULL DEFAULT now(),
    updated_by       text          NOT NULL DEFAULT '',
    is_deleted       boolean       NOT NULL DEFAULT false,
    deleted_at       timestamptz   NULL
);

CREATE INDEX IF NOT EXISTS ix_sales_sync  ON sales (is_deleted, updated_at);
CREATE INDEX IF NOT EXISTS ix_sales_car   ON sales (car_id);
CREATE INDEX IF NOT EXISTS ix_sales_tarix ON sales (satis_tarixi);

-- ---------------------------------------------------------------------------
--  💳 credit_terms — Kredit şərtləri · aylıq ödənişlər · qalıq borc
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS credit_terms (
    id               uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    sale_id          uuid          NULL REFERENCES sales(id) ON DELETE SET NULL,
    car_id           uuid          NULL REFERENCES cars(id) ON DELETE SET NULL,
    customer_id      uuid          NULL REFERENCES customers(id) ON DELETE SET NULL,
    muqavile_nomresi text          NOT NULL DEFAULT '',
    mebleg           numeric(14,2) NOT NULL DEFAULT 0,
    ilkin_odenis     numeric(14,2) NOT NULL DEFAULT 0,   -- 💰 avans
    kreditlesdirilen numeric(14,2) NOT NULL DEFAULT 0,
    faiz_derecesi    numeric(9,4)  NOT NULL DEFAULT 0,
    kredit_qiymeti   numeric(14,2) NOT NULL DEFAULT 0,
    aylig_odenis     numeric(14,2) NOT NULL DEFAULT 0,   -- aylıq ödəniş
    muddet_ay        integer       NOT NULL DEFAULT 0,
    baslama_tarixi   timestamptz   NULL,
    odenilmis        numeric(14,2) NOT NULL DEFAULT 0,   -- ✅ ödənilmiş
    qaliq_borc       numeric(14,2) NOT NULL DEFAULT 0,   -- 🔻 qalıq borc
    status           text          NOT NULL DEFAULT 'Aktiv',
    updated_at       timestamptz   NOT NULL DEFAULT now(),
    updated_by       text          NOT NULL DEFAULT '',
    is_deleted       boolean       NOT NULL DEFAULT false,
    deleted_at       timestamptz   NULL
);

CREATE INDEX IF NOT EXISTS ix_credit_terms_sync ON credit_terms (is_deleted, updated_at);
CREATE INDEX IF NOT EXISTS ix_credit_terms_car  ON credit_terms (car_id);
CREATE INDEX IF NOT EXISTS ix_credit_terms_stat ON credit_terms (status);

-- ---------------------------------------------------------------------------
--  🔄 sync_state — hər cədvəl × hər hədəf üçün son nişan
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sync_state (
    id             uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    table_name     text        NOT NULL,
    target_name    text        NOT NULL,
    last_synced_at timestamptz NOT NULL DEFAULT now(),
    last_run_at    timestamptz NOT NULL DEFAULT now(),
    sent_count     integer     NOT NULL DEFAULT 0,
    received_count integer     NOT NULL DEFAULT 0,
    conflict_count integer     NOT NULL DEFAULT 0,
    error_count    integer     NOT NULL DEFAULT 0,
    CONSTRAINT uq_sync_state UNIQUE (table_name, target_name)
);

-- ---------------------------------------------------------------------------
--  📜 sync_log — sinxronizasiya auditi
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS sync_log (
    id         uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    run_at     timestamptz NOT NULL DEFAULT now(),
    peer_name  text        NOT NULL,
    phase      text        NOT NULL,
    table_name text        NOT NULL DEFAULT '',
    sent       integer     NOT NULL DEFAULT 0,
    received   integer     NOT NULL DEFAULT 0,
    conflicts  integer     NOT NULL DEFAULT 0,
    success    boolean     NOT NULL DEFAULT true,
    message    text        NOT NULL DEFAULT ''
);

CREATE INDEX IF NOT EXISTS ix_sync_log_run_at ON sync_log (run_at DESC);

-- ---------------------------------------------------------------------------
--  👁️ GÖRÜNÜŞLƏR — yalnız aktiv sətirlər ✓ (bütün oxumalar bunlarla ✓)
-- ---------------------------------------------------------------------------
CREATE OR REPLACE VIEW v_cars         AS SELECT * FROM cars         WHERE NOT is_deleted;
CREATE OR REPLACE VIEW v_customers    AS SELECT * FROM customers    WHERE NOT is_deleted;
CREATE OR REPLACE VIEW v_expenses     AS SELECT * FROM expenses     WHERE NOT is_deleted;
CREATE OR REPLACE VIEW v_sales        AS SELECT * FROM sales        WHERE NOT is_deleted;
CREATE OR REPLACE VIEW v_credit_terms AS SELECT * FROM credit_terms WHERE NOT is_deleted;

-- ============================================================================
--  🛡️ TƏHLÜKƏSİZLİK — Row Level Security (istəyə bağlı ✓)
-- ----------------------------------------------------------------------------
--  ⚠ Sinxronizasiya xidməti `service_role` / ayrıca istifadəçi ilə işləyir ✓
--    Anon açar YALNIZ oxuya bilər ✓ (yazma üçün JWT tələb olunur ✓)
-- ============================================================================
-- ALTER TABLE cars ENABLE ROW LEVEL SECURITY;
-- CREATE POLICY cars_read  ON cars FOR SELECT USING (true);
-- CREATE POLICY cars_write ON cars FOR ALL    USING (current_setting('request.jwt.claim.role', true) = 'service_role');
