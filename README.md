# 🚗 021Cars — Avtomobil Parkı

> **Avtomobil parkı idarəetmə sistemi** — masaüstü (WPF) + veb + Firebase bulud sinxronizasiyası + 💾 USB yedəkləmə + 🚀 **avtomatik güncəlləmə**

---

## 📦 MÜŞTƏRİ ÜÇÜN QURAŞDIRMA

**YALNIZ BİR FAYL** kifayətdir ✓ — başqa heç nə lazım deyil ✗:

```
021Cars_Installer.exe      ← 📥 GitHub → Releases → ən son buraxılışdan yükləyin
```

1. 🖱️ İki dəfə klikləyin → 🔐 UAC «Bəli» ✓
2. 📂 Yeri seçin (default `C:\Program Files\021Cars`) ✓
3. 📦 **QURAŞDIR** → masaüstündə qısayol yaranır ✓
4. 🗑️ Silmək üçün: `C:\Program Files\021Cars\Uninstaller.exe` ✓

> 💡 Bütün məlumatlar **quraşdırma qovluğunda** saxlanılır ✓ (AppData-da **heç nə** ✗)

> 🆕 **QURAŞDIRICI SIFIR GƏLİR** ✓✓✓ — içində **heç bir** avtomobil · kredit ·
> xərc olmur ✗ (yalnız boş baza yaranır ✓). Məlumat ya əl ilə, ya da
> **«📜 Skript İdxalı»** tabından skriptlə əlavə olunur ✓✓✓

---

## 🆕 VERSİYA 6.2.2 — 🌊 SƏHİFƏ SCROLL-U + 🐞 ÇÖKMƏ DÜZƏLİŞİ

| # | Nə düzəldi |
|---|---|
| ① | 🌊 **BÜTÜN tablara SƏHİFƏ SCROLL-U** ✓ — Avto Park · Satış · Kreditlər · Qrafik · Əlavə Gəlir/Xərc · Satılan & Bitmiş · Tərəfdaşlar (Ümumi Xərclər · Maliyyə · Tənzimləmələr · Kalkulyator-da artıq var idi ✓) → uzun forma + cədvəl **BİRLİKDƏ** sürüşür ✓✓✓ |
| ② | 🐞 **«An ItemsControl is inconsistent with its items source»** çökməsi ✗ → `BulkObservableCollection.AddRange` artıq **tək-tək `Add()`** edir ✓ (WPF çox-elementli bildirişi dəstəkləmir ✗) + yükləmə `LoadingRow`-dan çıxarıldı ✓ (600 ms taymer ✓) |
| ③ | ⚡ **VİRTUALİZASİYA QORUNDU** ✓ — səhifə scroll-u içindəki cədvəllər `MaxHeight` ilə pəncərə hündürlüyünə məhdudlaşdırılır ✓ · **`FallbackValue=700` MÜTLƏQ LAZIMDIR** ✓✓✓ (əks halda ilk ölçmə sonsuz hündürlüklə keçir ✗ → 20 000 sətir = **64 SANİYƏ** ✗; `FallbackValue` ilə **0.4 saniyə** ✓) |
| ④ | 🧹 **Publish təmizliyi** ✓ — `qur.ps1` və `Yig-Installer.ps1` artıq `obj`/`bin`-i **təmizləyir** ✓ (köhnə qalıqlar .NET 10 fayllarını qarışdırırdı ✗ → proqram açılmırdı ✗✓✓) |
| ⑤ | 🌐 **Veb tətbiq AYRI «Web\» qovluğuna** köçürüldü ✓ (web .NET 10, masaüstü .NET 8 → eyni qovluqda runtime TOQQUŞURDU ✗✓✓) |

---

## 🚀 AVTOMATİK GÜNCƏLLƏMƏ (necə işləyir)

| # | Nə baş verir |
|---|---|
| ① | Proqram **AÇILANDA** ~15 saniyə sonra GitHub-a baxır ✓ |
| ② | Sonra **HƏR 30 DƏQİQƏDƏ BİR** yoxlayır ✓ |
| ③ | Yeni versiya varsa → 🎉 **POPUP**: `[🚀 GÜNCƏLLƏ]` `[⏰ SONRA]` ✓ |
| ④ | **GÜNCƏLLƏ** → installer yüklənir (⬇️ faizlə) → proqram özü bağlanır ✗ → fayllar dəyişir ✓ → proqram **YENİDƏN AÇILIR** ✓✓✓ |
| ⑤ | **SONRA** → bu sessiyada susur ✗ — **növbəti AÇILIŞDA** yenidən soruşur ✓✓✓ |

> 🛡️ **İSTİFADƏÇİ MƏLUMATLARI HƏMİŞƏ QORUNUR** ✓✓✓ —
> `avtopark.db` · `*.db-wal` · `*.db-shm` · `istifadeciler.json` · `bulud_ayarlari.json` ·
> `transfer-sexler.json` · `Media\` · `Logs\` · `Yedekler\` · `Hesabatlar\` · `AutocodePDF\`

---

## 📤 YENİ VERSİYA BURAXMAQ (developer üçün)

> ⚠⚠ **VACİB — PUBLISH TƏMİZ OLMALIDIR** ✓✓✓ (2026-09-29-dan ★)
>
> `qur.ps1` və `Yig-Installer.ps1` artıq `bin`/`obj`-i **özü** təmizləyir ✓ —
> əl ilə `dotnet publish` edirsinizsə, **əvvəlcə** silin:
>
> ```powershell
> Remove-Item bin\Release\net8.0-windows, obj\Release\net8.0-windows -Recurse -Force
> ```
>
> Əks halda veb layihəsinin (.NET 10) runtime faylları masaüstü (.NET 8)
> çıxışına **QARIŞIR** ✗ → `coreclr.dll` 10.x ✗ + `WindowsBase.dll` 8.x ✗ →
> «Could not load System.Runtime / WindowsBase» ✗ → **proqram HEÇ AÇILMIR** ✗✓✓
>
> 🌐 Veb tətbiq isə **«Web\» alt qovluğunda** saxlanılır ✓ (öz .NET 10
> runtime-ı ilə ✓ — eyni qovluqda rüntaym toqquşur ✗✓✓)

### Adım 1 — 🔢 Versiyanı artırın

```xml
<!-- EnterpriseAeroStudio.csproj -->
<Version>6.2.2</Version>

<!-- Docs\Qurasdirma\Installer\021Cars_Installer.csproj -->
<Version>6.2.2</Version>
```

### Adım 2 — 📦 Installer-i yığın

```powershell
cd C:\Users\021Cars_User\Desktop\Autocode\Docs\Qurasdirma\Installer
.\Yig-Installer.ps1
```
→ `publish_setup\021Cars_Installer.exe` ✓ (tək fayl ✓ · hər şey içində ✓)

### Adım 3 — 🚀 GitHub-a buraxın

```powershell
cd C:\Users\021Cars_User\Desktop\Autocode\Docs\Qurasdirma
.\GitHub-Paylash.ps1 -Burax -Qeyd "🚀 nə dəyişdi..."
```

**Skript avtomatik edir:**
- 📤 kodu `github.com/Seccad-Elizade/021Cars`-a push edir ✓
- 🏷️ `v6.2.2` teqini yaradır ✓
- 📦 **RELEASE** yaradır və installer-i **ASSET** kimi yükləyir ✓✓✓

> 🔐 **Bir dəfə** token lazımdır: https://github.com/settings/tokens → «repo» →
> `$env:GITHUB_TOKEN = "ghp_..."`
> 🖥️ **Bütün kompüterlər** 30 dəqiqə içində görür və popup çıxarır ✓✓✓

---

## ❓ SUAL: GitHub-a `installer.exe`-i qoymalıyam, yoxsa proqram fayllarını?

| Nə ✗ | Haraya ✗ | Niyə ✓ |
|---|---|---|
| ❌ Proqram fayllarını (660+ fayl) | Repo-ya | Gərəksizdir ✗ — installer **hamısını özü içində daşıyır** ✓ |
| ❌ `021Cars_Installer.exe` | Repo-ya (`git push`) | **146 MB** ✗ — GitHub fayl limiti **100 MB**-dır ✗✗✗ |
| ✅ **`021Cars_Installer.exe`** | **RELEASE → Assets** ✓✓✓ | Limit **2 GB** ✓ — skript bunu **avtomatik** edir ✓ |

**Nəticə:** kod `git push` ilə ✓ + installer **Release asset** kimi ✓
(`GitHub-Paylash.ps1 -Burax` ✓✓✓)

---

<details>
<summary>📚 <b>ƏTRAFLI SƏNƏDLƏŞMƏ (veb · DNS · WiFi · router)</b> — açmaq üçün klikləyin ✓</summary>


Bu layihə masaüstü (WPF) tətbiqin **veb interfeysidir**. Eyni biznes məntiqini
(`Services`), eyni verilənlər bazasını və eyni fayl arxivini istifadə edir.

```
Desktop\
└── Autocode\                    ← TƏK PROQRAM QOVLUĞU
    ├── EnterpriseAeroStudio.csproj   ← Masaüstü tətbiq (WPF, .NET 8)
    ├── Models\ Data\ Services\ ViewModels\ Views\ Hosting\
    ├── Web\                          ← Veb tətbiq (Blazor Server, .NET 10)
    │   ├── Autocode.Web.csproj
    │   ├── Program.cs
    │   ├── Components\ Services\ wwwroot\
    │   └── appsettings.json
    ├── AVTOPARK.bat                  ← Başlatma skripti
    ├── ishe-sal.bat
    ├── SAC-SONDUR.bat
    └── README.md
```

> 🎯 **TƏK PROQRAM**: masaüstü və veb tətbiq **eyni qovluqdadır** və
> **eyni `bin`/`obj` ağacını** paylaşır. Masaüstü tətbiq build edildikdə
> veb tətbiq də **avtomatik** build olunur (`ProjectReference`).

---

## 📶 YALNIZ WiFi ÜÇÜN — ən sadə yol

**Domen lazım deyil. İnternet lazım deyil. Pulsuzdur.**

### ⭐ TƏK FAYL: `AVTOPARK.bat`

**Sağ klik → "Run as administrator"** (bir dəfə icazə soruşacaq).
Bundan sonra hər gün sadəcə ikiqat klik.

Fayl özü **hər şeyi** edir:

| # | Nə edir |
|---|---|
| 1 | **Admin hüququnu alır** (özünü yüksəldir — UAC soruşur) |
| 2 | Köhnə serveri dayandırır (port konflikti olmasın) |
| 3 | **Firewall qaydaları** — TCP 80, 5000 + UDP 53 |
| 4 | **`021cars.az` → bu kompüter** (hosts faylı, avtomatik) |
| 5 | Veb serveri başladır: **port 80 + 5000 + yerli DNS serveri** |
| 6 | Hər şeyi yoxlayır və ünvanları göstərir |

### Ekranda görəcəksiniz

```
======================================================================
   HAZIRDIR!
======================================================================

   BU KOMPUTERDE:
       http://021cars.az
       http://localhost

   WIFI-DEKI CIHAZLAR ucun (telefon / planset / basqa komputer):
       A)  http://192.168.0.186          (derhal isleyir)

       B)  Cihazda DNS = 192.168.0.186  etseniz:
           http://021cars.az             (domen kimi!)

   GIRIS SIFRESI :  appsettings.json  ->  AdminPassword
======================================================================
```

### 🌐 `021cars.az` — DOMEN ALMADAN işləyir!

Proqramın içində **kiçik DNS serveri** var:

- **Bu kompüterdə:** hosts faylı avtomatik yazılır → `http://021cars.az` **dərhal işləyir**
- **Telefon/planşetdə:** cihazın **DNS-ini `192.168.0.186`** etsəniz → `http://021cars.az` açılır
- Digər bütün domenlər (google.com və s.) **yuxarı DNS-ə ötürülür** — internet itmir

> ✅ Test edildi: `021cars.az` → `192.168.0.186` ✅ &nbsp;|&nbsp; `google.com` → normal işləyir ✅

### 📱 Digər cihazlarda (telefon / planşet)

**Niyə işləmir?** Çünki `hosts` faylı YALNIZ server kompüterinə təsir edir.
Digər cihazlar `021cars.az`-ı tanımır. **3 həll yolu var:**

---

#### ✅ A) DERHAL İŞLƏYƏN — heç bir ayar lazım deyil

Telefonda brauzeri açıb yazın:
```
http://192.168.0.186
```
> `192.168.0.186` = server kompüterinin IP-si (`AVTOPARK.bat` onu göstərir)

Bu **hər cihazda dərhal işləyir**. Yeganə çatışmazlıq: "021cars.az" yazmaq olmur.

---

#### ⭐ B) ROUTER-DƏ BİR DƏFƏ AYAR — bütün cihazlar üçün `021cars.az`

