-- ============================================================================
--  🗄️  LOCAL-FIRST AVTOSALON SİSTEMİ — SQLite SXEMİ (Kompüter + Fləşkart)
-- ----------------------------------------------------------------------------
--  Prinsiplər (Zero Data Loss):
--   • HƏR cədvəldə: Id (GUID TEXT) · UpdatedAt (ISO-8601 UTC) · IsDeleted
--   • FİZİKİ SİLİNMƏ YOXDUR ✗ → yalnız IsDeleted = 1 (soft delete) ✓
--   • UpdatedAt həmişə UTC və «yyyy-MM-dd HH:mm:ss.fffZ» formatında ✓
--     (mətn kimi də sıralanır ✓ → LWW müqayisəsi dəqiq işləyir ✓)
--   • DeviceId — konfliktlərdə deterministik tiebreaker ✓
-- ============================================================================

PRAGMA journal_mode = WAL;      -- ✓ paralel oxu/yazı, çökməyə davamlı
PRAGMA synchronous = FULL;      -- ✓ məlumat itkisinin qarşısı
PRAGMA foreign_keys = ON;
PRAGMA busy_timeout = 5000;

-- ---------------------------------------------------------------------------
--  👤 CUSTOMERS — Müştərilər  (Cars-dan ƏVVƏL yaradılır — FK üçün ✓)
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS Customers (
    Id           TEXT NOT NULL PRIMARY KEY,                 -- GUID ✓
    Ad           TEXT NOT NULL DEFAULT '',
    Soyad        TEXT NOT NULL DEFAULT '',
    AtaAdi       TEXT NOT NULL DEFAULT '',
    Telefon      TEXT NOT NULL DEFAULT '',
    FinKod       TEXT NOT NULL DEFAULT '',
    SeriyaNomre  TEXT NOT NULL DEFAULT '',
    Unvan        TEXT NOT NULL DEFAULT '',
    Qeyd         TEXT NOT NULL DEFAULT '',
    UpdatedAt    TEXT NOT NULL,                             -- ✓ son dəyişiklik (UTC)
    UpdatedBy    TEXT NOT NULL DEFAULT '',                  -- ✓ DeviceId
    IsDeleted    INTEGER NOT NULL DEFAULT 0,                -- 🗑️ soft delete
    DeletedAt    TEXT NULL
);

CREATE INDEX IF NOT EXISTS IX_Customers_Sync    ON Customers (IsDeleted, UpdatedAt);
CREATE INDEX IF NOT EXISTS IX_Customers_Telefon ON Customers (Telefon);

-- ---------------------------------------------------------------------------
--  🚘 CARS — Avtomobillər
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS Cars (
    Id              TEXT    NOT NULL PRIMARY KEY,
    SiraNomresi     INTEGER NOT NULL DEFAULT 0,
    Marka           TEXT    NOT NULL DEFAULT '',
    QeydiyyatNisani TEXT    NOT NULL DEFAULT '',
    Vin             TEXT    NOT NULL DEFAULT '',
    Il              INTEGER NOT NULL DEFAULT 0,
    Yurus           INTEGER NOT NULL DEFAULT 0,
    Yanacaq         TEXT    NOT NULL DEFAULT '',
    AlisTarixi      TEXT    NULL,                           -- ISO-8601 UTC
    AlisUsulu       TEXT    NOT NULL DEFAULT 'Nağd',
    AlisQiymeti     REAL    NOT NULL DEFAULT 0,             -- 💵 alış qiyməti
    XerclerCemi     REAL    NOT NULL DEFAULT 0,             -- 🧾 xərclər cəmi
    MayaDeyeri      REAL    NOT NULL DEFAULT 0,             -- 💎 alış + xərclər
    SatisQiymeti    REAL    NOT NULL DEFAULT 0,             -- 💰 satış qiyməti
    Status          TEXT    NOT NULL DEFAULT 'Stokda',      -- Stokda/Satışda/Kreditdə/Satıldı
    KreditNomresi   TEXT    NOT NULL DEFAULT '',
    BarterTesviri   TEXT    NOT NULL DEFAULT '',
    BarterDeyeri    REAL    NOT NULL DEFAULT 0,
    CustomerId      TEXT    NULL REFERENCES Customers(Id),
    UpdatedAt       TEXT    NOT NULL,
    UpdatedBy       TEXT    NOT NULL DEFAULT '',
    IsDeleted       INTEGER NOT NULL DEFAULT 0,
    DeletedAt       TEXT    NULL
);

