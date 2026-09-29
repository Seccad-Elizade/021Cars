# 🏛️ Local-First Avtosalon İdarəetmə Sistemi — Arxitektura + Sinxronizasiya

> **Üçlü arxitektura:** 🖥️ Yerli SQLite (sürət ✓) ⇄ 💾 Fləşkart (portativ ehtiyat ✓) ⇄ ☁️ PostgreSQL (mərkəzi bulud ✓)
> **Prinsip:** *Zero Data Loss* — heç bir sətir İTMİR ✗, heç bir halda proqram ÇÖKMÜR ✗.

```
┌──────────────────────────┐     🔄 LWW + MERGE      ┌──────────────────────────┐
│ 🖥️ KOMPÜTER (SQLite)     │ ◄─────────────────────► │ 💾 FLƏŞKART (SQLite)     │
│ data/local.db            │   „Yerli« mərkəz rolunda │ 021cars_drive.lock ✓     │
│ — sürətli əməliyyatlar — │                         │ data/backup.db           │
└───────────┬──────────────┘                         └──────────────────────────┘
            │                                                   ▲
            │        🔄 LWW + MERGE (Task.Run · arxa fon ✓)     │
            ▼                                                   │
┌──────────────────────────────────────────────────┐            │
│ ☁️ BULUD — PostgreSQL (Firebase/Supabase/öz)      │  ⟵ dolayı yolla uzlaşır ✓
│ cars · customers · expenses · sales · credit_terms│            │
└──────────────────────────────────────────────────┘            └─── iki faza kifayət edir ✓
```

---

## 📦 1. Quraşdırma

```powershell
# layihə kökündə (WPF layihəsi ✓)
dotnet add package Microsoft.Data.Sqlite     # ✓ SQLite (yerli + fləşkart)
dotnet add package Npgsql                    # ✓ PostgreSQL (bulud)
```

| Fayl | Yeri | Nə |
|---|---|---|
| `Schema_SQLite.sql` | `Docs/LocalFirst/` | SQLite sxemi (yerli + fləşkart ✓) |
| `Schema_PostgreSQL.sql` | `Docs/LocalFirst/` | PostgreSQL sxemi (bulud ✓) |
| `LocalFirstSyncService.cs.txt` | `Docs/LocalFirst/` | 🔄 Sinxronizasiya mühərriki → **`.cs` kimi layihəyə köçürün** ✓ |

> ⚠ Fayl `*.cs.txt` saxlanılıb ki, repo-nun hazırkı build-i pozulmasın ✗ →
> `LocalFirstSyncService.cs` adı ilə layihəyə əlavə edin ✓ və 2 NuGet paketini qoşun ✓.

---

## 🗄️ 2. Sxemin qurulması

**① SQLite (yerli + fləşkart)** — ✅ **AVTOMATİK** ✓
`SyncAllAsync()` işə düşəndə `EnsureSchemaAsync(...)` hər iki SQLite bazasında
cədvəlləri, indeksləri və `SyncState`/`SyncLog` cədvəllərini **özü yaradır** ✓
(`Schema_SQLite.sql` yalnız əl ilə qurmaq/audit etmək istəsəniz ✓).

**② PostgreSQL (bulud)** — ⚠️ **BİR DƏFƏ** əl ilə ✓
```bash
psql "Host=…;Port=5432;Database=autosalon;Username=…;Password=…" \
     -f Docs/LocalFirst/Schema_PostgreSQL.sql
```

**③ Fləşkart hazırlığı** ✓
```
F:\                              ← fləşkart kökü (hərf dəyişə bilər ✗ ✓)
├── 021cars_drive.lock           ← 🔑 marker fayl (BOŞ fayl ✓)
└── data\
    └── backup.db                ← SQLite bazası (ilk dəfə avtomatik yaradılır ✓)
```

---

## 🚀 3. İnteqrasiya (proqram açılışında)

