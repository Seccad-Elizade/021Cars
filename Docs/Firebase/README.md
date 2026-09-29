# ☁️ 021Cars — **FIREBASE REALTIME DATABASE** ARXİTEKTURASI

> ## ✅ SQL TAMAMİLƏ LƏĞV EDİLDİ ✗ — YALNIZ BULUD ✓
> **Endpoint:** `https://cas-database-96e6e-default-rtdb.firebaseio.com/`
> **SQLite ✗ · SQL faylı ✗ · EF Core ✗ · migration ✗ — HEÇ BİRİ YOXDUR ✓✓✓**

---

## 🏛️ ARXİTEKTURA

```
┌──────────────────────────────────────────────────────────────┐
│  🖥️  WPF + Blazor «021Cars»                                  │
│  App.xaml.cs → BuludKonteksti (08) ← BİR SƏTİRLİK GİRİŞ ✓    │
└──────┬──────────────┬───────────────┬───────────────┬────────┘
       ▼              ▼               ▼               ▼
┌────────────┐ ┌──────────────┐ ┌────────────┐ ┌─────────────┐
│ 🔥 BULUD   │ │ 💾 FLƏŞKART  │ │ 📜 LOG     │ │ 📋 NÖVBƏ    │
│ RTDB REST  │ │ 021cars_     │ │ app_errors │ │ JSON        │
│ + SSE canlı│ │ drive.lock   │ │ .log       │ │ (offline)   │
│ JSON ağac  │ │ E:·F:·G: …   │ │ 5MB rotasiya│ │ SQL DEYİL ✗ │
└────────────┘ └──────────────┘ └────────────┘ └─────────────┘
 ☁️ məlumat      📎 ağır fayllar   🛡️ fail-safe   📤 gözləyən yazma
 (kiçik JSON)    (PDF · şəkil)     (çökmə YOX ✗)  (itki YOX ✗)
```

---

## 📂 FAYL SİYAHISI (`Docs/Firebase/`)

| # | Fayl | Vəzifəsi |
|---|---|---|
| 1️⃣ | `01_FirebaseOptions.cs.txt` | ⚙️ Ünvan · token · timeout · `Url()` ✓ |
| 2️⃣ | `02_FirebaseEntity.cs.txt` | 🧩 **`id` · `updatedAt` · `updatedBy` · `isDeleted` · `deletedAt`** + LWW ✓ |
| 3️⃣ | `03_AppLogger.cs.txt` | 📜 `app_errors.log` · rotasiya · **heç vaxt atmayan** loq ✓ |
| 4️⃣ | `04_FirebaseRestClient.cs.txt` | 🌐 GET/PUT/PATCH/DELETE · retry+backoff · **SSE real-time** ✓ |
| 5️⃣ | `05_UsbDriveDetector.cs.txt` | 💾 **`021cars_drive.lock`** aşkarlama · **NİSBİ yol** ✓✓✓ |
| 6️⃣ | `06_FirebaseRepository.cs.txt` | 🗃️ CRUD · **soft-delete** · **LWW merge** · canlı izləmə ✓ |
| 7️⃣ | `07_Modeller.cs.txt` | 🚗 POCO modellər (Avtomobil · Müştəri · Kredit …) ✓ |
| 8️⃣ | `08_BuludKonteksti.cs.txt` | ☁️ **Giriş nöqtəsi** — hamısını qurur və başladır ✓ |
| 9️⃣ | `09_MediaXidmeti.cs.txt` | 📎 Fayl → USB ✓ · metadata → bulud (nisbi yol ✓) ✓ |

> ⚠️ **`.cs.txt` uzantısı**: faylları `Services/Firebase/` qovluğuna **`.cs`** kimi köçürün ✓
> (bu formada saxlanılır ki, hazırkı Web/WPF build-i **pozulmasın** ✗✓✓)

---

## 🔄 SQL → FIREBASE XƏRİTƏSİ