**Bu ən yaxşı həlldir.** Bir dəfə router-də edin — bundan sonra WiFi-ə qoşulan
**bütün** cihazlar `http://021cars.az` yaza biləcək (heç bir telefon ayarı lazım deyil).

`router-dns.bat` faylına **ikiqat klik** — o:
1. Server IP-sini və router IP-sini göstərir
2. Router panelini brauzerdə açır
3. Hansı menyuya girəcəyinizi addım-addım deyir

**Nə etməli:**
```
1) Router paneli:  http://192.168.0.1   (adətən admin / admin)
2) Bu bölməni tapın:  DHCP  ->  DNS Server
3) Yazın:   Birinci DNS = 192.168.0.186
            İkinci DNS  = 8.8.8.8          <-- VACİB!
4) Yadda saxla -> router yenidən başladın
```

| Router | Yol |
|---|---|
| **TP-Link** | DHCP → DHCP Settings → Primary DNS |
| **Keenetic** | Ev Şəbəkə → Parametrlər → DNS |
| **Asus** | LAN → DHCP Server → DNS Server |
| **D-Link** | Setup → Network Settings → DNS |
| **Huawei** | Şəbəkə → LAN → DHCP → DNS |
| **ZTE** | LAN → DHCP → DNS |
| **Mi / Xiaomi** | Komut Ayarları → DHCP → DNS 1 |

> ⚠️ **İkinci DNS = `8.8.8.8` mütləqdir!** Əks halda server kompüteri söndükdə
> bütün WiFi-də internet işləməyəcək. İkinci DNS fallback rolunu oynayır.

---

#### 🔧 C) YALNIZ BİR CİHAZ ÜÇÜN

| Sistem | Yol |
|---|---|
| **Android** | Wi-Fi → şəbəkəyə uzun bas → *Şəbəkəni dəyiş* → *Qabaqcıl* → IP: **Statik** → DNS 1: `192.168.0.186` |
| **iPhone** | Wi-Fi → şəbəkə yanındaki ⓘ → *IP-ni konfiqurasiya et* → **Manual** → DNS: `192.168.0.186` |
| **Windows** | Şəbəkə → Xüsusiyyətlər → IPv4 → DNS: `192.168.0.186` |

---

### 🌐 `021cars.az` — DOMEN ALMADAN işləyir!

Proqramın içində **kiçik DNS serveri** var (`MiniDnsServer.cs`):

- `021cars.az` soruşulanda → **bu kompüterin IP-sini qaytarır**
- Bütün digər domenlər (google.com və s.) → **yuxarı DNS-ə ötürülür** — internet itmir

> ✅ Protokol səviyyəsində test edildi:
> `021cars.az` → `192.168.0.186` ✅ &nbsp;|&nbsp; `www.021cars.az` → `192.168.0.186` ✅
> `google.com` → `172.217.20.78` (ötürmə işləyir) ✅

> ⚠️ DNS serveri **port 53**-də işləyir — bu port üçün **admin hüququ** lazımdır.
> Ona görə `AVTOPARK.bat`-ı **admin kimi** işlədin.

### 🔐 Şifrə

Standart: `021cars` → `appsettings.json` → `AdminPassword`.

İstəmirsinizsə: `"RequireLogin": false`

---

## 🖥️ Veb Sayt masaüstü tətbiqin İÇİNDƏ

Masaüstü tətbiq (`EnterpriseAeroStudio.exe`) veb serveri **avtomatik** işə salır —
ayrıca heç nə etmək lazım deyil.

### 🌐 «Veb Sayt» tabı

Tətbiqin **ən sonuncu tabında** (aşağıda) `🌐 Veb Sayt` bölməsi var:

| Blok | Nə göstərir |
|---|---|
| **Vəziyyət** | 🟢 **İşləyir** / 🔴 **Dayandırılıb** (canlı) |
| **▶ Başlat** | Veb serveri işə salır |
| **⏹ Dayandır** | Veb serveri dayandırır |
| **🔄 Yenidən başlat** | Serveri yenidən başladır |
| **↻ Yenilə** | Daxili brauzeri yeniləyir |
| **Giriş ünvanları** | 3 düymə — klik = xarici brauzerdə açır |
| **Daxili brauzer** | WebView2 — saytı **birbaşa proqramın içində** göstərir |

### Giriş ünvanları

```
🟣 http://021cars.az      ← domen (hosts faylı ilə)
🔵 http://localhost:5000  ← bu kompüter
🟠 http://192.168.1.51    ← telefon / planşet
```

### Necə işləyir

```
┌──────────────────────────────────────────────────────────────┐
│  EnterpriseAeroStudio.exe  (masaüstü tətbiq)                  │
│                                                              │
│   İşə düşür  ──►  WebServerService  ──►  ayrı proses:        │
│                                          EnterpriseAeroStudio │
│                                            .Web.exe          │
│                                                              │
│   Bağlanır   ──►  Dispose()  ──►  veb server də dayanır      │
└──────────────────────────────────────────────────────────────┘
```

| Xüsusiyyət | İzah |
|---|---|
| **Avtomatik başlatma** | Proqram açılan kimi veb server işə düşür |
| **Avtomatik dayandırma** | Proqram bağlananda veb server də dayanır |
| **Canlı vəziyyət** | Hər 3 saniyədə yoxlanılır |
| **Canlı log** | Serverin çıxışı toplanır (300 sətir) |
| **Daxili brauzer** | WebView2 (Edge) — əlavə quraşdırma lazım deyil* |
| **Xəta göstərilməsi** | Server başlamasa, səbəb tabda görünür |

> \* WebView2 Runtime Windows 11-də **standart olaraq quraşdırılmışdır**.
> Yoxdur deyilsə, ünvan düymələri xarici brauzeri açır.

### 🔒 Veb tətbiqinin tapılması

`WebServerService` veb tətbiqini avtomatik tapır:
1. Masaüstü tətbiqin **yanındaki** qovluq (publish halı)
2. Yuxarı qovluqlarda **`Autocode.Web`** → ən yeni build çıxışı

> ⚠️ Veb layihə **bir dəfə build edilməlidir** (`dotnet build`).
> Edilməyibsə tabda *«Veb tətbiqi tapılmadı»* yazısı görünür.


### ⚠️ Vacib

- Server penceresi **açıq qalmalıdır**
- Kompüter yuxuya getməsin → *Parametrlər → Güc → Yuxu: Heç vaxt*
- Bütün cihazlar **eyni WiFi-də** olmalıdır

---
## 📜 «Skript İdxalı» TABI (JSON) ✓✓✓

Köhnə məlumatları **bir dəfəyə** proqrama köçürmək üçün yeni tab ✓
(əvvəl Excel/kağız siyahısı — indi **skript yapışdır → maşınlar düşür** ✓)

```
📜 Skript İdxalı
 ├── 📜 Skript  (JSON mətnini buraya yapışdırın ✓)
 └── 📊 Nəticə hesabatı  (nə yazıldı — sətir-sətir ✓)
     🔎 Yoxla   📥 İdxal et   📋 Nümunə   🧹 Təmizlə
```

### Necə işləyir — 3 addım

| # | Nə edilir | Nəticə |
|---|---|---|
| ① | JSON skriptini **yapışdırın** (Ctrl+V ✓) | tab-ın sol tərəfində görünür ✓ |
| ② | **🔎 Yoxla** | Bazaya **heç nə yazılmır** ✗ — nə olacağı göstərilir ✓ |
| ③ | **📥 İdxal et** | Avtomobil · xərc · tarix · qeyd — **hamısı** yazılır ✓✓✓ |

### Skriptin formatı

```json
[
  {
    "SiraNomresi": null,
    "MarkaModel": "Mercedes E240",
    "DovletNomresi": "90-DV-174",
    "Il": 1998,
    "AlisTarixi": "09.07.2023",
    "AlisQiymeti": 10820.00,
    "Xercler": [
      { "Təsvir": "Elsen", "Tarix": "09.07.2023", "Məbləğ": 40.00 },
      { "Təsvir": "Qeydiyyat", "Tarix": "10.07.2023", "Məbləğ": 260.00 }
    ],
    "Qeydler": "Texniki baxışı 30.07.23-də bitir"
  }
]
```

| Sahə | Mənası |
|---|---|
| `SiraNomresi` | **boş buraxın** (`null`) → proqram ardıcıl nömrə verir ✓ |
| `MarkaModel` | marka / model ✓ |
| `DovletNomresi` | dövlət nömrəsi ✓ (artıq varsa → **keçilir** ✗, köhnə məlumat dəyişmir ✓) |
| `Il` | buraxılış ili ✓ |
| `AlisTarixi` | `09.07.2023` ✓ (həm də `2023-07-09` ✓) |
| `AlisQiymeti` | alış qiyməti ✓ (avtomatik «Alış» xərc sətri yaradılır ✓) |
| `Xercler` | xərclər: `Təsvir` · `Tarix` · `Məbləğ` ✓ (boş ola bilər ✓) |
| `Qeydler` | qeyd → **ƏN AXIRINCI xərcin** «Qeyd» xanasına yazılır ✓✓✓ |

> 💡 **Dəqiqlik:** hər avtomobilin **ALIŞ + XƏRCLƏR = ÜMUMİ MAYA** dəyəri
> nəticə hesabatında sətir-sətir göstərilir ✓ və proqramdaki cəmlə eyni olur ✓✓✓

### 🔤 KATEQORİYA / QRUP UYĞUNLAŞDIRMASI (avtomatik ✓)

Skriptdə ad **`a/e`** ilə yazılsa da proqramda **`ə`** ilə ola bilər —
sistem bunu **özü tanıyır** ✓✓✓:

| Skriptdə | Proqramda tapılan | Nə olur |
|---|---|---|
| `Elsen` | **`Elşən`** | mövcud kateqoriya ✓ (ə → e) |
| `Çərimə ödənişi` | **`Cərimə`** | mövcud kateqoriya ✓ |
| `Usta xərci` | **`Usta haqqı (Digər)`** | açar söz → qrup ✓ |
| `Maşın bağlanma dəyəri` | **`Maşının bağlanma dəyəri`** | oxşarlıq ✓ |
| `Rəşid` (yeni ad) | — | **yeni kateqoriya** → «👤 Usta & İşçilik Haqqı» ➕ |
| `Termostat` · `Radiator` | — | **yeni kateqoriya** → «⚙️ Mühərrik & Ehtiyat Hissələri» ➕ |

> 📁 Qrup **yoxdursa avtomatik yaradılır** ✓ — kateqoriya yoxdursa
> skriptdəki adı ilə əlavə olunur ✓ (hesabatda `➕` ilə görünür ✓)
> Sıra: ① tam uyğunluq (ə/e fərqi nəzərə alınmır) → ② açar sözlər →
> ③ oxşarlıq (≥62 %) → ④ şəxs adı → ⑤ yeni kateqoriya ✓

### 🛡️ Təhlükəsizlik

- **🔎 Yoxla** heç nə yazmır ✗ (sərbəst sınamaq olar ✓)
- **Təkrar nömrə** → avtomobil **keçilir** ✓ (iki dəfə idxal zərərsizdir ✓✓✓)
- Skript daxilində eyni nömrə təkrarlanırsa → ikinci sətir yazılmır ✓
- Oxunmayan tarix / boş sahə → **xəbərdarlıq** verilir ✓ (idxal dayanmır ✓)

---



## 🛠️ PROBLEM: Düymələr işləmir

**Əlamət:** Səhifələr açılır, cədvəllər görünür, AMMA *«Yeni Avtomobil»*,
*«Əlavə et»*, *«Sil»* kimi düymələrin **heç biri reaksiya vermir.**

### Səbəb

Brauzer **`blazor.web.js`** faylını yükləyə bilmir. Bu fayl olmadan
səhifə yalnız **statik görüntüdür** — interaktivlik yoxdur.

`blazor.web.js` .NET-in **daxili (framework) statik aktividir** və fiziki olaraq
NuGet paketində saxlanılır:

```
%USERPROFILE%\.nuget\packages\
    microsoft.aspnetcore.app.internal.assets\<versiya>\_framework\blazor.web.js
```

`dotnet run` zamanı tətbiq **Production** rejimində işləyir və ASP.NET bu
qovluğu **avtomatik qoşmur** (yalnız Development-də və ya `dotnet publish`
edilmiş çıxışda qoşulur). Nəticədə fayl tapılmır:

```
FileNotFoundException: Could not find file
    '...\Autocode.Web\wwwroot\_framework\blazor.web.js'
[WRN] The application is not running against the published output and
      Static Web Assets are not enabled.
```

### Həlli

`Program.cs` faylında `_framework` qovluğu **əl ilə qoşulur**
(<c>FindFrameworkAssetsFolder()</c> metodu onu NuGet paketindən tapır):