```csharp
// App.xaml.cs → OnStartup  (MainWindow.Show()-dan SONRA ✓ — UI bloklanmır ✗)
var sync = new LocalFirstSyncService(new SyncOptions
{
    LocalDbPath           = Path.Combine(AppContext.BaseDirectory, "data", "local.db"),
    FlashDbRelativePath   = @"data\backup.db",
    FlashLockFile         = "021cars_drive.lock",
    CloudConnectionString = Environment.GetEnvironmentVariable("AUTOSALON_PG"),
    BatchSize             = 500,
    MaxRetries            = 3
}, log: m => Serilog.Log.Information("SYNC | {Mesaj}", m));

// 📢 UI-yə YALNIZ Dispatcher vasitəsilə ✓ (thread təhlükəsizliyi ✓)
sync.StatusChanged += m => Application.Current?.Dispatcher.Invoke(
    () => { if (MainWindow?.DataContext is MainViewModel vm) vm.StatusMessage = m; });

sync.ProgressChanged += r => Serilog.Log.Information("SYNC | {Faza}", r.ToString());

// 🚀 ARXA FONDA — proqram açılışı HEÇ VAXT gözləmir ✗✓✓
sync.RunInBackground();

// 🪟 Sənəd (fayl) əməliyyatları üçün fləşkart yoxlanışı ✓ (çökmə YOX ✗):
bool flashVar = sync.Locator.Find() is not null;
if (!flashVar)
{
    vm.StatusMessage = "💾 Fləşkart taxılı deyil ✗ — sənəd arxivi əlçatan deyil, " +
                       "qalan əməliyyatlar NORMAL işləyir ✓";
}
```

### 🔁 Dövri sinxronizasiya (istəyə bağlı ✓)
```csharp
var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));   // hər 5 dəqiqə ✓
_ = Task.Run(async () =>
{
    while (await timer.WaitForNextTickAsync())
    {
        try { await sync.SyncAllAsync(); } catch { /* ✓ udulur, log yazılır ✓ */ }
    }
});
```

---

## ⚖️ 4. Konfliktin həlli (Conflict Resolution) — LWW + MERGE

```csharp
private SyncRow? UstunTut(SyncRow? a, SyncRow? b)
{
    if (a is null) return b;                 // ✓ yalnız bir tərəfdə var → MERGE ✓
    if (b is null) return a;                 // ✓ yeni qeyd İTMİR ✗

    var ta = Sinirla(a.UpdatedAtUtc);         // ⏲️ gələcək tarixlər sıxılır ✓
    var tb = Sinirla(b.UpdatedAtUtc);

    if (ta > tb) return a;                    // ① ən TƏZƏ dəyişiklik üstün ✓
    if (tb > ta) return b;

    // ② bərabər → DeviceId ordinal (HƏR İKİ tərəfdə EYNİ nəticə ✓)
    return string.CompareOrdinal(a.DeviceId, b.DeviceId) >= 0 ? a : b;
}
```

| Qayda | Nəticə |
|---|---|
| ① `UpdatedAt` böyükdür | ✅ həmin tərəf üstün ✓, digəri yenilənir ✓ |
| ② `UpdatedAt` bərabərdir | ✅ `UpdatedBy` (DeviceId) ordinal → **deterministik** ✓ (sonsuz dövr yox ✗) |
| ③ qeyd yalnız bir tərəfdə | ✅ **MERGE** — digər tərəfə köçürülür ✓ (itmir ✗) |
| ④ 🗑️ `IsDeleted = 1` | ✅ adi sahə kimi sinxronlaşır ✓ → **hər üç mənbədə silinir** ✓ (fiziki silinmə yox ✗) |
| ⑤ sinxronizasiya yarıda kəsildi | ✅ cursor yalnız irəli gedir ✗ + **2 dəqiqə geriyə baxış** ✓ + UPSERT idempotent ✓ → sətir İTMİR ✗✓✓ |
| ⑥ saat fərqi (clock skew) | ✅ 300 saniyə dözüm ✓ — gələcək tarixlər indiyə sıxılır ✓ |

---

## 🛡️ 5. Dayanıqlıq matrisi (nə olursa nə edir?)

