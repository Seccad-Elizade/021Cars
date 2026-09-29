/* ==========================================================================
   AvtoPark v6.0 — köməkçi JavaScript funksiyaları
   --------------------------------------------------------------------------
   Yalnız Blazor-un çağırdığı kiçik köməkçilər saxlanılır.
   ========================================================================== */

/**
 * Verilmiş ünvana sorğu göndərib HTTP status kodunu qaytarır.
 *
 * Diaqnostika səhifəsi (<c>/diaqnostika</c>) bu funksiya ilə
 * <c>blazor.web.js</c> və <c>app.css</c> fayllarının həqiqətən
 * yükləndiyini yoxlayır. Bu fayllar 404 olsa, HEÇ BİR düymə işləmir.
 *
 * @param {string} url Yoxlanılacaq ünvan (məs. "_framework/blazor.web.js").
 * @returns {Promise<number>} HTTP status kodu (0 = şəbəkə xətası).
 */
window.avtoparkFetchStatus = async function (url) {
    try {
        const response = await fetch(url, { cache: 'no-store' });
        return response.status;
    } catch {
        return 0;
    }
};
