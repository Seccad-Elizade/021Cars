namespace EnterpriseAeroStudio.Services
{
    /// <summary>
    /// Verilənlər bazası faylının dəyişməsini izləyən xidmət.
    /// <para>
    /// Masaüstü (WPF) tətbiq və veb server EYNİ SQLite faylını işlədir.
    /// Bu xidmət həmin faylı izləyir və hər dəyişiklikdə <see cref="Changed"/>
    /// hadisəsini işə salır — beləliklə bir tərəfdə edilən dəyişiklik
    /// digər tərəfə DƏRHAL çatır.
    /// </para>
    /// </summary>
    public interface IDataChangeWatcher : IDisposable
    {
        /// <summary>Baza faylı dəyişdikdə baş verir (arxa planda, başqa axında).</summary>
        event EventHandler? Changed;

        /// <summary>İzləmə aktivdirmi?</summary>
        bool IsRunning { get; }

        /// <summary>Aşkarlanmış dəyişikliklərin sayı (diaqnostika üçün).</summary>
        int ChangeCount { get; }

        /// <summary>Son dəyişikliyin vaxtı (UTC); hələ olmayıbsa <c>null</c>.</summary>
        DateTime? LastChangeUtc { get; }

        /// <summary>İzləməni başladır (artıq işləyirsə heç nə etmir).</summary>
        void Start();

        /// <summary>İzləməni dayandırır.</summary>
        void Stop();
    }
}
