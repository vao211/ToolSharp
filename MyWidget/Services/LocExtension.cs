using System.Windows;
using System.Windows.Data;

namespace MyWidget.Services;

public class LocExtension : Binding
{
   public LocExtension()
   {
      Source = LocalizationService.Instance;
      Mode = BindingMode.OneWay;
   }

   public LocExtension(string key) : this()
   {
      Path = new PropertyPath($"[{key}]");
   }
}
