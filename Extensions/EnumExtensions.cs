using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace DentistDB.Extensions;

public static class EnumExtensions
{
    public static string GetDisplayName(this Enum value)
    {
        var member = value.GetType().GetMember(value.ToString()).FirstOrDefault();
        var displayName = member?.GetCustomAttribute<DisplayAttribute>()?.GetName();
        return string.IsNullOrWhiteSpace(displayName) ? value.ToString() : displayName;
    }
}
