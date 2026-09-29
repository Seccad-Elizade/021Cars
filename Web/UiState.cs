namespace EnterpriseAeroStudio.Web;

/// <summary>
/// Blazor dövrə (circuit) üzrə paylaşılan qısa müddətli UI vəziyyəti —
/// səhifələr arası bildiriş (toast) mesajları üçün istifadə olunur.
/// </summary>
public sealed class UiState
{
    /// <summary>Mesaj dəyişdikdə baş verir (MainLayout abunə olur).</summary>
    public event Action? Changed;

    /// <summary>Göstərilən mesaj (yoxdursa <c>null</c>).</summary>
    public string? Message { get; private set; }

    /// <summary>Mesajın növü: <c>ok</c> | <c>warn</c> | <c>error</c>.</summary>
    public string Kind { get; private set; } = "ok";

    /// <summary>Uğurlu əməliyyat bildirişi.</summary>
    public void Success(string message) => Set(message, "ok");

    /// <summary>Xəbərdarlıq bildirişi.</summary>
    public void Warn(string message) => Set(message, "warn");

    /// <summary>Xəta bildirişi.</summary>
    public void Error(string message) => Set(message, "error");

    /// <summary>Mesajı təmizləyir.</summary>
    public void Clear()
    {
        Message = null;
        Changed?.Invoke();
    }

    private void Set(string message, string kind)
    {
        Message = message;
        Kind = kind;
        Changed?.Invoke();
    }
}