| SQL (köhnə ✗) | Firebase (yeni ✓) | Ünvan ✓ |
|---|---|---|
| `CREATE TABLE cars` | `FirebaseRepository<Avtomobil>` | `021Cars/cars/{id}` ✓ |
| `SELECT * … is_deleted = 0` | `HamisiniAlAsync()` ✓ | `GET …/cars.json` ✓ |
| `INSERT INTO cars …` | `YazAsync(avtomobil)` ✓ | `PUT …/cars/{id}.json` ✓ |
| `UPDATE cars SET …` | `YazAsync(avtomobil)` ✓ (PATCH ✓) | `PUT …/cars/{id}.json` ✓ |
| `DELETE FROM cars` ✗ | `SoftSilAsync(id)` ✓✓✓ | `PATCH …/cars/{id}.json` ✓ |
| `JOIN sales ON cars.id` | `carId` sahəsi ✓ (client-side ✓) | xanada `carId` ✓ |
| `TRIGGER SET updated_at` | `FirebaseEntity.Toxun()` ✓✓✓ | hər yazmada ✓ |
| `ORDER BY` | LINQ `.OrderBy()` ✓ | keşdə ✓ |
| `UNIQUE (plate)` | `rules.json` + tətbiq yoxlaması ✓ | — |

---

## 🌳 BULUD AĞACI (`021Cars/…`)

```
021Cars/
 ├── cars/                  🚗 { id, siraNomresi, marka, …, updatedAt, updatedBy, isDeleted, deletedAt }
 ├── customers/             👤 müştərilər ✓
 ├── expenses/              💸 xərclər ✓
 ├── sales/                 🤝 satışlar ✓
 ├── creditTerms/           🏦 kredit müqavilələri ✓
 ├── creditTransactions/    💳 ödəniş · gecikmə · transfer ✓
 ├── partners/              👥 tərəfdaşlar ✓
 ├── partnerShares/         💰 bölgülər ✓
 ├── partnerPayments/       💵 tərəfdaş ödənişləri ✓
 ├── transferRecipients/    📤 transfer şəxsləri ✓
 ├── media/                 📎 { fileName, storedPath: "Media/Avtomobil/001 - …/a.jpg" ✓ }
 ├── users/                 🔐 istifadəçilər (hash + salt ✓)
 ├── usbDevices/            💾 fləşkart tokenləri ✓
 ├── settings/              ⚙️ ayarlar ✓
 └── trash/                 🗑️ silinmişlərin surəti ✓ (bərpa ✓)
```

> 💡 **`media.storedPath` HƏRFSİZDİR** ✗✓✓ → `Media/Avtomobil/001 - Hyundai Sonata - 99QE103/satis.pdf`
> Fləşkart `E:`-dən `F:`-ə keçsə də **heç nə pozulmur** ✓ — tam yol `UsbDriveDetector.TamYol()` ilə qurulur ✓

## ⚙️ İNTEQRASİYA — 3 ADDIM ✓

### 1️⃣ Faylları köçürün ✓

```
Services/Firebase/
   ├── FirebaseOptions.cs        ← 01
   ├── FirebaseEntity.cs         ← 02
   ├── AppLogger.cs              ← 03
   ├── FirebaseRestClient.cs     ← 04
   ├── UsbDriveDetector.cs       ← 05
   ├── FirebaseRepository.cs     ← 06
   ├── Modeller.cs               ← 07
   ├── BuludKonteksti.cs         ← 08
   └── MediaXidmeti.cs           ← 09
```

> ✅ **NuGet paketi LAZIM DEYİL** ✗ — `HttpClient` + `System.Text.Json` kifayətdir ✓✓✓

### 2️⃣ Token verin ✓ (⚠️ KODA YAZMAYIN ✗)

```powershell
# 🔐 Birdəfəlik — istifadəçi səviyyəsində ✓
[Environment]::SetEnvironmentVariable("CAS_FIREBASE_TOKEN", "<database-secret>", "User")

# 💾 Fləşkartı hazırlamaq:  Bulud.Usb.Hazirla(token) ✓
#    → 021cars_drive.lock + 021Cars/{Media,Senedler,Hesabatlar,Yedekler} ✓
```

**Firebase Konsol → Realtime Database → Qaydalar:**