```csharp
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(frameworkAssets),
    RequestPath = "/_framework"
});
```

> ⚠️ `<c>app.MapStaticAssets()</c>` **istifadə edilmir** — o, Production
> rejimində `FileNotFoundException` atır və düymələri tamamilə sıradan çıxarır.

### Yoxlama

```powershell
# Server işləyərkən:
(Invoke-WebRequest http://localhost:5000/_framework/blazor.web.js -UseBasicParsing).RawContentLength
#   195000+  ->  hər şey qaydasındadır
#   404/500  ->  DÜYMƏLƏR İŞLƏMİR
```

Ən asan yol: **`http://021cars.az/diaqnostika`** səhifəsini açın.

---

## 🛠️ PROBLEM: Köhnə kod işləyir (build uğursuz olur)

**Əlamət:** Kod dəyişdirilib, amma heç nə dəyişmir.

```
error MSB3027: Could not copy "...\EnterpriseAeroStudio.Web.exe".
The file is locked by: "EnterpriseAeroStudio.Web.exe (12345)"
```

### Səbəb

**Köhnə server penceresi hələ də açıqdır** və DLL faylını kilidləyir →
yeni kod yazıla bilmir → köhnə kod işləməyə davam edir.

### Həlli

1. **Bütün** `AvtoPark - Server` pencerelerini bağlayın (`Ctrl+C`)
2. `AVTOPARK.bat`-ı **yenidən** işə salın

> ✅ `ishe-sal.bat` bunu **avtomatik yoxlayır**: build xəta versə,
> serveri **ümumiyyətlə başlatmır** və ekranda aydın xəbərdarlıq göstərir.

---

## 🛡️ PROBLEM: «Smart App Control has blocked part of this app»

**Əlamət:** Tətbiq **ümumiyyətlə açılmır**. Ekranda Windows-un mesajı:
```
Smart App Control has blocked part of this app
```

Və ya `dotnet build` / `dotnet ef` yazanda:
```
System.IO.FileLoadException: An Application Control policy has blocked this file. (0x800711C7)
```

### Səbəb

Windows 11-in **Smart App Control** funksiyası **rəqəmsal imzası olmayan**
tətbiqləri bloklayır. `EnterpriseAeroStudio.exe` lokal olaraq yığıldığı üçün
imzası yoxdur → bloklanır.

**Yoxlama** (PowerShell):
```powershell
(Get-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Control\CI\Policy').VerifiedAndReputablePolicyState
#   0 = söndürülüb   |   1 = AKTİVDİR (bloklayır!)   |   2 = qiymətləndirmə
```

### Həlli — 2 yol

#### ✅ A) Avtomatik (tövsiyə olunur)

`SAC-SONDUR.bat` faylına **sağ klik → "Run as administrator"**

Skript:
1. Admin hüququnu alır
2. Vəziyyəti yoxlayır
3. **Xəbərdarlıq** göstərir və təsdiq istəyir
4. Smart App Control-u söndürür
5. Restart lazım olduğunu bildirir

Sonra: **kompüteri restart edin** → `AVTOPARK.bat`

#### 🔧 B) Əl ilə (rəsmi yol)

1. **Başlat** → **Windows Security** (Windows Təhlükəsizliyi)
2. **App & browser control** (Tətbiq və brauzer nəzarəti)
3. **Smart App Control** → **Settings**
4. **Off** seçin → təsdiqləyin
5. **Kompüteri restart edin**

### ⚠️ VACİB XƏBƏRDARLIQ

> **Smart App Control BİR DƏFƏ SÖNDÜRÜLDÜKDƏN SONRA YENİDƏN QOŞULA BİLMƏZ.**
>
> Geri qoşmaq üçün **Windows-u yenidən qurmaq** lazımdır.
> Bu, Microsoft-un öz qərarıdır — dəyişdirilə bilməz.

### 🔄 Alternativlər (Smart App Control-u söndürmədən)

| Yol | Nə lazım | Çatışmazlıq |
|---|---|---|
| Başqa kompüterə köçürmə | Smart App Control olmayan PC | Server kompüteri dəyişmək lazım |
| Rəqəmsal imzalama | Sertifikat (~200-400 USD/il) | Xərc |

> 💡 Bu tətbiq **yalnız yerli şəbəkədə** (WiFi) işləyən bir biznes alətidir.
> Söndürmə riski aşağıdır — Windows Defender **işləməyə davam edir**.

### ✅ Nəticəni yoxlama

```powershell
(Get-ItemProperty 'HKLM:\SYSTEM\CurrentControlSet\Control\CI\Policy').VerifiedAndReputablePolicyState
#   -> 0   (söndürülüb)
#   -> 1   (hələ də aktivdir — restart edin)
```

---

## 🚨 ƏGƏR `021cars.az` İSTƏYİRSİNİZ

### `021cars.az` domeni QEYDİYYATDA DEYİL!

Yoxlama nəticəsi:
```
nslookup 021cars.az 8.8.8.8   →  Non-existent domain
RDAP sorğusu                  →  HTTP 404 Not Found
```

**Yəni bu domen dünyada mövcud deyil.** Ona görə `021cars.az` yazanda sayt
açılmır — tunel, server, kod hamısı işləyir, sadəcə domenin özü yoxdur.

### ✅ Nə etməli — 2 yol var

| Yol | Nə lazım | Nə qazandığınız |
|---|---|---|
| **A) Domen almadan** | Heç nə! | `test-tunel.bat` → dərhal işləyən pulsuz HTTPS ünvan (müvəqqəti) |
| **B) `021cars.az` üçün** | Domeni almaq (~20-40 AZN/il) | Daimi öz ünvanınız |

### A) Dərhal internetə çıxarmaq (domen lazım deyil)

```
1) ishe-sal.bat          ← veb serveri başladır
2) test-tunel.bat        ← pulsuz HTTPS ünvan verir
```

`test-tunel.bat` konsolda belə bir ünvan verəcək:
```
|  https://bell-astronomy-fast-climbing.trycloudflare.com  |
```
Bu ünvanı dünyanın hər yerindən açmaq olar — **işləyir!**
(Ünvan hər işə salmada dəyişir və müvəqqətidir.)

### B) `021cars.az` domenini almaq

1. **Domeni qeydiyyatdan keçirin** — `.az` domenləri üçün: **https://www.nic.az**
   (Qiymət: təxminən 20-40 AZN/il. Akkreditə olunmuş registratorlardan biri vasitəsilə.)

2. **Cloudflare-ə əlavə edin** — https://dash.cloudflare.com → **Add site** → `021cars.az`

3. **Nameserver-ləri dəyişin** — domeni aldığınız yerdə (registrator panelində)
   NS qeydlərini Cloudflare-in verdiyi 2 ünvanla əvəz edin.
   (Yayılması 5 dəqiqə - 24 saat çəkə bilər.)

4. **Qurulumu işə salın:** `domen-qurasdir.bat` (admin LAZIM DEYİL)

5. **Yoxlayın:** `yoxla.bat`

6. **Hər gün:** `her-seyi-baslat.bat`

---

## 🔍 0. Vəziyyəti yoxlamaq — `yoxla.bat`

Hər şeyi yoxlayır və nə çatışmadığını deyir:

```
======================================================================
   AVTOMOBIL PARKI v6.0  -  SISTEM YOXLAMASI
======================================================================

  1) VEB SERVER (5000 portu)
    [OK]    Veb server ISLEYIR (PID 2932)
    [OK]    5000 portu dinlenilir

  2) CLOUDFLARED (domen tuneli ucun)
    [OK]    Qurasdirilib: C:\Users\...\cloudflared\cloudflared.exe

  3) TUNEL KONFIQURASIYASI
    [XETA]  config.yml yoxdur - tunel qurulmayib

  4) 021cars.az  DOMENI VE DNS
    [XETA]  021cars.az  ->  DNS-de TAPILMADI
            ***********************************************************
            *  021cars.az DOMENI QEYDIYYATDA DEYIL!                  *
            ***********************************************************

  5) MELUMATLAR
    [OK]    Verilenler bazasi: 44 KB
======================================================================
   YEKUN:  5 OK   |   2 XETA
======================================================================
```

---

## ⚡ 1. Necə işə salınır?

Bu qovluqda (`Autocode.Web`) komanda pəncərəsi açıb yazın:

```powershell
dotnet run
```

Server işə düşəndə konsolda bütün əlçatan ünvanlar göstərilir:

```
======================================================================
   AVTOMOBİL PARKI v6.0  —  VEB SERVER işə düşdü
======================================================================
   Bu kompüterdə :  http://localhost:5000

   Şəbəkədə (WiFi / LAN) — digər cihazlar bu ünvanı açır:
       ->  http://192.168.1.42:5000

   Verilənlər bazası : C:\Users\...\AppData\Local\EnterpriseAeroStudio\avtopark.db
   Giriş nəzarəti   : AKTİV (şifrə tələb olunur)
======================================================================
```

- **Bu kompüterdə:** `http://localhost:5000`
- **Eyni WiFi-dəki telefon/planşet/digər kompüter:** konsolda göstərilən
  `http://192.168.x.x:5000` ünvanı

> 💡 Dayandırmaq üçün konsolda **Ctrl + C** basın.

### Asan yol (skript)

`ishe-sal.bat` faylına ikiqat klik edin — server avtomatik başlayır.

---

## 🔐 2. Giriş şifrəsi

Standart şifrə: **`021cars`**

**MÜTLƏQ DƏYİŞİN!** `appsettings.json` faylını Notepad ilə açın:

```json
{
  "AvtoPark": {
    "Port": 5000,
    "RequireLogin": true,
    "AdminPassword": "BURAYA-ÖZ-ŞİFRƏNİZİ-YAZIN",
    "SessionDays": 30
  }
}
```

| Ayar | İzah |
|---|---|
| `Port` | Serverin portu (standart `5000`) |
| `RequireLogin` | `true` = şifrə tələb olunur, `false` = şifrəsiz |
| `AdminPassword` | Giriş şifrəsi |
| `SessionDays` | Şifrə neçə gün yadda saxlanılsın (standart 30) |
| `DataDirectory` | Boş = masaüstü tətbiqlə eyni qovluq. Fərqli baza istəyirsinizsə yol yazın. |
| `AllowWrite` | `false` = veb-dən yalnız baxmaq (əlavə/redaktə/silmə bağlanır) |

---

## 🌐 3. Şəbəkəyə (WiFi-yə) açmaq

Windows Firewall standart olaraq portu bağlayır. **Bir dəfə** icazə verilməlidir.

`firewall-elave.bat` faylına **sağ klik → "Run as administrator"** edin.

Və ya Administrator PowerShell-də:

```powershell
netsh advfirewall firewall add rule name="AvtoPark Web 5000" dir=in action=allow protocol=TCP localport=5000
```

Yoxlamaq üçün telefonunuzun brauzerində `http://192.168.x.x:5000` ünvanını açın.

> ⚠️ Bütün cihazlar **eyni WiFi / eyni şəbəkədə** olmalıdır.

---

## 🌍 4. `021cars.az` domenini qoşmaq

### A) Sadə yol — Port yönləndirmə (port forwarding)

1. **Statik IP və ya DDNS** — İnternet provayderinizin verdiyi IP dəyişir.
   `no-ip.com` və ya `duckdns.org` kimi pulsuz DDNS xidməti qeydiyyatdan keçin.
2. **Router-ə girin** → *Port Forwarding* bölməsi:
   - Xarici port: `80` (və ya `443`)
   - Daxili IP: serverin işlədiyi kompüterin IP-si (məs. `192.168.1.42`)
   - Daxili port: `5000`
3. **DNS** — `021cars.az` domeninin A qeydini ev/ofis IP-nizə (və ya DDNS ünvanına) yönəldin.

> ⚠️ **Diqqət:** Bu halda sayt internetdən əlçatan olur. Şifrəni güclü seçin!

### B) Peşəkar yol — Cloudflare Tunnel ⭐ (TÖVSİYƏ OLUNUR)

Router-də port açmağa ehtiyac yoxdur, **statik IP lazım deyil**, HTTPS avtomatik gəlir.

**Avtomatik quraşdırma (tövsiyə olunur):**

`domen-qurasdir.bat` faylına **sağ klik → "Run as administrator"**

Skript özü:
1. `cloudflared` alətini endirir və quraşdırır
2. Cloudflare hesabına giriş edir (brauzer açılır)
3. `avtopark` tunelini yaradır
4. `021cars.az` və `www.021cars.az` DNS qeydlərini bağlayır
5. `config.yml` faylını yazır

Sonra **`her-seyi-baslat.bat`** faylına ikiqat klik → **hazırdır!**

**Əl ilə (alternativ):**

```powershell
cloudflared tunnel login
cloudflared tunnel create avtopark
cloudflared tunnel route dns avtopark 021cars.az
cloudflared tunnel run --url http://localhost:5000 avtopark
```

