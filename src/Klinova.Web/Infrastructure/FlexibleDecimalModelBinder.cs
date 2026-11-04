using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Klinova.Web.Infrastructure;

/// <summary>
/// Binds decimals typed in Turkish format ("1.234,50") as well as values sent by
/// <c>&lt;input type="number"&gt;</c>, which always use a dot ("1234.50"). The
/// default binder would read "120.50" as 12 050 under the tr-TR culture.
/// </summary>
public sealed class FlexibleDecimalModelBinder : IModelBinder
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    public Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var result = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
        if (result == ValueProviderResult.None)
        {
            return Task.CompletedTask;
        }

        bindingContext.ModelState.SetModelValue(bindingContext.ModelName, result);
        var raw = result.FirstValue?.Trim().Replace("₺", string.Empty, StringComparison.Ordinal).Replace(" ", string.Empty, StringComparison.Ordinal);

        if (string.IsNullOrEmpty(raw))
        {
            if (bindingContext.ModelMetadata.IsReferenceOrNullableType)
            {
                bindingContext.Result = ModelBindingResult.Success(null);
            }

            return Task.CompletedTask;
        }

        if (TryParse(raw, out var value))
        {
            bindingContext.Result = ModelBindingResult.Success(value);
        }
        else
        {
            bindingContext.ModelState.TryAddModelError(bindingContext.ModelName, $"'{result.FirstValue}' geçerli bir tutar değil.");
        }

        return Task.CompletedTask;
    }

    public static bool TryParse(string raw, out decimal value)
        => raw.Contains(',', StringComparison.Ordinal)
            ? decimal.TryParse(raw, NumberStyles.Number, Turkish, out value)
            : decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
}

public sealed class FlexibleDecimalModelBinderProvider : IModelBinderProvider
{
    public IModelBinder? GetBinder(ModelBinderProviderContext context)
        => context.Metadata.UnderlyingOrModelType == typeof(decimal) ? new FlexibleDecimalModelBinder() : null;
}
