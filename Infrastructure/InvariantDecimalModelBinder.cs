using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace DentistDB.Infrastructure;

/// <summary>
/// Accepts money values typed either the Turkish way ("1.250,50") or the invariant way ("1250.50").
/// The default binder parses with the request culture (tr-TR), which silently turns "250.50" into 25050.
/// </summary>
public sealed class InvariantDecimalModelBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var valueResult = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
        if (valueResult == ValueProviderResult.None)
        {
            return Task.CompletedTask;
        }

        bindingContext.ModelState.SetModelValue(bindingContext.ModelName, valueResult);

        var raw = valueResult.FirstValue?.Trim();
        if (string.IsNullOrEmpty(raw))
        {
            if (bindingContext.ModelType == typeof(decimal?))
            {
                bindingContext.Result = ModelBindingResult.Success(null);
            }
            else
            {
                bindingContext.ModelState.TryAddModelError(bindingContext.ModelName, "Bu alan zorunludur.");
            }

            return Task.CompletedTask;
        }

        if (TryParse(raw, out var value))
        {
            bindingContext.Result = ModelBindingResult.Success(value);
        }
        else
        {
            bindingContext.ModelState.TryAddModelError(bindingContext.ModelName, "Geçerli bir tutar girin (örn. 1250,50).");
        }

        return Task.CompletedTask;
    }

    public static bool TryParse(string raw, out decimal value)
    {
        value = 0m;
        var text = raw.Replace(" ", string.Empty).Replace("₺", string.Empty).Replace("TL", string.Empty, StringComparison.OrdinalIgnoreCase);

        if (text.Length == 0)
        {
            return false;
        }

        var lastComma = text.LastIndexOf(',');
        var lastDot = text.LastIndexOf('.');

        string normalized;
        if (lastComma >= 0 && lastDot >= 0)
        {
            // Whichever separator comes last is the decimal separator; the other is grouping.
            normalized = lastComma > lastDot
                ? text.Replace(".", string.Empty).Replace(',', '.')
                : text.Replace(",", string.Empty);
        }
        else if (lastComma >= 0)
        {
            normalized = text.Count(c => c == ',') == 1
                ? text.Replace(',', '.')
                : text.Replace(",", string.Empty);
        }
        else if (lastDot >= 0)
        {
            // Several dots ("1.250.000") are always grouping. A single dot followed by exactly three
            // digits ("1.250") is read as Turkish grouping; anything else ("250.50", "12.5") is a decimal point.
            var dotCount = text.Count(c => c == '.');
            var digitsAfterDot = text.Length - lastDot - 1;
            normalized = dotCount > 1 || digitsAfterDot == 3 ? text.Replace(".", string.Empty) : text;
        }
        else
        {
            normalized = text;
        }

        return decimal.TryParse(normalized, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value);
    }
}

public sealed class InvariantDecimalModelBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
    {
        var type = context.Metadata.UnderlyingOrModelType;
        return type == typeof(decimal) ? new InvariantDecimalModelBinder() : null;
    }
}