> ⚠️ **Ön şərt:** `021cars.az` domeni Cloudflare hesabınıza əlavə edilmiş olmalıdır.
> `dash.cloudflare.com` → **Add site** → domeni yazın → registratorunuzda (domen aldığınız yerdə)
> **nameserver**-ləri Cloudflare-in verdiyi ünvanlarla dəyişin.

---

## 🧮 5. Kredit Kalkulyatoru &amp; Xərc Kataloqu

Masaüstü tətbiqdəki **iki köməkçi modul** veb versiyada da tam işləyir.

### 5.1 Kredit Kalkulyatoru — `/loan-calculator`

Annuitet (bərabər ödənişli) kredit hesablaması:

| Giriş | İzah |
|---|---|
| Satış / kredit məbləği | Avtomobilin satış qiyməti |
| İlkin ödəniş | Faiz nisbəti avtomatik göstərilir |
| İllik faiz dərəcəsi (%) | 0 olduqda sadə bölünmə |
| Müddət (ay) | 1 – 600 ay |

Çıxış: **kreditləşdirilən məbləğ**, **aylıq ödəniş**, **ümumi ödəniş**,
**ümumi faiz gəliri** — və əlavə olaraq masaüstündə olmayan
**ay-ay tam ödəniş cədvəli** (əsas borc / faiz / qalıq).

> Hesablama düsturu masaüstü tətbiqdəki `LoanCalculatorViewModel` ilə
> tam eynidir, o cümlədən çox yüksək faizdə (`double` sərhədi) limit dəstəyi.

### 5.2 Xərc Kataloqu — `/expense-catalog`

Xərc qrup və kateqoriyalarının idarəsi:

- **Təyinat** üzrə sekmeler: *Avtomobil Xərci* / *Ofis / İnzibati Xərc*
- Qrup **əlavə et** / **sil** (silmə qrupun bütün kateqoriyalarını da silir)
- Seçilmiş qrupun kateqoriyalarını **əlavə et** / **sil**
- Silmə əməliyyatları **təsdiq pəncərəsi** ilə qorunur
- Statistik panel: qrup sayı, kateqoriya sayı, xərc qeydi sayı

> Xərc qeydlərinə **toxunulmur** — yalnız kataloq dəyişir.
> Dəyişikliklər masaüstü tətbiqdə də dərhal görünür (eyni baza).

### 5.3 Xərc əlavə edərkən qrup / kateqoriya (masaüstü ilə EYNİ)

«➕ Yeni Xərc» pəncərəsində **Xərc qrupu** və **Kateqoriya** sahələri
adi açılan siyahıdır və ən sonunda **iki xüsusi seçim** var:

```
Xərc qrupu
┌────────────────────────────────────────┐
│ 💰 Alış & Maya Xərcləri                │
│ 🛠️ Kuzov, Dəmirçi & Malyar             │
│ ⚙️ Mühərrik, Slesar & Ehtiyat Hissələri│
│ ...                                     │
│ ➕ Yeni qrup əlavə et…                  │  ← seçin
│ 🗑 Seçilmiş qrupu sil…                  │  ← seçin
└────────────────────────────────────────┘
```

| Seçim | Nə olur |
|---|---|
| Normal qrup | Həmin qrupun kateqoriyaları yüklənir |
| **➕ Yeni qrup əlavə et…** | Aşağıda ad sətri açılır → yazıb **✔ Əlavə et** |
| **🗑 Seçilmiş qrupu sil…** | Aşağıda təsdiq sətri çıxır → **Bəli, sil** |
| **➕ Yeni kateqoriya əlavə et…** | Seçilmiş qrupa yeni kateqoriya |
| **🗑 Seçilmiş kateqoriyanı sil…** | Seçilmiş kateqoriya silinir |

**Zəncirvari filtr (masaüstü ilə eyni):**
- **Təyinat** dəyişir → qruplar yenilənir, ilk qrup seçilir
- **Qrup** dəyişir → yalnız o qrupun kateqoriyaları görünür
- **Ofis** təyinatı seçilir → avtomobil seçimi ləğv olunur

> ✅ Sonradan əlavə etdiyiniz qrup/kateqoriyalar **dərhal** həm saytda,
> həm masaüstü tətbiqdə görünür (eyni baza + canlı sinxronizasiya).

### 5.4 Alışlar — Nağd / Barter (`/purchases`)

Avtomobil alışlarının tam təhlili üçün **yeni professional bölmə**.

#### Alış qeydiyyatı (`/cars` → ➕ Yeni Avtomobil)

| Sahə | İzah |
|---|---|
| **Alış tarixi** | Tarix seçicisi (standart: bu gün) |
| **Alış saatı** | `12:15` formatı |
| **Alış üsulu** | **💵 Nağd** və ya **🔄 Barter (əvəzləmə)** |
| **Barter təsviri** | Yalnız Barter seçildikdə görünür — *«2015 Nexia 90AB123 + 5 000 ₼»* |

> Barter sahəsi **avtomatik görünür/gizlənir**. Nağd seçilsə, köhnə barter mətni təmizlənir.

#### Alışlar səhifəsi

| Blok | Nə göstərir |
|---|---|
| **6 KPI kartı** | Ümumi alış · Nağd · Barter · Bu ay · Orta qiymət · Barter payı |
| **📈 12 aylıq qrafik** | Nağd / Barter sütunları + alış sayı (3 rəng) |
| **🔍 Axtarış** | Marka, nömrə, VIN, **barter təsviri**, saat üzrə canlı filtr |
| **Filtrlər** | Üsul (hamısı/nağd/barter) · İl · Status |
| **📋 Cədvəl** | Tarix, avtomobil, nömrə, VIN, üsul, barter təsviri, qiymət, xərc, maya, status |
| **⚖️ Bölgü cədvəli** | Nağd vs Barter — say, pay (%), məbləğ, orta qiymət + vizual zolaq |
| **⬇ CSV ixracı** | `alislar_20260916.csv` — üsul və barter təsviri daxil |

#### Masaüstü tətbiqdə

`🚗 Avto Park` tabında:
- **Alış Üsulu** açılan siyahısı (Nağd / Barter)
- **Barter Təsviri** sahəsi (yalnız Barter seçildikdə görünür)
- Cədvəldə **«Alış Üsulu»** sütunu (`💵 Nağd` / `🔄 Barter`)

> 🔄 Hər iki tərəf **canlı sinxron** — saytda barter seçsəniz, masaüstüdə dərhal görünür.

#### Avtomatik "Alış" xərci

Barter ilə alışda avtomatik yaranan xərc qeydinin Qeyd sahəsi belə olur:
```
Barter alış — əvəz: 2015 Nexia 90AB123 + 5 000 ₼
```

---

## 🌍 6. `021cars.az` domenini qoşmaq — KİMLƏR GİRƏ BİLƏR?

- **Şifrə ilə** → dünyanın hər yerindən `https://021cars.az` açılır
- **Şifrəsiz** → yalnız eyni WiFi-dəkilər LAN ünvanı ilə (`http://192.168.x.x:5000`)
- **Şifrəni söndürmək** → `appsettings.json` → `"RequireLogin": false`
  (bu halda domeni qoşmaq **təhlükəlidir** — hamı girə bilər)

---

## 💾 7. Verilənlər bazası və fayllar

Veb server **masaüstü tətbiqlə eyni** bazanı oxuyur:

```
C:\Users\<İstifadəçi>\AppData\Local\EnterpriseAeroStudio\
├── avtopark.db          ← SQLite verilənlər bazası
├── Media\               ← Yüklənmiş sənədlər (PDF, şəkillər…)
│   ├── Avtomobil\<Id>\
│   └── KreditEmeliyyati\<Id>\
└── Logs\
    ├── avtopark-*.log     ← Masaüstü tətbiq loqu
    └── avtopark-web-*.log ← Veb server loqu
```

### ✅ Masaüstü + veb CANLI SİNXRON işləyir

Hər iki tətbiq **eyni `avtopark.db` faylını** işlədir və hər ikisi bu faylı
**nəzarətdə saxlayır**. Bir tərəfdə edilən dəyişiklik digər tərəfdə
**DƏRHAL (1 saniyə içində)** görünür — **heç bir düymə basmaq lazım deyil.**

| Nə edirsiniz | Nəticə |
|---|---|
| Veb-də avtomobil əlavə edirsiniz | Masaüstü tətbiqdə **dərhal** görünür |
| Masaüstü tətbiqdə xərc əlavə edirsiniz | Veb səhifədə **dərhal** görünür |
| Veb-də kredit yaradırsınız | Masaüstü tətbiqdə **dərhal** görünür |
| Telefonda avtomobil silirsiniz | Kompüterdəki veb **dərhal** yenilənir |

**Necə işləyir** (`Services/DataChangeWatcher.cs` — hər iki layihə üçün ortaq):

1. **FileSystemWatcher** — baza faylı dəyişən kimi ANİ reaksiya
2. **Nəzarət taymeri** — hər 2 saniyədə vaxtı/ölçünü yoxlayır (ehtiyat qat)
3. Hər iki mənbə **400 ms** gecikdirmə ilə tək hadisəyə birləşdirilir
   (SQLite bir əməliyyatda 3 fayl yazır: `.db`, `-wal`, `-shm`)

**Yoxlama:**
```
http://021cars.az/diaqnostika   →  "Canlı Sinxronizasiya: AKTİV"
http://021cars.az/health        →  {"sinxronizasiya":{"isleyir":true,"deyisiklikSayi":N}}
```

> 💡 Masaüstü tətbiqdə dəyişiklik gələndə status sətrində
> *«Veb-dəki dəyişiklik yeniləndi — 17:14:45»* yazısı görünür.

⚠️ **Nəzərə alın:** Eyni vaxtda yazma SQLite-ın **WAL rejimi** ilə təhlükəsiz
idarə olunur. Ancaq eyni avtomobili iki yerdə EYNİ ANDA redaktə etməyin —
sonuncu yazan qalib gəlir.

---

## 📄 8. Səhifələr

| Səhifə | Ünvan | Nə edir |
|---|---|---|
| 📊 İdarə Paneli | `/` | KPI kartları + 12 aylıq gəlir/xərc/mənfəət qrafiki |
| 🚗 Avtomobillər | `/cars` | Siyahı, əlavə/redaktə/silmə, axtarış, CSV ixracı |
| 🛒 Alışlar | `/purchases` | **Nağd/Barter təhlili, qrafik, axtarış, CSV** |
| 💸 Xərclər | `/expenses` | Xərc qeydləri, kataloqdan seçim, filtrlər, CSV |
| 💰 Satışlar | `/sales` | Satışın qeydə alınması, mənfəət hesablaması |
| 🏦 Kreditlər | `/credits` | Kredit müqavilələri, annuitet hesablaması |
| 🧾 Kredit Əməliyyatları | `/credit-transactions` | Gəlir / Xərc / Möhlət / Gecikmə qeydləri |
| 📅 Ödəniş Qrafiki | `/payment-schedule` | Tam ödəniş cədvəli, möhlət və gecikmə ilə |
| 🗄️ Arxiv | `/archive` | Satılmış avtomobillər + bağlı kreditlər |
| 📎 Sənədlər | `/media` | Fayl yükləmə/açma/silmə (masaüstü ilə ortaq qovluq) |
| 🗂️ Xərc Kataloqu | `/expense-catalog` | Qrup / kateqoriya idarəsi (əlavə + silmə) |
| 🧮 Kredit Kalkulyatoru | `/loan-calculator` | Annuitet hesablaması + tam ödəniş cədvəli |

---

## 🏗️ 9. Texniki memarlıq

```
Autocode.Web/
├── Autocode.Web.csproj      ← Layihə (net10.0, Blazor Server)
├── Program.cs               ← Server qurulumu (DI, auth, Kestrel, endpointlər)
├── AvtoParkOptions.cs       ← Konfiqurasiya modeli
├── UiState.cs               ← Bildiriş (toast) servisi
├── appsettings.json         ← Ayarlar (domen, şifrə, DNS)
├── _Imports.razor
├── Components/
│   ├── App.razor            ← Kök HTML
│   ├── Routes.razor         ← Marşrutlaşdırıcı
│   ├── Layout/              ← NavMenu, MainLayout
│   ├── Shared/              ← AppFormat, StatCard (ümumi komponentlər)
│   └── Pages/               ← 11 səhifə
├── Services/
│   └── MiniDnsServer.cs     ← Yerli DNS (021cars.az → LAN IP)
├── wwwroot/
│   ├── app.css              ← Dizayn sistemi
│   ├── login.html           ← Giriş səhifəsi
│   ├── qrcode.min.js        ← QR kod (mobil giriş üçün)
│   └── favicon.svg
├── AVTOPARK.bat             ← ⭐ TƏK ANA SKRİPT — hər gün bunu işlədin (admin)
├── ishe-sal.bat             ← Yalnız veb serveri
├── router-dns.bat           ← Router DHCP DNS qurulumu (bir dəfə)
├── yoxla.bat                ← Vəziyyət yoxlaması
└── README.md
```