```json
{
  "rules": {
    "021Cars": {
      ".read":  "auth != null",
      ".write": "auth != null",
      "cars":        { ".indexOn": ["qeydiyyatNisani", "status", "updatedAt"] },
      "customers":   { ".indexOn": ["telefon", "finKod", "updatedAt"] },
      "sales":       { ".indexOn": ["carId", "customerId", "satisTarixi"] },
      "creditTerms": { ".indexOn": ["carId", "status", "muqavileNomresi"] },
      "creditTransactions": { ".indexOn": ["creditId", "tarix", "nov"] },
      "expenses":    { ".indexOn": ["carId", "tarix"] },
      "partnerShares": { ".indexOn": ["refType", "refId", "partnerId"] },
      "media":       { ".indexOn": ["refType", "refId"] }
    }
  }
}
```

> 🔐 **Production:** `auth != null` yerinə sərt qaydalar ✓
> (məs. `".write": "auth.uid === 'seccad' || auth.uid === 'asif'"` ✓)

### 3️⃣ `App.xaml.cs`-də başladın ✓

```csharp
using Cas0201.Firebase;

public partial class App : Application
{
    /// <summary>☁️ Qlobal bulud konteksti ✓ (SQL YOX ✗)</summary>
    public static BuludKonteksti Bulud { get; private set; } = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            Bulud = new BuludKonteksti();     // ① ☁️ qur ✓
            await Bulud.BaslatAsync();        // ② 🚀 başlat ✓
        }
        catch (Exception ex)
        {
            // 🛡️ Yalnız bu halda xəbərdarlıq ✓ — proqram OFFLINE işləyir ✓✓✓
            AppLogger.Xeta(ex, "App.OnStartup");
        }

        new LoginWindow().Show();             // ③ 🔐 giriş ekranı ✓
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try { Bulud?.Dispose(); } catch { }   // 🧹 təmiz bağlanma ✓
        base.OnExit(e);
    }
}
```

---

## 📜 LOQ FAYLI HARADADIR? ✓✓✓

```
① %LOCALAPPDATA%\EnterpriseAeroStudio\Logs\app_errors.log   ← ✅ ƏSAS ✓ (layihə standartı ✓)
② {exe qovluğu}\Logs\app_errors.log                         ← 🔁 fallback (icazə yoxdursa ✗)
③ %TEMP%\021Cars\Logs\app_errors.log                        ← 🆘 son çarə ✓
```

### 📍 Tam yol (bu kompüterdə ✓)

```
C:\Users\021Cars_User\AppData\Local\EnterpriseAeroStudio\Logs\app_errors.log
```

> ℹ️ Başqa kompüterdə `<İstifadəçi>` dəyişir ✓ — yol HƏMİŞƏ eyni qalır ✓
> (`Environment.SpecialFolder.LocalApplicationData` ilə tapılır ✓)

### 🔎 Necə tapmaq / açmaq ✓

| Nə istəyirsiniz | Nə etməli ✓ |
|---|---|
| 📂 **Qovluğu açmaq** | `AppLogger.LoqlariAc();` ✓ (Explorer açılır ✓) |
| 📄 **Faylı Notepad-də açmaq** | PowerShell: `notepad "$env:LOCALAPPDATA\EnterpriseAeroStudio\Logs\app_errors.log"` ✓ |
| 📜 **Kodda son 200 sətir** | `var s = AppLogger.SonSetirler(200);` ✓ |
| 📏 **Ölçüsünü yoxlamaq** | `AppLogger.OlcuKB` ✓ · `AppLogger.LoqVar` ✓ |
| 🧹 **Təmizləmək** | `AppLogger.LoquTemizle();` ✓ |
| ♻️ **Rotasiya** | 5 MB-dan sonra → **`app_errors.old.log`** ✓ (eyni qovluqda ✓) |

### 🎛️ UI düyməsi (MainWindow-a əlavə edin ✓)

```xml
<Button Content="📜 Loqnu aç" Click="Loq_Click" Padding="12,6"/>
```

```csharp
private void Loq_Click(object sender, RoutedEventArgs e)
    => AppLogger.LoqlariAc();   // 📂 %LOCALAPPDATA%\EnterpriseAeroStudio\Logs ✓
```

### ⚠️ Niyə `%LOCALAPPDATA%` (exe qovluğu deyil ✗)?

