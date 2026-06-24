using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls.Xaml;
using UserMobile.Services;

namespace UserMobile.Markup;

[ContentProperty(nameof(Key))]
public sealed class TExtension : IMarkupExtension<BindingBase>
{
    public string Key { get; set; } = string.Empty;

    public BindingBase ProvideValue(IServiceProvider serviceProvider)
    {
        var localization = App.Services.GetRequiredService<ILocalizationService>();
        return new Binding($"[{Key}]", BindingMode.OneWay, source: localization);
    }

    object IMarkupExtension.ProvideValue(IServiceProvider serviceProvider)
    {
        return ProvideValue(serviceProvider);
    }
}