### Kod təkrarı YOXDUR

`Autocode.Web.csproj` aşağıdakı qovluqları **eyni fiziki fayllardan**
kompilyasiya edir:

```xml
<Compile Include="..\Autocode\Models\**\*.cs"   LinkBase="Shared\Models" />
<Compile Include="..\Autocode\Data\**\*.cs"     LinkBase="Shared\Data" />
<Compile Include="..\Autocode\Services\**\*.cs" LinkBase="Shared\Services" />
```

Yəni `Models/CarItem.cs`-də nəsə dəyişsəniz, **hər iki** tətbiqə təsir edir.

Yalnız `DialogService.cs` / `IDialogService.cs` (WPF MessageBox & fayl dialoqları)
xaric edilir — veb onları istifadə etmir.

**Autentifikasiya:** cookie əsaslı (`AvtoPark.Auth`), HttpOnly + SameSite=Lax,
30 gün etibarlı. Bütün sorğular middleware ilə qorunur.

---

## 🔧 10. Problemlər və həlli

| Problem | Səbəb / Həll |
|---|---|
| `'"cloudflared"' is not recognized` | `domen-qurasdir.bat` işlədilməyib (o, cloudflared-i quraşdırır) |
| `address already in use` | Server artıq işləyir. `her-seyi-baslat.bat` köhnə prosesi özü dayandırır |
| `021cars.az` açılmır | **Domen qeydiyyatda deyil!** README-nin əvvəlini oxuyun |
| Digər cihazdan açılmır | Firewall qaydası → `firewall-elave.bat` (admin) |
| `dotnet` tanınmır | .NET SDK 10: https://dotnet.microsoft.com/download |
| "Port istifadədədir" | `appsettings.json`-da `Port` dəyərini dəyişin (məs. `8080`) |
| Şifrə yaddan çıxdı | `appsettings.json` → `AdminPassword` dəyişin, serveri yenidən başladın |
| Səhifə köhnə görünür | `Ctrl + F5` ilə brauzer keşini təmizləyin |
| Hər şey qarışıqdır | **`yoxla.bat` işlədin** — nə çatışmadığını göstərəcək |

Loqlar: `%LOCALAPPDATA%\EnterpriseAeroStudio\Logs\avtopark-web-*.log`

---

## 📦 11. Tətbiq kimi yerləşdirmə (isteğe bağlı)

Serveri başqa kompüterdə `dotnet` olmadan işlətmək üçün:

```powershell
dotnet publish Autocode.Web.csproj -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -o C:\AvtoParkWeb
```

Sonra `C:\AvtoParkWeb\EnterpriseAeroStudio.Web.exe` faylını işə salın.

> ⚠️ `appsettings.json` faylını exe ilə **eyni qovluğa** kopyalamağı unutmayın.

### Windows xidməti kimi (kompüter açılanda avtomatik başlasın)

