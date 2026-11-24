using System.Globalization;

namespace MyWidget.Models;

public class AppSettings
{
   public bool IsDarkMode { get; set; }
   public string AccentColor { get; set; } = "#3B82F6";
   public double CornerRadius { get; set; } = 12.0;
   public bool ShowDescriptions { get; set; }

   public string LanguageFile { get; set; } =
      CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("vi", StringComparison.OrdinalIgnoreCase)
         ? "vi_vn.json"
         : "en_us.json";

   public bool StartWithWindows { get; set; }
   public bool CopyImagesToAppData { get; set; } = true;
   public bool HideToTrayOnClose { get; set; } = true;

   public NotePinMode DefaultPinMode { get; set; } = NotePinMode.Desktop;
   public string DefaultNoteColor { get; set; } = "#FACC15";
}
