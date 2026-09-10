using DentistDB.Extensions;

namespace DentistDB.Services;

public interface ICurrentAccountAccessor
{
    /// <summary>"admin", "worker", or "system" when no request is active (startup, background jobs).</summary>
    string AccountKey { get; }
}

public sealed class CurrentAccountAccessor : ICurrentAccountAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentAccountAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string AccountKey
    {
        get
        {
            var context = _httpContextAccessor.HttpContext;
            if (context is null)
            {
                return "system";
            }

            try
            {
                return context.GetCurrentAccount()?.Key ?? "anonim";
            }
            catch (InvalidOperationException)
            {
                // Session middleware not yet run for this request.
                return "anonim";
            }
        }
    }
}