CREATE INDEX IF NOT EXISTS IX_Cars_Sync      ON Cars (IsDeleted, UpdatedAt);
CREATE INDEX IF NOT EXISTS IX_Cars_Status    ON Cars (Status);
CREATE INDEX IF NOT EXISTS IX_Cars_Qeydiyyat ON Cars (QeydiyyatNisani);

-- ---------------------------------------------------------------------------
--  🧾 EXPENSES — Xərclər
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS Expenses (
    Id          TEXT NOT NULL PRIMARY KEY,
    CarId       TEXT NULL REFERENCES Cars(Id),
    Tarix       TEXT NOT NULL,
    Qrup        TEXT NOT NULL DEFAULT '',      -- Avtomobil / Ofis …
    Kategoriya  TEXT NOT NULL DEFAULT '',      -- Təmir / Yuyulma / İnzibati …
    Teyinat     TEXT NOT NULL DEFAULT '',
    OdenisUsulu TEXT NOT NULL DEFAULT '',
    Mebleg      REAL NOT NULL DEFAULT 0,
    Qeyd        TEXT NOT NULL DEFAULT '',
    UpdatedAt   TEXT NOT NULL,
    UpdatedBy   TEXT NOT NULL DEFAULT '',
    IsDeleted   INTEGER NOT NULL DEFAULT 0,
    DeletedAt   TEXT NULL
);

CREATE INDEX IF NOT EXISTS IX_Expenses_Sync  ON Expenses (IsDeleted, UpdatedAt);
CREATE INDEX IF NOT EXISTS IX_Expenses_Car   ON Expenses (CarId);
CREATE INDEX IF NOT EXISTS IX_Expenses_Tarix ON Expenses (Tarix);

-- ---------------------------------------------------------------------------
--  💰 SALES — Satışlar
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS Sales (
    Id              TEXT NOT NULL PRIMARY KEY,
    MuqavileNomresi TEXT NOT NULL DEFAULT '',
    CarId           TEXT NULL REFERENCES Cars(Id),
    CustomerId      TEXT NULL REFERENCES Customers(Id),
    Musteri         TEXT NOT NULL DEFAULT '',
    SatisTarixi     TEXT NOT NULL,
    SatisQiymeti    REAL NOT NULL DEFAULT 0,    -- 💰 necəyə satılıb
    MayaDeyeri      REAL NOT NULL DEFAULT 0,
    Menfeet         REAL NOT NULL DEFAULT 0,
    OdenisUsulu     TEXT NOT NULL DEFAULT '',   -- Nağd / Kredit / Barter
    BarterMebleg    REAL NOT NULL DEFAULT 0,
    NagdMebleg      REAL NOT NULL DEFAULT 0,
    Qeyd            TEXT NOT NULL DEFAULT '',
    UpdatedAt       TEXT NOT NULL,
    UpdatedBy       TEXT NOT NULL DEFAULT '',
    IsDeleted       INTEGER NOT NULL DEFAULT 0,
    DeletedAt       TEXT NULL
);

CREATE INDEX IF NOT EXISTS IX_Sales_Sync  ON Sales (IsDeleted, UpdatedAt);
CREATE INDEX IF NOT EXISTS IX_Sales_Car   ON Sales (CarId);
CREATE INDEX IF NOT EXISTS IX_Sales_Tarix ON Sales (SatisTarixi);

