// ============================================================================
//  🚗 021Cars — DOMAIN MODELLƏRİ (07)  ★ Firebase POCO ★
// ----------------------------------------------------------------------------
//  ✅ Hər model <see cref="FirebaseEntity"/>-dən törəyir ✓
//     → id ✓ updatedAt ✓ updatedBy ✓ isDeleted ✓ deletedAt ✓ AVTOMATİK ✓
//  ✅ [JsonPropertyName] → Firebase-dəki sahə adı ✓ (camelCase ✓)
//  ✅ NULLABLE istifadə olunur ✓ (köhnə qeydlər üçün təhlükəsiz ✓)
// ============================================================================

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Cas0201.Firebase.Modeller
{
    /// <summary>🗂️ Kolleksiya adları ✓ (DƏYİŞMƏZ ✗ — bulud strukturu ✓)</summary>
    public static class Kolleksiyalar
    {
        public const string Cars = "cars";
        public const string Customers = "customers";
        public const string Expenses = "expenses";
        public const string Sales = "sales";
        public const string Credits = "creditTerms";
        public const string CreditTransactions = "creditTransactions";
        public const string Partners = "partners";
        public const string PartnerShares = "partnerShares";
        public const string PartnerPayments = "partnerPayments";
        public const string TransferRecipients = "transferRecipients";
        public const string Media = "media";
        public const string Users = "users";
        public const string UsbDevices = "usbDevices";
        public const string Trash = "trash";
        public const string Settings = "settings";
    }

    /// <summary>🚗 <b>AVTOMOBİL</b> ✓ — <c>021Cars/cars/{id}</c> ✓</summary>
    public sealed class Avtomobil : FirebaseEntity
    {
        [JsonPropertyName("siraNomresi")] public int SiraNomresi { get; set; }
        [JsonPropertyName("marka")] public string Marka { get; set; } = "";
        [JsonPropertyName("qeydiyyatNisani")] public string QeydiyyatNisani { get; set; } = "";
        [JsonPropertyName("vin")] public string Vin { get; set; } = "";
        [JsonPropertyName("il")] public int Il { get; set; }
        [JsonPropertyName("yurus")] public int Yurus { get; set; }
        [JsonPropertyName("yanacaq")] public string Yanacaq { get; set; } = "";
        [JsonPropertyName("alisTarixi")] public string AlisTarixi { get; set; } = "";
        [JsonPropertyName("alisUsulu")] public string AlisUsulu { get; set; } = "";
        [JsonPropertyName("alisQiymeti")] public double AlisQiymeti { get; set; }
        [JsonPropertyName("xerclerCemi")] public double XerclerCemi { get; set; }
        [JsonPropertyName("mayaDeyeri")] public double MayaDeyeri { get; set; }

        /// <summary>📊 Status ✓ — <c>Stokda</c> · <c>Satisda</c> · <c>Kreditde</c> · <c>Satildi</c> · <c>BarterEdildi</c> ✓</summary>
        [JsonPropertyName("status")] public string Status { get; set; } = "Stokda";

        [JsonPropertyName("kreditNomresi")] public string KreditNomresi { get; set; } = "";
        [JsonPropertyName("barterTesviri")] public string BarterTesviri { get; set; } = "";
        [JsonPropertyName("barterDeyeri")] public double BarterDeyeri { get; set; }
        [JsonPropertyName("barterCarId")] public string? BarterCarId { get; set; }
        [JsonPropertyName("customerId")] public string? CustomerId { get; set; }
        [JsonPropertyName("isBarter")] public bool IsBarter { get; set; }
        [JsonPropertyName("senedSayi")] public int SenedSayi { get; set; }
        [JsonPropertyName("qeyd")] public string Qeyd { get; set; } = "";

        /// <summary>🚗 Göstəriş: <c>001 - Hyundai Sonata - 99QE103</c> ✓</summary>
        [JsonIgnore]
        public string TamAd =>
            $"{SiraNomresi:D3} - {Marka} - {QeydiyyatNisani}";
    }

    /// <summary>👤 <b>MÜŞTƏRİ</b> ✓ — <c>021Cars/customers/{id}</c> ✓</summary>
    public sealed class Musteri : FirebaseEntity
    {
        [JsonPropertyName("ad")] public string Ad { get; set; } = "";
        [JsonPropertyName("soyad")] public string Soyad { get; set; } = "";
        [JsonPropertyName("ataAdi")] public string AtaAdi { get; set; } = "";
        [JsonPropertyName("telefon")] public string Telefon { get; set; } = "";
        [JsonPropertyName("finKod")] public string FinKod { get; set; } = "";
        [JsonPropertyName("seriyaNomre")] public string SeriyaNomre { get; set; } = "";
        [JsonPropertyName("unvan")] public string Unvan { get; set; } = "";
        [JsonPropertyName("musteriNomresi")] public string MusteriNomresi { get; set; } = "";
        [JsonPropertyName("qeyd")] public string Qeyd { get; set; } = "";

        [JsonIgnore]
        public string TamAd => $"{Ad} {Soyad} {AtaAdi}".Trim();
    }

    /// <summary>💸 <b>XƏRC</b> ✓ — <c>021Cars/expenses/{id}</c> ✓</summary>
    public sealed class Xerc : FirebaseEntity
    {
        [JsonPropertyName("carId")] public string? CarId { get; set; }
        [JsonPropertyName("tarix")] public string Tarix { get; set; } = "";
        [JsonPropertyName("qrup")] public string Qrup { get; set; } = "";
        [JsonPropertyName("kategoriya")] public string Kategoriya { get; set; } = "";
        [JsonPropertyName("teyinat")] public string Teyinat { get; set; } = "";
        [JsonPropertyName("teyinatNovu")] public string TeyinatNovu { get; set; } = "";
        [JsonPropertyName("odenisUsulu")] public string OdenisUsulu { get; set; } = "";
        [JsonPropertyName("mebleg")] public double Mebleg { get; set; }
        [JsonPropertyName("qeyd")] public string Qeyd { get; set; } = "";
    }

    /// <summary>🤝 <b>SATIŞ</b> ✓ — <c>021Cars/sales/{id}</c> ✓</summary>
    public sealed class Satis : FirebaseEntity
    {
        [JsonPropertyName("muqavileNomresi")] public string MuqavileNomresi { get; set; } = "";
        [JsonPropertyName("carId")] public string? CarId { get; set; }
        [JsonPropertyName("customerId")] public string? CustomerId { get; set; }
        [JsonPropertyName("receivedCarId")] public string? ReceivedCarId { get; set; }
        [JsonPropertyName("musteri")] public string Musteri { get; set; } = "";
        [JsonPropertyName("satisTarixi")] public string SatisTarixi { get; set; } = "";
        [JsonPropertyName("satisQiymeti")] public double SatisQiymeti { get; set; }
        [JsonPropertyName("mayaDeyeri")] public double MayaDeyeri { get; set; }
        [JsonPropertyName("menfeet")] public double Menfeet { get; set; }
        [JsonPropertyName("odenisUsulu")] public string OdenisUsulu { get; set; } = "";
        [JsonPropertyName("barterMebleg")] public double BarterMebleg { get; set; }
        [JsonPropertyName("nagdMebleg")] public double NagdMebleg { get; set; }
        [JsonPropertyName("barterTesviri")] public string BarterTesviri { get; set; } = "";
        [JsonPropertyName("qeyd")] public string Qeyd { get; set; } = "";
    }

    /// <summary>🏦 <b>KREDİT MÜQAVİLƏSİ</b> ✓ — <c>021Cars/creditTerms/{id}</c> ✓</summary>
    public sealed class Kredit : FirebaseEntity
    {
        [JsonPropertyName("saleId")] public string? SaleId { get; set; }
        [JsonPropertyName("carId")] public string? CarId { get; set; }
        [JsonPropertyName("customerId")] public string? CustomerId { get; set; }
        [JsonPropertyName("muqavileNomresi")] public string MuqavileNomresi { get; set; } = "";
        [JsonPropertyName("mebleg")] public double Mebleg { get; set; }
        [JsonPropertyName("ilkinOdenis")] public double IlkinOdenis { get; set; }
        [JsonPropertyName("kreditlesdirilen")] public double Kreditlesdirilen { get; set; }
        [JsonPropertyName("faizDerecesi")] public double FaizDerecesi { get; set; }
        [JsonPropertyName("kreditQiymeti")] public double KreditQiymeti { get; set; }
        [JsonPropertyName("ayligOdenis")] public double AyligOdenis { get; set; }
        [JsonPropertyName("muddetAy")] public int MuddetAy { get; set; }
        [JsonPropertyName("baslamaTarixi")] public string BaslamaTarixi { get; set; } = "";
        [JsonPropertyName("odenilmis")] public double Odenilmis { get; set; }
        [JsonPropertyName("qaliqBorc")] public double QaliqBorc { get; set; }
        [JsonPropertyName("status")] public string Status { get; set; } = "Aktiv";
        [JsonPropertyName("erkenBaglanma")] public bool ErkenBaglanma { get; set; }
        [JsonPropertyName("baglanmaTarixi")] public string? BaglanmaTarixi { get; set; }
        [JsonPropertyName("qeyd")] public string Qeyd { get; set; } = "";
    }

    /// <summary>💳 <b>KREDİT ƏMƏLİYYATI</b> ✓ — <c>021Cars/creditTransactions/{id}</c> ✓</summary>
    public sealed class KreditEmeliyyati : FirebaseEntity
    {
        [JsonPropertyName("creditId")] public string? CreditId { get; set; }
        [JsonPropertyName("carId")] public string? CarId { get; set; }

        /// <summary>📋 <c>Gelir</c> · <c>Gecikme</c> · <c>Barter</c> · <c>Transfer</c> · <c>VaxtindanTezBaglama</c> ✓</summary>
        [JsonPropertyName("nov")] public string Nov { get; set; } = "Gelir";

        [JsonPropertyName("mebleg")] public double Mebleg { get; set; }
        [JsonPropertyName("tarix")] public string Tarix { get; set; } = "";
        [JsonPropertyName("installmentNo")] public int? InstallmentNo { get; set; }
        [JsonPropertyName("mohletTarixi")] public string? MohletTarixi { get; set; }
        [JsonPropertyName("gecikmeTarixi")] public string? GecikmeTarixi { get; set; }
        [JsonPropertyName("odenilib")] public bool Odenilib { get; set; }
        [JsonPropertyName("tesvir")] public string Tesvir { get; set; } = "";
        [JsonPropertyName("transferSexs")] public string TransferSexs { get; set; } = "";
        [JsonPropertyName("gecikmeGun")] public int GecikmeGun { get; set; }
        [JsonPropertyName("qeyd")] public string Qeyd { get; set; } = "";
    }

    /// <summary>👥 <b>TƏRƏFDAŞ</b> ✓ — <c>021Cars/partners/{id}</c> ✓</summary>
    public sealed class Terefdas : FirebaseEntity
    {
        [JsonPropertyName("ad")] public string Ad { get; set; } = "";
        [JsonPropertyName("faiz")] public double Faiz { get; set; }
        [JsonPropertyName("qaligPayi")] public bool QaligPayi { get; set; }
        [JsonPropertyName("aktiv")] public bool Aktiv { get; set; } = true;
        [JsonPropertyName("sira")] public int Sira { get; set; }
        [JsonPropertyName("qeyd")] public string Qeyd { get; set; } = "";
    }

    /// <summary>💰 <b>TƏRƏFDAŞ PAYI</b> ✓ — <c>021Cars/partnerShares/{id}</c> ✓</summary>
    public sealed class TerefdasPayi : FirebaseEntity
    {
        /// <summary>📎 <c>Avtomobil</c> · <c>KreditEmeliyyati</c> · <c>Satis</c> ✓</summary>
        [JsonPropertyName("refType")] public string RefType { get; set; } = "Avtomobil";

        [JsonPropertyName("refId")] public string RefId { get; set; } = "";
        [JsonPropertyName("partnerId")] public string PartnerId { get; set; } = "";
        [JsonPropertyName("terefdas")] public string Terefdas { get; set; } = "";
        [JsonPropertyName("faiz")] public double Faiz { get; set; }
        [JsonPropertyName("qaligPayi")] public bool QaligPayi { get; set; }
        [JsonPropertyName("mebleg")] public double Mebleg { get; set; }
        [JsonPropertyName("baza")] public double Baza { get; set; }
        [JsonPropertyName("aktiv")] public bool Aktiv { get; set; } = true;
        [JsonPropertyName("sira")] public int Sira { get; set; }
    }

    /// <summary>📎 <b>MEDIA / SƏNƏD</b> ✓ — fayl YALNIZ fləşkartda ✓ · buluddaki sahə NİSBİ YOL ✓✓✓</summary>
    public sealed class MediaFayl : FirebaseEntity
    {
        /// <summary>📎 <c>Avtomobil</c> · <c>Kredit</c> · <c>KreditEmeliyyati</c> · <c>Satis</c> ✓</summary>
        [JsonPropertyName("refType")] public string RefType { get; set; } = "Avtomobil";

        [JsonPropertyName("refId")] public string RefId { get; set; } = "";
        [JsonPropertyName("fileName")] public string FileName { get; set; } = "";

        /// <summary>🛣️ <b>NİSBİ YOL</b> ✓ — hərf YOXDUR ✗ (məs. <c>Media/Avtomobil/001 - Sonata/a.jpg</c> ✓)</summary>
        [JsonPropertyName("storedPath")] public string StoredPath { get; set; } = "";

        [JsonPropertyName("sizeBytes")] public long SizeBytes { get; set; }
        [JsonPropertyName("tarix")] public string Tarix { get; set; } = "";
        [JsonPropertyName("usbToken")] public string UsbToken { get; set; } = "";
        [JsonPropertyName("qeyd")] public string Qeyd { get; set; } = "";
    }

    /// <summary>👤 <b>İSTİFADƏÇİ</b> ✓ — <c>021Cars/users/{id}</c> ✓</summary>
    public sealed class Istifadeci : FirebaseEntity
    {
        [JsonPropertyName("username")] public string Username { get; set; } = "";
        [JsonPropertyName("fullName")] public string FullName { get; set; } = "";
        [JsonPropertyName("passwordHash")] public string PasswordHash { get; set; } = "";
        [JsonPropertyName("passwordSalt")] public string PasswordSalt { get; set; } = "";
        [JsonPropertyName("role")] public string Role { get; set; } = "Sahil";
        [JsonPropertyName("aktiv")] public bool Aktiv { get; set; } = true;
        [JsonPropertyName("lastLoginAt")] public string? LastLoginAt { get; set; }
    }

    /// <summary>⚙️ <b>AYAR</b> ✓ — <c>021Cars/settings/{id}</c> ✓</summary>
    public sealed class Ayar : FirebaseEntity
    {
        [JsonPropertyName("key")] public string Key { get; set; } = "";
        [JsonPropertyName("value")] public string Value { get; set; } = "";
        [JsonPropertyName("qrup")] public string Qrup { get; set; } = "Umumi";
        [JsonPropertyName("qeyd")] public string Qeyd { get; set; } = "";
    }

    /// <summary>🗑️ <b>ZİBİL QEYDİ</b> ✓ — silinmənin tam surəti ✓ (bərpa üçün ✓✓✓)</summary>
    public sealed class ZibilQeydi : FirebaseEntity
    {
        [JsonPropertyName("refType")] public string RefType { get; set; } = "";
        [JsonPropertyName("refId")] public string RefId { get; set; } = "";
        [JsonPropertyName("payload")] public Dictionary<string, object?>? Payload { get; set; }
        [JsonPropertyName("silinmeTarixi")] public string SilinmeTarixi { get; set; } = "";
        [JsonPropertyName("silen")] public string Silen { get; set; } = "";
        [JsonPropertyName("sebeb")] public string Sebeb { get; set; } = "";
    }
}