1. [NSSM](https://nssm.cc/) alətini endirin.
2. Administrator PowerShell:

```powershell
nssm install AvtoParkWeb "C:\AvtoParkWeb\EnterpriseAeroStudio.Web.exe"
nssm set AvtoParkWeb AppDirectory "C:\AvtoParkWeb"
nssm set AvtoParkWeb Start SERVICE_AUTO_START
nssm start AvtoParkWeb
```

</details>

---

## 📝 GITHUB-A NƏ YAZMALIYAM? (buraxılış mətni)

`GitHub-Paylash.ps1 -Burax -Qeyd "..."` əmri ilə və ya GitHub-da **Releases → Draft a new release** ilə ✓.

### 🏷️ Teq (tag) — MÜTLƏQ bu formatda ✗✓✓

```
v6.1        ← ✅ DÜZGÜN ✓ (proqram onu oxuyur ✓)
v6.1.0      ← ✅ olar ✓
6.1         ← ✅ olar ✓
yeni-versiya ← ❌ OLMAZ ✗ (rəqəm lazımdır ✗)
```

> ⚠️ **VACİB** ✗: teqdəki rəqəm `EnterpriseAeroStudio.csproj`-daki `<Version>`-dan **BÖYÜK** olmalıdır ✓
> (məs. proqramda `6.0.0` → teq `v6.1` ✓ → popup çıxır ✓✓✓)

### 📛 Buraxılış adı (name)

```
021Cars 6.1
```

### 📄 Mətn (description) — nümunə şablon ✓

```markdown
## 🚀 021Cars 6.1

### ✨ YENİLİKLƏR
- 🚀 Proqram içində «Güncəlləmə» düyməsi əlavə olundu ✓
- ⏱️ Avtomatik yoxlama hər 30 dəqiqədə bir ✓
- 🐞 Hesabatda tarix səhvi düzəldildi ✗

### 🔧 DÜZƏLİŞLƏR
- Satış cədvəlində yuvarlaqlaşdırma dəqiqləşdirildi ✓
- Kredit hesablamasında nadir hal düzəldildi ✓

### 🛡️ MƏLUMATLAR
- Baza · media · yedəklər **toxunulmur** ✓✓✓
- Proqram yeniləndikdən sonra **avtomatik açılır** ✓
```

### 📦 Assets (fayllar) — BURAYA QOYUN ✓✓✓

```
┌─────────────────────────────────────────────────────────────┐
│  Assets                                                      │
│  ┌───────────────────────────────────────────────────────┐  │
│  │  021Cars_Installer.exe      146.6 MB   ← ★ BU ★      │  │
│  └───────────────────────────────────────────────────────┘  │
│  (adı DƏYİŞMƏZ olmalıdır ✗ — proqram «021Cars_Installer.exe»│
│   adlı faylı axtarır ✓; tapmasa ilk .exe-ni götürür ✓)      │
└─────────────────────────────────────────────────────────────┘
```

> ❌ `payload.zip`, `publish\` qovluğu, `.pdb` faylları — **LAZIM DEYİL** ✗
> ✅ YALNIZ `021Cars_Installer.exe` ✓ (hər şey onun içindədir ✓)

---

## 🔄 GÜNCƏLLƏMƏ ZAMANI PROQRAMA NƏ OLUR? (addım-addım ✓)

İstifadəçi «🚀 GÜNCƏLLƏ» basanda **avtomatik** baş verir ✓:

| # | Nə olur |
|---|---|
| ① | ⬇️ Yeni installer `%TEMP%\021Cars_Guncelleme\` qovluğuna yüklənir ✓ (faiz göstərilir ✓) |
| ② | 🔐 Windows admin icazəsi soruşur → «Bəli» ✓ |
| ③ | ⏳ **Proqram bağlanır** ✗ (və veb server də ✗) — fayllar kiliddən azad olur ✓ |
| ④ | 📦 Fayllar `.exe`-in içindəki paketdən **üstünə yazılır** ✓ (⚡ 20-60 saniyə ✓) |
| ⑤ | 🛡️ **İSTİFADƏÇİ MƏLUMATLARI TOXUNULMUR** ✗✓✓✓ — baza · media · yedəklər · istifadəçilər · ayarlar |
| ⑥ | 🔗 Masaüstü + Baş menyu qısayolları yenilənir ✓ |
| ⑦ | 📋 «Proqramlar və Xüsusiyyətlər»-də versiya yenilənir ✓ |
| ⑧ | 🚀 **Proqram avtomatik yenidən açılır** ✓ — istifadəçi sadəcə giriş edir ✓✓✓ |

### ⚠️ İstifadəçiyə deyiləcək 3 şey ✗

```
① 🚀 «GÜNCƏLLƏ» basın → 1-2 dəqiqə gözləyin ✓
② 🔐 Windows «Bəli» (admin) basın ✓
③ 🚀 Proqram özü açılacaq — YENİDƏN GİRİŞ edin ✓
   (📌 məlumatlarınız YERİNDƏDİR ✓ — heç nə itmir ✗✓✓)
```

### ✅ Yoxlama (yeniləmədən sonra ✓)

```
① Aşağıda 🟢 yaşıl dairə + «🚀 Güncəlləmə» yazısı → ən son versiya ✓
② «🚀 Güncəlləmə» bas → «✅ ƏN SON VERSİYADASINIZ» görəcəksiniz ✓
```

---

## 🛠️ PROBLEMLƏR VƏ HƏLLİ

| Problem | Həll |
|---|---|
| 🚀 Düymə 🟠 narıncı, popup çıxır | ✅ Normaldır ✓ — yeni versiya var ✓ «GÜNCƏLLƏ» basın ✓ |
| 🚀 Düymə 🔴 qırmızı | 📶 İnternet yoxdur ✗ — 30 dəqiqə sonra özü yenidən yoxlayır ✓ |
| Popup çıxdı, «SONRA» basdım | ✅ Proqramı bağlayıb açanda yenə gələcək ✓ |
| Güncəlləmə yoxlanmır | 🌐 GitHub-da **buraxılış** (Release) yaradılıbmı? ✗ Teq `v6.1` formatındadır? ✗ |
| Popup heç çıxmır | 🔢 Teqdəki versiya `<Version>`-dan böyükdür? ✓ Artırın ✓ |
| `git` tapılmır ✗ | `winget install Git.Git` ✓ |
| GitHub-a 146 MB yüklənmir ✗ | ❌ Repo-ya YOX ✗ — **Releases → Assets**-ə qoyun ✓ (skript edir ✓) |

---

## 🖱️ İDARƏETMƏ — SİÇAN + KLAVİATURA ✓✓✓

| Hərəkət | Nəticə |
|---|---|
| 🖱️ **Çarx** | şaquli sürüşmə (adi ✓) |
| 🖱️ **Ctrl + Çarx ⬆️/⬇️** | ⬅️➡️ **SAĞA / SOLA** — cədvəllər · siyahılar · tablar ✓✓✓ |
| ⌨️ **Ctrl + Z** | ən sonuncu əməliyyatı geri qaytarır ✓ |
| ⌨️ **Ctrl + S** | yadda saxla ✓ |
| ⌨️ **F11** | tam ekran ✓ |

> 🎯 **Ctrl + Çarx** BÜTÜN pəncərələrdə işləyir ✓ (avtomobil parkı · xərclər · satış ·
> kreditlər · maliyyə · tərəfdaşlar · silinənlər · arxiv ✓✓✓)

---

## 💾 SİNXRONİZASİYA — 3 KANAL (biri işləməsə də digərləri işləyir ✓✓✓)

```
        🖥️ KOMPÜTER (yerli baza)   ←→   💾 DATA USB   ←→   🔥 FIREBASE (bulud)
        avtopark.db                      D:\021Cars\         realtime
```

| Vəziyyət | Nə olur |
|---|---|
| 🌐 İnternet yoxdur ✗ | ✅ Kompüter + USB işləyir ✓ — dəyişikliklər **növbəyə** yazılır ✓ |
| 🔥 Firebase qayıdır ✓ | ⬆️ **Növbə BOŞALDILIR** ✓ (oflayn zamanı yazılanlar avtomatik göndərilir ✓✓✓) |
| 💾 USB taxılmır ✗ | ✅ Kompüter + Firebase işləyir ✓ |
| 💾 USB taxılır ✓ | ⬇️ Baza USB-yə yenidən yazılır ✓ + medyalar köçürülür ✓ |
| 🖥️ 2-ci kompüter işlədir ✓ | ⬇️ **Firebase-dən YOXLANILIR** ✓ — çatışmayan qeydlər **yerli bazaya ƏLAVƏ** olunur ✓✓✓ |
| 🆕 YENİ kompüter + USB ✓ | 📥 **USB-dəki HAMISI yerli bazaya köçürülür** ✓✓✓ (aşağıda ✓) |

> ✅ **Nəticə:** bir kanal işləməsə də digərləri işləyir ✓ və kanal geri qayıdanda
> **çatışmayan hər şey AVTOMATİK** tamamlanır ✓✓✓ (LWW = son dəyişiklik qazanır ✓)

---

## 🆕 YENİ KOMPÜTER + USB = AVTOMATİK BƏRPA ✓✓✓

```
① 🆕 Proqram yeni quraşdırılır (baza boşdur ✓)
② 💾 Qeydiyyatlı DATA USB taxılır ✓
③ 🚀 Proqram açılır → USB yoxlanılır ✓
        ├── 🛡️ Yerli baza BOŞDURSA ✓
        │    → 📥 USB-dəki 021cars_data.db YERLİ BAZAYA köçürülür ✓✓✓
        │      (bütün avtomobil · xərc · satış · kredit · əməliyyat · tərəfdaş ✓)
        ├── 🔀 USB bazası DAHA TƏZƏDİRSƏ ✓
        │    → çatışmayan qeydlər ƏLAVƏ olunur ✓ (mövcudlar TOXUNULMUR ✗✓✓)
        └── ⏱️ USB bazası KÖHNƏDİRSƏ ✗
             → heç nə dəyişmir ✗ (silinmişlər geri qayıtmır ✓✓✓)
④ ☁️ Sonra Firebase ilə də sinxronlaşır ✓ + medyalar USB-dədir ✓
```

> ⚠️ Köhnə baza zədəli/boş olarsa → `avtopark.db.bak` kimi **saxlanılır** ✓ (itmir ✗)
>
> 💾 **VAHİD MƏNBƏ** ✗✓✓ — baza yolu **YALNIZ** `Cas0201.Kok.BazaYolu`-dan götürülür ✓:
> `{Quraşdırma yeri}\EnterpriseAeroStudio\avtopark.db` ✓ — həm tətbiq ✓, həm USB
> bərpa/birləşdirmə ✓ **EYNİ** faylı işlədir ✓ (əks halda USB-dən gələn məlumat
> görünməz qalırdı ✗ → «avtomobillər gəlmədi» ✗✓✓).
> ♻️ Köhnə buraxılışdan qalan `{Quraşdırma yeri}\avtopark.db` məlumatlıdırsa →
> **avtomatik** əsas bazaya köçürülür ✓ (məlumat İTMİR ✗✓✓)

---

## 🔀 BİRLƏŞDİRMƏ QAYDASI — «HEÇ NƏ SİLİNMİR» ✗✓✓

İki kompüterin **Id-ləri eyni başlayır** (1, 2, 3 … ✓) — buna görə sadə birləşdirmə
məlumat itirirdi ✗. İndi işləyən **professional** qayda belədir ✓:

| Hal | Nə olur |
|---|---|
| 📥 Baza yerli bazada **yoxdur** (eyni Id sərbəstdir ✓) | Eyni Id ilə əlavə olunur ✓ |
| ✅ Id var, sətir **TAM EYNİDİR** ✓ | Təkrar əlavə edilmir ✗ (idempotent ✓ — eyni USB-ni 100 dəfə taxsan da dublikat yox ✗✓✓) |
| 🔀 Id var, sətir **FƏRLİDİR** ✗ | Sətir **YENİ Id** alır ✓ və asılı istinadlar (`CarId` · `CreditId` · `SaleId` · `Senedler.RefId` ✓) **avtomatik** yeni Id-yə düzəldilir ✓✓✓ |
| 🗑️ Qeyd burada **SİLİNMİŞDİR** ✓ (`Trash\*.json` ✓) | USB-dən **GERİ GƏTİRİLMİR** ✗✓✓ |
| 🛡️ Hər birləşdirmədən **ƏVVƏL** | Ehtiyat surət: `{data}\Yedekler\Baza\avtopark_YYYYMMDD_HHMMSS.db` ✓ (son 20 ✓) |
| ♻️ İtmiş məlumat varsa ✓ | `.bak` · köhnə yol · son 3 surət → ƏSAS bazaya **BİRLƏŞDİRİLİR** ✓✓✓ |

> ⚠️ **Yerli bazaya HEÇ VAXT üzərinə yazılmır** ✗✓✓ — kopyalama **yalnız** baza
> HEÇ YOXDURSA edilir ✓ (o halda itiriləcək məlumat da yoxdur ✓).
> Nəticə: «inteqrasiya et» basanda **ikinci kompüterin məlumatı da QALIR** ✓,
> birinci kompüterin məlumatı da GƏLİR ✓✓✓

---

## 🔢 SIRA NÖMRƏSİ (məs. 312) — ART IQ İTMİR ✗✓✓

| Qayda | Nə edir |
|---|---|
| 🏗️ **Sxem ƏVVƏL** ✓ | Tətbiq açılışında əvvəlcə miqrasiya/sxem hazırlanır ✓, **sonra** USB birləşdirməsi ✓ → yeni sütunlar (`SiraNomresi` ✓) mövcud olur ✓ (əvvəl bu sıra səhv idi ✗ → «312 → 0» ✗✓✓) |
| 🔑 **«Eyni qeyd» açarı** ✓ | 🚗 eyni **dövlət nömrəsi** · 💳🧾 eyni **müqavilə nömrəsi** · 👥 eyni **tərəfdaş adı** → **eyni** qeyd sayılır ✓ → dublikat yaranmır ✗ |
| 🩹 **Boş sahələr doldurulur** ✓ | Yerli qeydin **boş** sahələri (0 · null · «» ✓) mənbədən doldurulur ✓ — **dolu** sahələrə TOXUNULMUR ✗✓✓ |
| 🔢 **Sıra nömrəsi istisnası** ✓ | Eyni maşının **sıra nömrəsi** mənbədən **gəlir** ✓ (əvvəl avtomatik 0/4 verilibsə də → 312 gəlir ✓✓✓) |
| 🛡️ **«0» qoruyucusu** ✓ | Buluddan/USB-dən gələn `siraNomresi = 0` **heç vaxt** mövcud nömrəni silmir ✗✓✓ |
| 🔢 **Düzəliş yalnız «0»-a** ✓ | Sıra nömrəsi düzəlişi YALNIZ nömrəsi olmayanlara nömrə verir ✗ → istifadəçinin **312**-si toxunulmaz ✓✓✓ |
| ⏱️ **İki mərhələli yoxlama** ✓ | Birləşdirmədən **əvvəl** ✓ və **sonra** ✓ sıra nömrələri yoxlanılır ✓ |

---

## ⚡ DONMA PROBLEMİ — HƏLL OLUNDU ✗✓✓✓

**Problem ✗:** məlumat artdıqca (194 avtomobil · 1800+ xərc ✓) proqram **donurdu** ✗ —
çünki hər dəyişiklikdə **9 tab-ın HAMISI** yenidən qurulurdu ✗ (hər 5 saniyədə ✓).

**Həll ✓✓✓:**

| Düzəliş | Nəticə |
|---|---|
| 🎯 **Yalnız GÖRÜNƏN tab yenilənir** ✓ | 9 tab yerinə **1 tab** ✓ → donma YOX ✗✓✓ |
| ⏳ Qalan tablar **«çirkli»** işarələnir ✓ | istifadəçi həmin taba keçəndə yenilənir ✓ (məlumat itmir ✗) |
| 🔢 **Versiya qapısı** ✓ | heç nə dəyişməyibsə UI-a **toxunulmur** ✗✓✓ |
| ⌨️ **Redaktə qapısı** ✓ | yazarkən yeniləmə **təxirə salınır** ✗ (mətn silinmir ✓) |
| 📥 **USB yazma arxa fonda** ✓ | 86 MB media köçürməsi UI-ı **dondurmur** ✗✓✓ |
| 🔄 «🔄 İndi yenilə» düyməsi ✓ | istəsəniz **bütün** tabları bir dəfə yeniləyir ✓ |
| 🧩 `AsNoTracking()` ✓ | oxuma sorğuları EF izləməsi olmadan ✓ (2× sürətli ✓) |

> 📑 **Necə işləyir:** tab dəyişəndə (`MainTabs.SelectionChanged` ✓) yalnız o tab
> yenilənir ✓ — əvvəlki davranış (hər şeyi birdən yeniləmək ✗) tamamilə dayandırılıb ✗✓✓


| Fayl | Nə var |
|---|---|
| `Logs\usb_izleme.log` ✓ | 🔑 USB tanıma · 📥 bərpa/kopyalama · 🔀 birləşdirmə hesabatı (`Avtomobiller +3 · Satislar +1` ✓) |
| `Logs\app_errors.log` ✓ | ❌ XƏTA-lar · ♻️ bərpa xəbərləri ✓ |
| `Logs\CRASH.txt` · `Logs\CRASH_UI.txt` ✓ | 💥 qəfil çöküşlərin tam izi ✓ |
| `Yedekler\Baza\*.db` ✓ | 🛡️ hər birləşdirmədən əvvəlki tam baza surəti ✓ |

---

## 📁 MEDYA QOVLUQLARI — MAŞININ ADI İLƏ ✓✓✓

Sənəd/şəkil/PDF saxlananda qovluq **avtomobilin** adı ilə yaradılır ✓:

```
D:\021Cars\Media\
├── Avtomobil\
│   ├── 001 - Mercedes - 22GG222\      ← ★ Sıra № · Marka · Qeydiyyat nişanı ★ ✓
│   │   ├── texpasport.pdf ✓
│   │   └── sekil_1.jpg ✓
│   ├── 002 - Hyundai Sonata - 99QE103\
│   └── 003 - Toyota Camry - 10AA100\
│
└── KreditEmeliyyati\
    ├── 001 - Mercedes - 22GG222\      ← 💳 kredit ödəniş qəbzləri DƏ maşının adı ilə ✓✓✓
    └── 002 - Hyundai Sonata - 99QE103\
```

> 💳 **Kredit əməliyyatı sənədi** də əvvəlcə kreditə, oradan **avtomobilə** bağlanır ✓
> → yəni HƏR sənəd **maşının adı olan qovluğa** düşür ✓✓✓
> ⚠️ Maşın tapılmazsa → `refId` (məs. `61`) ✓ — proqram çökmür ✗

---

## 🚀 v6.2 — «MİLYON SƏTİRDƏ DƏ DONMA YOXDUR» ✗✓✓✓

> 📌 **Şikayət:** «Ümumi Xərclərdə çox məlumat olduğuna görə birinci girəndə proqram
> donur, bir neçə vaxtdan sonra açılır, amma üstündə işləyəndə yenə donur.
> Ümumiyyətlə, harda məlumat çoxdursa proqram donmağa başlayır.»

### 🎯 ƏSAS SƏBƏB — VİRTUALİZASİYA SÖNÜRÜLMÜŞDÜ ✗

```
⚠ ƏVVƏL (DONMANIN KÖKÜ) ✗
──────────────────────────────────────────────────────────────────────
① `App.xaml`-də QLOBAL ScrollViewer stili CanContentScroll=False qoyurdu ✗
   → bu qayda CƏDVƏLLƏRİN (DataGrid) DAXİLİ ScrollViewer-inə də düşürdü ✗
   → WPF SƏTİR VİRTUALİZASİYASI TAMAMİLƏ SÖNÜRDÜ ✗✗
   → 10 000 sətir = 10 000 UI elementi birdən çəkilirdi ✗ → DONMA ✗

② Bütün tablar XARİCİ bir ScrollViewer (CanContentScroll=False) içində idi ✗
   → cədvəllər SONSUZ hündürlük alırdı ✗ → eyni nəticə: virtualizasiya YOX ✗

✅ İNDİ: virtualizasiya AÇIQDIR ✓ (`VirtualizingPanel.VirtualizationMode=Recycling` ✓)
   → ekranda yalnız görünən ~30 sətir çəkilir ✓
   → 1 000 000 sətirdə də cədvəl ANİ açılır ✓✓✓
```

### 📊 ÖLÇÜLMÜŞ NƏTİCƏLƏR (200 000 xərc sətri ilə)

| Əməliyyat | ⚠ ƏVVƏL | ✅ İNDİ | Qazanc |
|---|---|---|---|
| 🔓 **İlk yükləmə** | **3 411 ms** · +78 MB RAM ✗ | **72 ms** · yalnız 200 sətir ✓ | **≈47× sürətli** ✓ |
| 🚘 **Avto Park** siyahısı | 307 ms (+ bütün xərclər RAM-da ✗) | **40 ms** (SQL cəmləri ✓) | **≈8×** ✓ |
| 🔎 **Axtarış** (hər hərfdə ✗) | tam siyahı RAM-da süzülürdü ✗ | **206 ms** SQL-də ✓ | donma YOX ✓ |
| 💰 **Yekunlar** (4 göstərici) | RAM-da `Sum()` ✗ | **378 ms** SQL `SUM` ✓ | donma YOX ✓ |
| 💾 **Yeni xərc** yazma | — | 122 ms ✓ | ✓ |
| ✏️ **Redaktə** | — | 8 ms ✓ | ✓ |
| 🔤 **Axtarış mətni hazırlığı** | — | 20 000 sətir / **0,3 s** ✓ (bir dəfəlik ✓) | ✓ |

> ✅ **Yaddaş:** 200 000 sətirdə 78 MB → **~0 MB** (yalnız 200 sətir yaddaşda ✓)
> → 1 000 000 sətirdə əvvəl ≈400 MB + 15 saniyə donma ✗ → indi **fərq ETMİR** ✓✓✓

### 🔧 V6.2-DƏ EDİLƏN DƏYİŞİKLİKLƏR

#### ① 🚀 Səhifələmə (PAGING) — bütün cədvəl yerinə 200 sətir ✓

```
⚠ ƏVVƏL: SELECT * FROM Xercler  → BÜTÜN cədvəl yaddaşa ✗
✅ İNDİ : LIMIT 200 OFFSET 0    → yalnız görünən səhifə ✓

• «⬇️ Daha çox yüklə» düyməsi ✓ + cədvəl aşağı sürüşəndə AVTOMATİK ✓
• «📄 200 / 1 234 567 sətir göstərilir» sayğacı ✓
• 🖱️ Sütun başlığına basanda sıralama SQL-də ✓ (bütün baza üzrə ✓ — yüklənmiş
  200 sətir üzrə deyil ✗✓✓)
```

#### ② 🔎 Axtarış SQL-DƏ (Regex/Contains deyil ✗)

```
• 350 ms «debounce» ✓ → hər hərfdə DB sorğusu getmir ✗
• `Axtaris` kölgə sütunu ✓: Şüşə → «suse» · Əyləc → «eylec» · Çəkmə → «cekme»
• SQLite `LIKE` yalnız ASCII hərflərdə hərf fərqini görür ✗ →
  bu sütun sayəsində «şüşə» yazanda «Şüşə» DƏ TAPILIR ✓✓✓
• ⚠ Backfill işləməmiş olsa belə axtarış DÜZGÜN işləyir ✓ (ehtiyat şərt ✓)
```

#### ③ 💰 Yekunlar SQL `SUM` ilə — `double` vasitəsilə ✓

```
⚠ SQLite bazasında `decimal` cəmi DƏSTƏKLƏNMİR ✗
  («SQLite cannot apply aggregate operator 'Sum' on expressions of type 'decimal'»)
✅ Cəm `double` ilə alınır ✓ (15-16 rəqəm dəqiqlik ✓) → `decimal`-a çevrilir ✓
```

#### ④ 🚘 «Avto Park» — SQL `GROUP BY` (1 000 000 xərc RAM-a YÜKLƏNMİR ✗)

```
⚠ ƏVVƏL: hər avtomobil üçün BÜTÜN xərcləri yaddaşa ✗ (Include ✓)
✅ İNDİ : xərc cəmləri SQL-də qruplaşdırılır ✓ → yalnız avtomobil siyahısı ✓
         (`XerclerCemi` / `ButunXerclerCemi` ✓)
```

#### ⑤ ⚡ AĞIR İŞ ARXA FONDA (UI heç vaxt bloklanmır ✗✓✓)

```
• `Task.Run` ilə oxuma ✓: Xərclər · Kredit Əməliyyatları · Maliyyə Paneli · Avto Park
• Bütün səhifə/siyahı yazmaları `await ... Dispatcher` axınında ✓
```

#### ⑥ ⏱️ AÇILIŞ SÜRƏTLƏNDİ — «LAZY» TABLAR ✓✓✓

```
⚠ ƏVVƏL: proqram açılanda 8 ViewModel BİRDƏN bütün cədvəllərini oxuyurdu ✗
✅ İNDİ : yalnız AÇILAN tab yüklənir ✓ (Avto Park + Xərclərin 1-ci səhifəsi ✓)
         qalan tablar «çirkli» işarələnir ✓ → istifadəçi keçəndə yüklənir ✓✓✓
```

#### ⑦ 🔄 YENİLƏMƏ FIRTINASI DAYANDIRILDI ✗✓✓

```
⚠ ƏVVƏL: bir xərc əlavə edəndə 7 ViewModel öz cədvəlini BAŞDAN oxuyurdu ✗
✅ İNDİ : YALNIZ GÖRÜNƏN tab yenilənir ✓ (digərləri «çirkli» ✓)
• Avtomatik yeniləmə dövrü 5 saniyə ✗ → **30 saniyə** ✓
  (real dəyişikliklər onsuz da ANİ tutulur ✓ — fayl izləyicisi ✓)
```

#### ⑧ 🗄️ SQLite SAZLAMALARI (PRAGMA) ✓

| PRAGMA | Nə verir |
|---|---|
| `journal_mode=WAL` | oxuma və yazma EYNİ ANDA ✓ |
| `synchronous=NORMAL` | təhlükəsiz, amma 3-10× sürətli ✓ |
| `cache_size=-65536` | 64 MB səhifə keşi ✓ |
| `temp_store=MEMORY` | `GROUP BY`/`ORDER BY` RAM-da ✓ |
| `mmap_size=268435456` | 256 MB yaddaş xəritələmə ✓ |
| `busy_timeout=10000` | «database is locked» xətası olmur ✗ |
| `Pooling=True` | bağlantı hovuzu ✓ |

#### ⑨ ⚙️ İNDEKSLƏR (milyon sətir üçün) ✓

```sql
Xercler          : (Tarix, Id) · Qrup · Kategoriya · CarId · Axtaris · Teyinat
KreditEmeliyyatlari : (CreditId, Tarix) · Nov · Tarix
Kreditler        : CarId · Status
```
> `dotnet ef migrations add PerformansIndeksleriVeAxtaris` ✓ —
> mövcud quraşdırmalarda **avtomatik** tətbiq olunur ✓

#### ⑩ 📦 TƏK BİLDİRİŞLƏ KOLLEKSİYA YENİLƏMƏSİ ✓

```
⚠ ƏVVƏL: `Clear()` + 10 000 × `Add()` → cədvəl 10 000 dəfə yenidən qurulurdu ✗
✅ İNDİ : `BulkObservableCollection.ReplaceAll()` → TƏK «Reset» hadisəsi ✓✓✓
```

#### ⑪ 🖱️ «Ümumi Xərclər» BÖLMƏSİNİN YENİ GÖRÜNÜŞÜ ✓

```
[📄 200 / 1 234 567 sətir göstərilir]              [⏳ yüklənir…]  [⬇️ Daha çox yüklə]
```

> ℹ️ **Məbləğ sütunu sıralanmır** ✗ — SQLite `decimal`-i MƏTN kimi saxlayır ✗ →
> mətn sıralaması yanlış nəticə verərdi ✗ («100» < «20» ✗✓✓). Digər başlıqlar
> (Tarix · Təyinat · Qrup · Kateqoriya · Qeyd ✓) SQL-də sıralanır ✓

---


### ① 🗑️ Silinmə digər kompüterlərdə TƏTBİQ OLUNMURDU ✗ → HƏLL OLUNDU ✓

```
ƏVVƏL ✗: silinən qeydin «zibil» (tombstone) yazısı DƏRHAL buluddan silinirdi ✗
         → o anda oflayn / gecikən kompüterlər silinməni HEÇ VAXT öyrənmirdi ✗
         → «bir kompüterdə sildim, digərində silinmədi» ✗

İNDİ ✓: tombstone 60 GÜN buludda saxlanılır ✓
         → bütün kompüterlər MÜTLƏQ onu görür və silinməni tətbiq edir ✓✓✓
         (`ZibilSaxlamaGun = 60` ✓ — 60 gündən köhnələr buluddan silinir ✓)
```

### ② 🔢 Sıra nömrələri «0» yazılırdı ✗ → HƏLL OLUNDU ✓

```
ƏVVƏL ✗: bəzi avtomobillər (xüsusən 💳 Kreditlər bölməsindən əlavə olunanlar ✗)
         `SiraNomresi = 0` ilə qalırdı ✗ → proqramda da ✗, Firebase-də də ✗ «0» görünürdü

İNDİ ✓: 🚀 Proqram açılanda `CarService.SiraNomreleriniDuzeltAsync()` işləyir ✓
         → «0» olan BÜTÜN avtomobillərə ardıcıl nömrə verilir (1, 2, 3 … ✓)
         → təkrarsız ✓ · satılanların boş nömrələri yenidən istifadə olunur ✓
         → ☁️ Firebase-ə də DÜZGÜN nömrə gedir ✓✓✓
```

### ③ 💳 Kredit sıra nömrələri yalnız ekranda idi ✗ → HƏLL OLUNDU ✓

```
ƏVVƏL ✗: «💳 Kreditlər» cədvəlində nömrə YALNIZ EKRANDA hesablanırdı ✗
         → bazaya yazılmırdı ✗ → ☁️ Firebase-də «0» görünürdü ✗

İNDİ ✓: nömrə «0»-dırsa → ardıcıl nömrə verilir ✓ və BAZAYA YAZILIR ✓
         → Firebase-də də düzgün görünür ✓✓✓
```

### ④ 🔑 USB başqa kompüterdə TANINMIRDI ✗ → HƏLL OLUNDU ✓

```
ƏVVƏL ✗: USB bir kompüterdə qeydiyyatdan keçirdi ✓ — token `data_usb.json`-da
         YALNIZ o kompüterdə saxlanılırdı ✗
         → başqa kompüterə taxanda «USB datası yoxdur ✗» deyirdi ✗✓✓

İNDİ ✓: proqram açılışda fləşkartın kökündəki GİZLİ markeri oxuyur ✓
         (`021cars_drive.lock` ✓) → token AVTOMATİK yerli qeydiyyata yazılır ✓
         → HƏMİN kompüter də fləşkartı tanıyır ✓✓✓ (`UsbAvtomatikTanit()` ✓)
```

### ⑤ 💾 USB məlumatı digər kompüterə GƏTİRMİRDİ ✗ → HƏLL OLUNDU ✓

```
ƏVVƏL ✗: USB-dən baza YALNIZ yerli baza BOŞ olanda köçürülürdü ✗
         → mövcud baza varsa heç nə gəlmirdi ✗✓✓

İNDİ ✓: `UsbBazasiniBirləşdir()` — SQLite `ATTACH` + `INSERT OR IGNORE` ✓✓✓
         • Yerli bazada OLMAYAN bütün ID-lər USB-dən ƏLAVƏ olunur ✓
         • Mövcud qeydlərə TOXUNULMUR ✗ (heç nə itmir ✓)
         • 8 cədvəl: Cars · Sales · Credits · CreditTransactions ·
           Expenses · Partners · PartnerShares · PartnerPayments ✓
         • Sonra ☁️ Firebase-ə də göndərilir ✓✓✓

🖥️ NƏTİCƏ: bir kompüterdə yığılan məlumat USB ilə digər kompüterə GƏLİR ✓✓✓
          (Firebase kimi işləyir ✓ — internet olmasa da ✓)
```

---

## 📊 SİNXRONİZASİYA — YEKUN VƏZİYYƏT ✓

| Kanal | ⬆️ Göndərir | ⬇️ Gətirir | Oflayn işləyir |
|---|---|---|---|
| 🖥️ **Komputer (baza)** | — | — | ✅ həmişə ✓ |
| 🔥 **Firebase** | ✅ dəyişənlər ✓ + tombstone ✓ | ✅ LWW + **YENİ QEYDLƏR** ✓ | ✅ növbə ✓ sonra avtomatik ✓ |
| 💾 **DATA USB** | ✅ tam baza ✓ + media ✓ | ✅ **çatışmayanlar birləşdirilir** ✓ | ✅ USB yoxdursa proqram işləyir ✓ |

> ✅ **3 kanal bir-birini tamamlayır** ✓ — biri işləməyəndə digərləri işləyir ✓
> və kanal geri qayıdanda **çatışmayan hər şey avtomatik tamamlanır** ✓✓✓

---

## 🔧 v6.2 — SON İKİ XƏTANIN HƏLLİ ✗✓✓✓

### ① 🔓 «Access to the path 'D:\021cars_drive.lock' is denied» ✗ → HƏLL OLUNDU ✓

```
SƏBƏB ✗: marker faylı `FileAttributes.Hidden` ilə yaradılır ✓
         → 2-ci kompüterdə onu YENİDƏN YAZMAQ ✗ / SİLMƏK ✗ mümkün olmur ✗
         → «USB qeydə alına bilmədi: access denied» ✗✓✓
         → USB tanınmır ✗ → nə məlumat ✗, nə sənəd gəlir ✗

HƏLL ✓: 3 yeni təhlükəsiz köməkçi əlavə olundu ✓
         • `AtributTemizle(yol)`   → Hidden/ReadOnly → Normal ✓✓✓
         • `MecburiYaz(yol, mətn)` → yazmadan ƏVVƏL atributları təmizləyir ✓
         • `MecburiSil(yol)`       → atributlardan asılı olmayaraq silir ✓
         
         + `ElaveEt` işə düşəndə 3 yerdəki köhnə marker-lərin atributları
           ƏVVƏLCƏ təmizlənir ✓ (D:\ ✓ · D:\021Cars\ ✓ · D:\021Cars\data\ ✓)
```

> ✅ **Nəticə:** USB istənilən kompüterdə yenidən qeydə alına bilir ✓✓✓
> (və `UsbAvtomatikTanit()` sayəsində ümumiyyətlə qeydiyyat lazım olmur ✓)

### ② 🧟 SİLİNƏN MAŞIN GERİ QAYIDIRDI ✗ → HƏLL OLUNDU ✓

```
SƏBƏB ✗: USB-dəki baza KÖHNƏ surətdir ✗ → `INSERT OR IGNORE` silinmiş
         maşını YENİDƏN ƏLAVƏ EDİRDİ ✗ → «sildim, proqramı bağlayıb açdım,
         maşın YENİDƏN GƏLDİ» ✗✓✓

HƏLL ✓: birləşdirmədən ƏVVƏL TARİX YOXLANILIR ✓✓✓
         var usbVaxt   = File.GetLastWriteTime(usb baza);
         var yerliVaxt = File.GetLastWriteTime(yerli baza);

         if (usbVaxt <= yerliVaxt)   // ⏱️ USB köhnədir → BİRLƏŞDİRMƏ YOX ✗
             return "✅ USB bazası yerli bazadan KÖHNƏDIR ✓ — silinmiş qeydlər geri qayıtmır ✓✓✓";
```

> ✅ **Nəticə:** silinmə **qalıcıdır** ✓ — köhnə USB surəti maşını geri gətirmir ✗✓✓✓
> (USB **həqiqətən təzədirsə** ✓ — yəni başqa kompüterdə yeni məlumat yığılıbsa ✓ —
> birləşdirmə yenə işləyir ✓✓✓)

---

## 🧭 «DATA USB» DÜYMƏLƏRİ (tənzimləmələrdə ✓)

| Düymə | Nə edir |
|---|---|
| ➕ **Əlavə et** | 🆕 **YENİ USB yaradır** ✓ — marker + token + `Media\` quruluşu ✓ + baza surəti ✓ |
| 🔑 **Avtomatik tanıma** | 🚀 Proqram açılışda **mövcud USB-ni** marker-dən tanıyır ✓ (əlavə etmək lazım deyil ✗) |
| 📥 **USB bərpa/birləşdirmə** | 📖 Mövcud USB-nin **içini oxuyur** ✓ → proqrama inteqrasiya edir ✓ → sonra **USB-nin özünə də yazır** ✓✓✓ |
| 🗑️ **Sil** | Qeydiyyatı + markeri silir ✓ |

> 📌 **Qeyd:** mövcud USB-ni taxmaq üçün sadəcə **fləşkartı taxıb proqramı açın** ✓ —
> proqram özü oxuyur ✓, birləşdirir ✓ və USB-yə yazır ✓✓✓ (əl ilə heç nə etmək lazım deyil ✗)



---

## 🔧 v6.3 — «YENİDƏN DONDU» ✗ → HƏLL OLUNDU ✓✓✓

### 🔍 ƏSL SƏBƏB NƏ İDİ? (logdan tapıldı ✓ — `Logs\app_errors.log`)

| # | Səbəb | Sübut (log / fayl) |
|---|-------|--------------------|
| ① | **QURAŞDIRILMIŞ BUILD KÖHNƏ İDİ** ✗ — `publish\` 20:57-də yığılmışdı ✓, amma `C:\Program Files\021Cars`-da **18:17** build işləyirdi ✗ | `EnterpriseAeroStudio.dll` → **27.09.2026 18:17:40** (quraşdırılmış) vs **20:57:04** (yığılmış) |
| ② | **BULUD AYARLARI 5/5/5 İDİ** ✗ — `_tenzimleme` nodu 5 saniyəni məcbur edirdi ✗ | `⚙️ Ayarlar BULUDDAN götürüldü ✓ — ⏱️ 5 san · 💾 USB 5 san · 🖥️ Kompüter 5 san` |
| ③ | **KÖRPÜ DÖVRÜ BİR PROSESDƏ İKİ DƏFƏ BAŞLAMIŞDI** ✗ — köhnə `Basla()`-da qoruyucu yox idi ✗ → iki dövr → ikiqat oxu/yazma ✗ + eyni fayla iki yazma ✗ | 18:31:38 və 18:31:40 — **2 saniyə aralı İKİ** `🌉 BULUD KÖRPÜSÜ BAŞLADI` sətri ✓ (proses isə **BİR** idi ✓ — PID 18108) |
| ④ | **NƏTİCƏ:** USB faylı toqquşurdu ✗ | `System.IO.IOException` → `'D:\021Cars\Yedekler\Json\cars.json' … being used by another process` · iz: `BuludKopru.UsbYedekAsync` |
| ⑤ | **EF NAVİQASİYA SÜTUNU SQL-Ə DÜŞÜRDÜ** ✗ — `CarItem.Expenses` toplusu sütun kimi INSERT olunurdu ✗ → ID saxlayan əlavə uğursuz olurdu ✗ (təkrar qeydlər ✗) | `⚠️ CarItem İD ilə əlavə olunmadı ✗ (SQLite Error 1: 'table Avtomobiller has no column named Expenses')` |
| ⑥ | **YEDƏK HƏR ~9 SANİYƏDƏ YAZILIRDI** ✗ (2169 qeyd ✗) → disk 100% ✗ → UI donur ✗ | `💾 USB yedəyi yazıldı ✓ … · 2169 qeyd ✓` sətirləri 14:41–14:44 arası **hər 9 saniyə** ✗ |

### ✅ NƏ DÜZƏLDİLDİ? (kod ✓)

1. 🚫 **TƏK NÜSXƏ KİLİDİ** (`App.xaml.cs`) — `Local\021Cars_TekNusxe_v6` mutex ✓
   → ikinci nüsxə **açılmır** ✗✓✓; çıxışda `Kopru?.Dayandir()` ilə təmiz bağlanır ✓
2. 🧵 **`Basla()` QORUYUCUSU + TƏZƏ CTS** (`BuludKopru.cs`) — `lock (_dovruKilidi)` + `Isleyir` ✓
   → ləğv olunmuş token yerinə **təzə** `CancellationTokenSource` ✓ → dövr **həmişə BİR** ✓✓✓
3. 💾 **ATOMİK + NÖVBƏLİ FAYL YAZISI** (`FayliYazAsync` ✓) —
   `SemaphoreSlim` növbəsi ✓ + əvvəl `*.tmp` → sonra `File.Move(…, overwrite)` ✓ + 6 təkrar cəhd ✓
   → «faylı başqa proses tutub» xətası **olmur** ✗✓✓ · yarımçıq/boş JSON **qalmır** ✗✓✓
4. ⚙️ **AYARLARIN QORUNMASI** (`Tenzimlemeler.cs`) —
   `TətbiqEt()` içində `Normalize()` ✓ → buluddan **5 san** gəlsə belə **10/120+** tətbiq olunur ✓✓✓
   · düzəliş yerli `bulud_ayarlari.json`-a da yazılır ✓ («5 san» faylda qalmır ✗)
5. 🗄️ **SÜTUN SİYAHISI EF MODELİNDƏN** (`YerliEkleAsync`) — yalnız **real** sütunlar ✓
   (naviqasiya topluları deyil ✗) → `Expenses` xətası yox ✗ · qeyd **öz İD-si ilə** əlavə olunur ✓
6. 🛡️ **`INSERT OR IGNORE`** — eyni anda iki yazma cəhdi olsa təkrar qeyd **yaranmır** ✗✓✓
7. ⏱️ **YEDƏK ALT HƏDDİ 120 SANİYƏ** — `Math.Max(120, …)` ✓ (əvvəl 60 ✗)

### ☁️ BULUD AYARLARI DÜZƏLDİLDİ ✓

`021Cars/_tenzimleme` nodu **10 / 300 / 300** edildi ✓ (əvvəl 5 / 5 / 5 ✗):

```json
{"firebaseSaniye":10,"usbSaniye":300,"yerliSaniye":300,"yenilenme":"2026-09-28T14:55:00.0000000Z","sonDeyisen":"021CARS"}
```

> Bu sətir sayəsində **köhnə build** belə düzgün işləməyə başladı ✓✓✓

### 📉 NƏTİCƏ (canlı log ✓)

| Vaxt | USB + Kompüter yedəyi |
|------|------------------------|
| 14:41:18 → 14:44:05 | **hər ~9 saniyə** ✗ (disk 100% ✗ — DONMA ✗) |
| 14:44:13 | ⚙️ `⏱️ 10 san · 💾 USB 300 san · 🖥️ Kompüter 300 san` ✓ |
| 14:49:09 | **300 saniyə sonra** ✓ |
| 14:54:11 | **300 saniyə sonra** ✓ |
| 14:56:21 | ✅ **YENİ BUILD işə düşdü** ✓ (PID 21184) · `IOException` **yox** ✗ · `Expenses` xətası **yox** ✗ |

→ **33× az disk yazısı** ✓✓✓ · USB yedəyi 5 s → 300 s ✓ · bulud 5 s → 10 s ✓

### 🛠️ YERİNDƏ GÜNCƏLLƏMƏ (admin tələb olunur ✓)

`Docs\Qurasdirma\Guncelle-Yerinde.ps1` — sağ düymə → **Run with PowerShell** → UAC «Bəli» ✓

1. proqramı **bağlayır** ✓ (elevated proses olduğu üçün admin lazımdır ✓)
2. `publish\` fayllarını `C:\Program Files\021Cars`-a **kopyalayır** ✓
3. nəticəni **təsdiqləyir** ✓ (dll/exe tarixi · ayar faylı ✓)

Loq: `%TEMP%\guncelle.txt` ✓ — son nəticə: **`copied = 496 | failed = 0`** ✓✓✓
(`appsettings.json` və `bulud_ayarlari.json` **toxunulmur** ✗ — müştərinin ayarları qalır ✓)

> ⚠️ **VACİB:** `publish_setup\021Cars_Installer.exe` **yenidən yığılmalıdır** ✓ —
> `cd Docs\Qurasdirma\Installer ; .\Yig-Installer.ps1` ✓
> (köhnə installer **28.09.2026 14:22** ✗ — onun içindəki build **v6.3 düzəlişlərini SAXLAMIR** ✗)

---

## 🔇 v6.4 — «LOQ TUFANI» SÖNDÜRÜLDÜ ✓✓✓

### 🔍 SƏBƏB (logdan sayıldı ✓ — `Logs\app_errors.log`)

| # | Səbəb | Sübut (log / kod) |
|---|-------|-------------------|
| ① | **HƏR QEYD ÜÇÜN AYRI LOQ SƏTRİ** ✗ — `BoşSahələriDoldur()` hər doldurulan sətir üçün `Melumat()` çağırırdı ✗ | `🩹 «Avtomobiller» Id=… — 1 boş sahə mənbədən DOLDURULDU ✓` — **165 avtomobil** ✗ |
| ② | **BİR BƏRPA = ~500 SƏTİR** ✗ — USB bərpa **4 faylı** birləşdirəndə hər birləşdirmə ÜÇÜN yenidən bütün avtomobillər yoxlanılırdı ✗ | `2026-09-28 14:56` → **497 sətir** ✗ (09-27 20:xx → **213 sətir** ✗) |
| ③ | **NƏTİCƏ:** 17 467 sətirlik loq ✗ — disk yorulur ✗, real xəbərdarlıqlar itir ✗ | `(Get-Content app_errors.log).Count = 17467` ✗ |

### ✅ NƏ DÜZƏLDİLDİ? (`Services\DataUsbService.cs`)

**`BoşSahələriDoldur()` — per-record `Melumat()` çağırışı SİLİNDİ ✗✓✓**

| Əvvəl ✗ | İndi ✓ |
|---------|--------|
| hər qeyd üçün 1 sətir ✗ (~500 sətir ✗) | **yalnız BİR yekun sətir** ✓ (`   • 🩹 N BOŞ SAHƏ DOLDURULDU ✓` ✓) |

> ♻️ **Nəticə (doldurulan sayı ✓) DƏYİŞMİR** ✗ — `BirləşdirUnionNet()` sayı
> artıq **yığıb** BİR sətirdə yazır ✓ (`doldurulan += nəticə.Doldurulan` ✓).
> Yəni **funksiya eynidir ✓** — yalnız **loq** azalır ✓✓✓

### 📉 NƏTİCƏ (yeni build ✓)

| Göstərici | Əvvəl ✗ | İndi ✓ |
|-----------|---------|--------|
| Bərpa başına loq sətri | ~500 ✗ | **1** ✓ |
| `IOException` (USB faylı tutulub ✗) | var idi ✗ | **0** ✓✓✓ |
| `has no column named Expenses` ✗ | var idi ✗ | **0** ✓✓✓ |
| `🔄 Təkrar …` (offline təkrar ✗) | minlərlə ✗ | **6** ✓ |
| `❌ XƏTA` blokları ✗ | var idi ✗ | **0** ✓ |
| Canlı dövr | — | ☁️ `BULUD: 2177 qeyd göndərildi ✓ · 🗑️ 0 soft-silindi ✓` ✓ |