-- ---------------------------------------------------------------------------
--  💳 CREDITTERMS — Kredit şərtləri · aylıq ödənişlər · qalıq borc
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS CreditTerms (
    Id               TEXT NOT NULL PRIMARY KEY,
    SaleId           TEXT NULL REFERENCES Sales(Id),
    CarId            TEXT NULL REFERENCES Cars(Id),
    CustomerId       TEXT NULL REFERENCES Customers(Id),
    MuqavileNomresi  TEXT NOT NULL DEFAULT '',
    Mebleg           REAL NOT NULL DEFAULT 0,   -- müqavilə məbləği
    IlkinOdenis      REAL NOT NULL DEFAULT 0,   -- 💰 avans
    Kreditlesdirilen REAL NOT NULL DEFAULT 0,   -- əsas borc
    FaizDerecesi     REAL NOT NULL DEFAULT 0,
    KreditQiymeti    REAL NOT NULL DEFAULT 0,
    AylıqOdenis      REAL NOT NULL DEFAULT 0,   -- aylıq ödəniş
    MuddetAy         INTEGER NOT NULL DEFAULT 0,
    BaslamaTarixi    TEXT NULL,
    Odenilmis        REAL NOT NULL DEFAULT 0,   -- ✅ ödənilmiş
    QaliqBorc        REAL NOT NULL DEFAULT 0,   -- 🔻 qalıq borc
    Status           TEXT NOT NULL DEFAULT 'Aktiv',
    UpdatedAt        TEXT NOT NULL,
    UpdatedBy        TEXT NOT NULL DEFAULT '',
    IsDeleted        INTEGER NOT NULL DEFAULT 0,
    DeletedAt        TEXT NULL
);

CREATE INDEX IF NOT EXISTS IX_CreditTerms_Sync ON CreditTerms (IsDeleted, UpdatedAt);
CREATE INDEX IF NOT EXISTS IX_CreditTerms_Car  ON CreditTerms (CarId);
CREATE INDEX IF NOT EXISTS IX_CreditTerms_Stat ON CreditTerms (Status);

-- ---------------------------------------------------------------------------
--  🔄 SYNCSTATE — hər cədvəl × hər hədəf üçün son sinxronizasiya nişanı
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS SyncState (
    Id            TEXT NOT NULL PRIMARY KEY,
    TableName     TEXT NOT NULL,
    TargetName    TEXT NOT NULL,          -- «Flash» · «Cloud»
    LastSyncedAt  TEXT NOT NULL,          -- bu tarixdən sonrakı dəyişikliklər göndərilir
    LastRunAt     TEXT NOT NULL,
    SentCount     INTEGER NOT NULL DEFAULT 0,
    ReceivedCount INTEGER NOT NULL DEFAULT 0,
    ConflictCount INTEGER NOT NULL DEFAULT 0,
    ErrorCount    INTEGER NOT NULL DEFAULT 0,
    UNIQUE (TableName, TargetName)
);

-- ---------------------------------------------------------------------------
--  📜 SYNCLOG — hər sinxronizasiya cəhdinin auditi
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS SyncLog (
    Id        TEXT NOT NULL PRIMARY KEY,
    RunAt     TEXT NOT NULL,
    PeerName  TEXT NOT NULL,
    Phase     TEXT NOT NULL,              -- LocalFlash / LocalCloud / FlashCloud
    TableName TEXT NOT NULL DEFAULT '',
    Sent      INTEGER NOT NULL DEFAULT 0,
    Received  INTEGER NOT NULL DEFAULT 0,
    Conflicts INTEGER NOT NULL DEFAULT 0,
    Success   INTEGER NOT NULL DEFAULT 1,
    Message   TEXT NOT NULL DEFAULT ''
);

CREATE INDEX IF NOT EXISTS IX_SyncLog_RunAt ON SyncLog (RunAt);

-- ---------------------------------------------------------------------------
--  👁️ GÖRÜNÜŞLƏR — bütün oxumalar `IsDeleted = 0` filtri ilə ✓
-- ---------------------------------------------------------------------------
DROP VIEW IF EXISTS vCars;
CREATE VIEW vCars AS SELECT * FROM Cars WHERE IsDeleted = 0;

DROP VIEW IF EXISTS vCustomers;
CREATE VIEW vCustomers AS SELECT * FROM Customers WHERE IsDeleted = 0;

DROP VIEW IF EXISTS vExpenses;
CREATE VIEW vExpenses AS SELECT * FROM Expenses WHERE IsDeleted = 0;

DROP VIEW IF EXISTS vSales;
CREATE VIEW vSales AS SELECT * FROM Sales WHERE IsDeleted = 0;

DROP VIEW IF EXISTS vCreditTerms;
CREATE VIEW vCreditTerms AS SELECT * FROM CreditTerms WHERE IsDeleted = 0;