| Səbəb | İzah |
|---|---|
| 🔒 **Program Files** | Tətbiq `C:\Program Files\…`-a quraşdırılsa **yazmaq QADAĞANDIR** ✗ → loq heç yazılmazdı ✗✓✓ |
| 📐 **Layihə standartı** | `%LOCALAPPDATA%\EnterpriseAeroStudio\` → `avtopark.db` · `Media\` · **`Logs\`** ✓ (mövcud README ilə eyni ✓) |
| 💾 **Disk təmizliyi** | `bin\…` yenidən qurulanda **silinmir** ✗✓✓ (build loqları məhv etmir ✓) |
| 👥 **Çox istifadəçi** | Hər Windows istifadəçisinin öz loqu olur ✓ (icazə problemləri yox ✗) |

> 🛡️ **Zəmanət:** ① yazıla bilmirsə → ② exe qovluğu ✓ → o da olmazsa ③ TEMP ✓
> — heç biri işləməzsə belə proqram **ÇÖKMÜR** ✗✓✓ (`catch { }` ✓)

---

## 💻 İSTİFADƏ NÜMUNƏLƏRİ ✓

```csharp
var bulud = App.Bulud;

// ➕ YENİ AVTOMOBİL ✓
var avto = new Avtomobil
{
    Marka = "Hyundai Sonata", QeydiyyatNisani = "99QE103", Il = 2019,
    AlisQiymeti = 24500, MayaDeyeri = 24500, Status = "Stokda", SiraNomresi = 1
};

await bulud.Avtomobiller.YazAsync(avto);   // ☁️ PUT 021Cars/cars/{id} ✓
// → id ✓ updatedAt ✓ updatedBy ✓ isDeleted=false ✓ AVTOMATİK yazıldı ✓

// 📥 SİYAHI (soft-delete olunanlar GƏLMİR ✗)
var stok = (await bulud.Avtomobiller.HamisiniAlAsync())
           .Where(a => a.Status == "Stokda")
           .OrderBy(a => a.SiraNomresi);

// 💰 XƏRC ƏLAVƏ ET ✓
await bulud.Xercler.YazAsync(new Xerc
{
    CarId = avto.Id, Mebleg = 120.50, Qrup = "Texniki",
    Teyinat = "Yağ dəyişmə", Tarix = DateTime.Now.ToString("yyyy-MM-dd")
});

// 📎 SƏNƏD/ŞƏKİL ƏLAVƏ ET ✓ (fayl → USB ✓ · buludda NİSBİ yol ✓)
var bytes = File.ReadAllBytes(@"C:\Temp\sonata.jpg");

var media = await bulud.Media.ElaveEtAsync(
    refType: "Avtomobil", refId: avto.Id,
    fileName: "sonata-on.jpg", məzmun: bytes,
    kateqoriya: "Media", siraNomresi: avto.SiraNomresi,
    marka: avto.Marka, qeydiyyatNisani: avto.QeydiyyatNisani);

if (media is null)
{
    // 🔌 Fləşkart YOXDUR ✗ — proqram ÇÖKMÜR ✗, sadəcə xəbərdarlıq ✓✓✓
    MessageBox.Show(bulud.Media.VeziyyetMetni, "Fləşkart tapılmadı ✗");
}
// ✅ media.StoredPath = "Media/Avtomobil/001 - Hyundai Sonata - 99QE103/sonata-on.jpg" ✓

// 🔓 SƏNƏDİ AÇ ✓ (fləşkart E: → F: dəyişsə də İŞLƏYİR ✓)
var data = bulud.Media.FayliAc(media!);
var tamYol = bulud.Media.TamYol(media!);   // PDF çapı / WebView2 üçün ✓

// 🗑️ SOFT-DELETE ✓ (buluddan SİLİNMİR ✗ — bərpa mümkündür ✓)
await bulud.Avtomobiller.SoftSilAsync(avto.Id);
// await bulud.Avtomobiller.BerpaEtAsync(avto);   // ♻️ geri qaytar ✓

// 📡 REAL-TIME ✓ — başqa cihaz dəyişsə dərhal xəbər tuturuq ✓
bulud.Avtomobiller.Deyisdi += a => Dispatcher.Invoke(() => SiyahınıYenile(a));
bulud.Avtomobiller.Silindi  += id => Dispatcher.Invoke(() => SetirdenSil(id));

