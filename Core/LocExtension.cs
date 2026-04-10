using System;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace GrandFantasiaINIEditor.Core
{
    [ContentProperty(nameof(Key))]
    public class LocExtension : MarkupExtension
    {
        public string Key { get; set; }

        public LocExtension() { }

        public LocExtension(string key)
        {
            Key = key;
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(Key)) return string.Empty;

                var binding = new Binding
                {
                    Source = LocalizationManager.Instance,
                    Path = new PropertyPath("CurrentLanguage"),
                    Converter = new LocConverter(),
                    ConverterParameter = Key,
                    Mode = BindingMode.OneWay
                };
                return binding.ProvideValue(serviceProvider);
            }
            catch
            {
                return Key ?? string.Empty;
            }
        }
    }
}