| Hadisə | Sistem davranışı | İstifadəçi təsiri |
|---|---|---|
| 🌐 İnternet qopdu | `IsAvailableAsync()` → false ✓ → bulud fazası **ATLANIR** ✗ | ✅ proqram normal işləyir ✓, log yazılır ✓ |
| 💾 Fləşkart çıxarıldı | `FindDbPath()` → null ✗ / `File.Move` xətası **udulur** ✓ | ✅ sənəd əməliyyatı xəbərdarlıq verir ✓, baza işləyir ✓ |
| 🔤 Fləşkartın hərfi dəyişdi (E:→F:) | `FlashDriveLocator` **hər dəfə yenidən skan edir** ✓ (marker fayl ✓) | ✅ avtomatik tapılır ✓ |
| 🗄️ Baza fayl kilidli/oxunmur | `catch` → xəta loglanır ✓, faz atlanır ✗ | ✅ UI donmur ✗ |
| ✏️ Kəsilmiş GPS/şəbəkə zamanı yazma | **Tranzaksiya** → ya hamısı, ya heç biri ✓ | ✅ yarımçıq sətir olmur ✗ |
| ⚠️ Bulud skripti işə salınmayıb | `EnsureSchemaAsync` xəbərdarlıq yazır ✓ | ✅ yerli + fləş işləyir ✓ |
| ❌ Gözlənilməz xəta | `RunInBackground` ən üst səviyyədə tutur ✓ | ✅ proqram çökmür ✗, yalnız log ✓ |
| 🔁 Eyni sətir təkrar gəldi | UPSERT (`ON CONFLICT … DO UPDATE`) ✓ | ✅ dublikat yaranmır ✗ |

---

## 🧪 6. Test ssenariləri (qəbul meyarı)

| # | Ssenari | Gözlənilən nəticə |
|---|---|---|
| 1 | Fləşkart və internet YOXDUR ✗ → proqram açılır ✓ | ✅ normal işləyir, statusda xəbərdarlıq ✓ |
| 2 | Yerli bazada maşın yaradılır → fləşkart taxılır → sinxron ✓ | ✅ maşın fləşkartda görünür ✓ |
| 3 | Fləşkartda maşın dəyişilir (`UpdatedAt` təzə ✓) → sinxron ✓ | ✅ yerli bazada HƏMİN dəyişiklik üstün tutulur ✓ |
| 4 | Hər iki tərəfdə EYNİ maşın FƏRQLİ dəyişdirilir ✓ | ✅ `UpdatedAt` təzə olan qalib ✓, digəri yenilənir ✓ |
| 5 | Hər iki tərəfdə FƏRQLİ yeni maşınlar ✓ | ✅ **hər ikisi hər iki tərəfdə** ✓ (MERGE ✓) |
| 6 | Yerli bazada maşın silinir (soft ✓) → sinxron ✓ | ✅ fləş + buludda da `IsDeleted=1` ✓ (sətir fiziki qalır ✓) |
| 7 | Sinxron yarıda kəsilir (şəbəkə ✗) → yenidən cəhd ✓ | ✅ heç bir sətir itmir ✓ (cursor overlap ✓) |
| 8 | 3 gün internet olmadı → sonra bərpa ✓ | ✅ bütün dəyişikliklər buluda çıxır ✓, konfliktlər LWW ilə ✓ |

**Yoxlama SQL-i (yerli baza):**
```sql
-- Son sinxronizasiya nişanları
SELECT TableName, TargetName, LastSyncedAt, LastRunAt FROM SyncState;

-- Sinxronizasiya jurnalı (son 20)
SELECT RunAt, PeerName, Sent, Received, Conflicts, Success, Message
FROM SyncLog ORDER BY RunAt DESC LIMIT 20;

-- Aktiv (silinməmiş) maşınlar — HƏMİŞƏ `vCars` görünüşü ilə ✓
SELECT COUNT(*) FROM vCars;
```

---

## ⚠️ 7. Vacib qaydalar (komanda üçün)

1. **Hər yazma əməliyyatında** `UpdatedAt = DateTime.UtcNow` ✓ və `UpdatedBy = DeviceId` ✓ mütləq yazılır ✗ başqa cür sinxronizasiya düzgün işləmir.
2. **`Id` HƏMİŞƏ GUID** olmalıdır ✓ (INT autoincrement ✗ — iki cihazda toqquşur ✗).
3. **Fiziki `DELETE` QADAĞANDIR** ✗ → `IsDeleted = 1, DeletedAt = UtcNow` ✓.
4. **Bütün oxumalar** `vCars` / `vSales` … görünüşləri və ya `IsDeleted = 0` filtri ilə ✓.
5. Sinxronizasiya **heç vaxt** UI thread-də çağırılmır ✗ → `RunInBackground()` / `Task.Run` ✓.
6. `LocalFirstSyncService` **singleton** olmalıdır ✓ (iki eyni vaxtda sinxron → `SemaphoreSlim` qoruyur ✓).
7. Baza bağlantı sətri **koda yazılmır** ✗ → mühit dəyişəni / istifadəçi sirri ✓.