// 📊 VƏZİYYƏT PANELİ ✓
Basliq.Text = bulud.Onlayn
    ? $"✅ Onlayn ✓ · 📋 növbə: {bulud.Növbədə} · 🕒 {AppLogger.SonSinxron:HH:mm:ss}"
    : $"🔌 OFFLINE ✗ · 📋 növbə: {bulud.Növbədə} (qoşulanda göndəriləcək ✓)";
```

---

## 🛡️ FAIL-SAFE ZƏMANƏTLƏRİ ✓✓✓

| Risk ✗ | Nə baş verir | Zəmanət ✓ |
|---|---|---|
| 🌐 İnternet qopdu | Yazma **növbəyə** düşür ✓ · oxuma boş qayıdır ✓ | `CəhdAsync` + retry + `NövbəniBoşaltAsync` ✓ |
| 💾 Fləşkart taxılmadı | Yalnız sənəd əməliyyatı bloklanır ✓ | `Usb.Hazir == false` → `null` ✓ |
| 💽 Fləşkart hərfi dəyişdi | **HEÇ NƏ OLMAZ** ✓ | Yol NİSBİ saxlanılır ✓✓✓ |
| 🔥 Token yanlışdır | Təkrar cəhd YOX ✗ → loqa yazılır ✓ | `FirebaseAuthException` ✓ |
| 🗑️ Səhvən silindi | `isDeleted=true` + `deletedAt` ✓ | `BerpaEtAsync()` ✓ |
| ⚡ İki cihaz eyni anda | **LWW** — ən təzə üstün ✓ | `UstunTut()` + `MergeEtAsync()` ✓ |
| 💥 Gözlənilməyən xəta | Proqram **işləyir** ✓ | try-catch ✓ · `app_errors.log` ✓ |

---

## ⚠️ TÖVSİYƏLƏR

| Mövzu | Tövsiyə |
|---|---|
| 💰 **Pul** | RTDB rəqəmi `double`-dur ✗ → qəpik itkisi olur ✗ · **qəpik (long)** və ya `string` saxlayın ✓ |
| 📎 **Fayllar** | Buludda YALNIZ yol ✓ → **fləşkart itirsə fayllar da itir** ✗ · `Yedekler/` qovluğuna nüsxə ✓ |
| 🗑️ **Zibil** | `HamisiniAlAsync(zibilDaxil: true)` ✓ · `ZibilAsync()` ✓ · admin «bərpa» ekranı ✓ |
| 🧹 **Yetim fayllar** | `Media.YetimFayllarAsync()` ✓ — buludda qeydi olmayan fayllar ✓ |
| 🔒 **Təhlükəsizlik** | Anonim girişi **SÖNDÜRÜN** ✗ · parollar hash+salt ✓ |
| 🌍 **Miqrasiya** | Köhnə `021cars_data.db` ✗ → `YazAsync(…)` dövrəsi ilə buluda köçürün ✓ |

---

## ✅ YOXLAMA SİYAHISI ✓

- [ ] `Services/Firebase/` — 9 fayl `.cs` kimi köçürüldü ✓
- [ ] `CAS_FIREBASE_TOKEN` mühit dəyişəni qoyuldu ✓ · **koda yazılmadı** ✗
- [ ] Firebase `rules` + `.indexOn` yazıldı ✓
- [ ] Fləşkart hazırlandı → `021cars_drive.lock` + `021Cars/{Media,Senedler,Hesabatlar,Yedekler}` ✓
- [ ] `App.xaml.cs` → `Bulud = new BuludKonteksti(); await Bulud.BaslatAsync();` ✓
- [ ] Tətbiq açıldı → `Logs/app_errors.log`-da «☁️ Bulud konteksti hazırdır» ✓
- [ ] 🔌 Fləşkart **çıxarıldı** → tətbiq **çökmür** ✗ · yalnız sənəd düymələri sönür ✓
- [ ] 🌐 İnternet **kəsildi** → yazma işləyir ✓ (növbə artır ✓) · qoşuldu → növbə boşaldı ✓
- [ ] 🖥️🖥️ İki kompüter → birində dəyişiklik → digərində **dərhal** göründü ✓✓✓
- [ ] 🗑️ Silmə → digər cihazda da **yox oldu** ✓ · bərpa edildi → **qayıtdı** ✓


