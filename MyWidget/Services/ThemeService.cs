using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace MyWidget.Services;

public record NoteVisualPalette(
   SolidColorBrush AccentBrush,
   SolidColorBrush AccentForegroundBrush,
   SolidColorBrush HeaderBrush,
   SolidColorBrush HeaderForegroundBrush,
   SolidColorBrush SurfaceBrush,
   SolidColorBrush ToolbarBrush,
   SolidColorBrush BorderBrush,
   SolidColorBrush ForegroundBrush,
   SolidColorBrush SecondaryForegroundBrush,
   SolidColorBrush HoverBrush);

public static class ThemeService
{
   private const int DwmwaUseImmersiveDarkModeBefore20H1 = 19;
   private const int DwmwaUseImmersiveDarkMode = 20;

   public static readonly string[] AppAccentPresets =
   [
      "#3B82F6", // Blue
      "#8B5CF6", // Violet
      "#10B981", // Emerald
      "#F59E0B", // Amber
      "#F43F5E", // Rose
      "#06B6D4", // Cyan
      "#6366F1", // Indigo
      "#EC4899", // Pink
      "#F97316", // Orange
      "#14B8A6"  // Teal
   ];

   /// <summary>
   /// Empty string means follow App Accent color.
   /// </summary>
   public static readonly string[] NoteColorPresets =
   [
      "",        // Follow App Accent
      "#FACC15", // Sticky Yellow
      "#34D399", // Mint Green
      "#60A5FA", // Sky Blue
      "#F472B6", // Sakura Pink
      "#A78BFA", // Lavender Purple
      "#FB923C", // Warm Orange
      "#2DD4BF", // Aqua Teal
      "#F87171", // Coral Red
      "#94A3B8"  // Slate Gray
   ];

   public static bool IsDarkMode { get; private set; }
   public static string CurrentAccentHex { get; private set; } = "#3B82F6";
   public static double CurrentCornerRadius { get; private set; } = 12.0;

   public static event EventHandler? ThemeChanged;

   [DllImport("dwmapi.dll")]
   private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

   public static bool TryParseHexColor(string? hex, out Color color)
   {
      color = Color.FromRgb(59, 130, 246);
      if (string.IsNullOrWhiteSpace(hex))
         return false;

      var clean = hex.Trim();
      if (!clean.StartsWith('#'))
         clean = "#" + clean;

      try
      {
         if (ColorConverter.ConvertFromString(clean) is Color parsed)
         {
            color = Color.FromRgb(parsed.R, parsed.G, parsed.B);
            return true;
         }
      }
      catch
      {
      }

      return false;
   }

   public static string NormalizeHex(string? hex, string fallback = "#3B82F6")
   {
      return TryParseHexColor(hex, out var c)
         ? $"#{c.R:X2}{c.G:X2}{c.B:X2}"
         : fallback;
   }

   public static Color Blend(Color baseColor, Color tintColor, double tintRatio)
   {
      tintRatio = Math.Clamp(tintRatio, 0, 1);
      var inv = 1.0 - tintRatio;
      return Color.FromRgb(
         (byte)Math.Clamp(Math.Round(baseColor.R * inv + tintColor.R * tintRatio), 0, 255),
         (byte)Math.Clamp(Math.Round(baseColor.G * inv + tintColor.G * tintRatio), 0, 255),
         (byte)Math.Clamp(Math.Round(baseColor.B * inv + tintColor.B * tintRatio), 0, 255));
   }

   public static double GetLuminance(Color c)
   {
      return (0.299 * c.R + 0.587 * c.G + 0.114 * c.B) / 255.0;
   }

   private static SolidColorBrush FrozenBrush(Color c)
   {
      var b = new SolidColorBrush(c);
      b.Freeze();
      return b;
   }

   public static void ApplyTheme(bool isDark, string? accentHex = null, double? cornerRadius = null, bool? showDescriptions = null)
   {
      IsDarkMode = isDark;
      if (!string.IsNullOrWhiteSpace(accentHex))
         CurrentAccentHex = NormalizeHex(accentHex, CurrentAccentHex);
      if (cornerRadius.HasValue)
         CurrentCornerRadius = Math.Clamp(cornerRadius.Value, 0, 24);

      var app = Application.Current;
      if (app is null) return;

      var targetSource = isDark ? "Themes/DarkTheme.xaml" : "Themes/LightTheme.xaml";
      var newDict = new ResourceDictionary
      {
         Source = new Uri(targetSource, UriKind.Relative)
      };

      var merged = app.Resources.MergedDictionaries;
      var existingIndex = -1;
      for (var i = 0; i < merged.Count; i++)
      {
         var src = merged[i].Source?.OriginalString;
         if (src is not null &&
             (src.EndsWith("LightTheme.xaml", StringComparison.OrdinalIgnoreCase) ||
              src.EndsWith("DarkTheme.xaml", StringComparison.OrdinalIgnoreCase)))
         {
            existingIndex = i;
            break;
         }
      }

      if (existingIndex >= 0)
         merged[existingIndex] = newDict;
      else
         merged.Insert(0, newDict);

      ApplyAccentResources(app.Resources, CurrentAccentHex, isDark);
      ApplyCornerRadiusResources(app.Resources, CurrentCornerRadius);

      if (showDescriptions.HasValue)
         ApplyHintsVisibility(showDescriptions.Value);

      foreach (Window window in app.Windows)
      {
         ApplyTitleBarTheme(window, isDark);
      }

      ThemeChanged?.Invoke(null, EventArgs.Empty);
   }

   public static void ApplyCornerRadius(double radius)
   {
      CurrentCornerRadius = Math.Clamp(radius, 0, 24);
      var app = Application.Current;
      if (app is null) return;
      ApplyCornerRadiusResources(app.Resources, CurrentCornerRadius);
   }

   public static void ApplyHintsVisibility(bool showDescriptions)
   {
      var app = Application.Current;
      if (app is null) return;
      app.Resources["DescVisibility"] = showDescriptions ? Visibility.Visible : Visibility.Collapsed;
   }

   private static void ApplyCornerRadiusResources(ResourceDictionary resources, double r)
   {
      r = Math.Round(Math.Clamp(r, 0, 24), 1);
      var inner = Math.Max(0, r - 1);
      var ctrl = Math.Round(r * 0.67, 1);
      var small = Math.Round(r * 0.50, 1);
      var tiny = Math.Round(r * 0.35, 1);

      resources["CardCornerRadius"] = new CornerRadius(r);
      resources["NoteInnerCornerRadius"] = new CornerRadius(inner);
      resources["NoteHeaderCornerRadius"] = new CornerRadius(inner, inner, 0, 0);
      resources["NoteFooterCornerRadius"] = new CornerRadius(0, 0, inner, inner);
      resources["ControlCornerRadius"] = new CornerRadius(ctrl);
      resources["SmallCornerRadius"] = new CornerRadius(small);
      resources["TinyCornerRadius"] = new CornerRadius(tiny);
   }

   private static void ApplyAccentResources(ResourceDictionary resources, string accentHex, bool isDark)
   {
      if (!TryParseHexColor(accentHex, out var accent))
         accent = Color.FromRgb(59, 130, 246);

      var hover = isDark
         ? Blend(accent, Colors.White, 0.16)
         : Blend(accent, Colors.Black, 0.12);

      var pressed = isDark
         ? Blend(accent, Colors.Black, 0.18)
         : Blend(accent, Colors.Black, 0.24);

      var surfaceBase = isDark ? Color.FromRgb(30, 30, 34) : Colors.White;
      var soft = Blend(surfaceBase, accent, isDark ? 0.22 : 0.14);
      var border = Blend(surfaceBase, accent, isDark ? 0.50 : 0.42);
      var fgOnAccent = GetLuminance(accent) > 0.64
         ? Color.FromRgb(17, 24, 39)
         : Colors.White;

      resources["AccentColor"] = accent;
      resources["AccentBrush"] = FrozenBrush(accent);
      resources["AccentHoverBrush"] = FrozenBrush(hover);
      resources["AccentPressedBrush"] = FrozenBrush(pressed);
      resources["AccentSoftBrush"] = FrozenBrush(soft);
      resources["AccentBorderBrush"] = FrozenBrush(border);
      resources["AccentForegroundBrush"] = FrozenBrush(fgOnAccent);
   }

   public static NoteVisualPalette ComputeNotePalette(string? noteColorHex, bool isDark, string? appAccentHex = null)
   {
      var fallbackAccent = string.IsNullOrWhiteSpace(appAccentHex) ? CurrentAccentHex : appAccentHex;
      var effectiveHex = string.IsNullOrWhiteSpace(noteColorHex) ? fallbackAccent : noteColorHex;
      if (!TryParseHexColor(effectiveHex, out var rawColor))
         TryParseHexColor(fallbackAccent, out rawColor);

      if (isDark)
      {
         var darkBase = Color.FromRgb(30, 31, 36);
         var darkHeaderBase = Color.FromRgb(24, 25, 30);
         var surface = Blend(darkBase, rawColor, 0.13);
         var header = Blend(darkHeaderBase, rawColor, 0.34);
         var toolbar = Blend(darkHeaderBase, rawColor, 0.16);
         var border = Blend(Color.FromRgb(63, 63, 70), rawColor, 0.45);
         var hover = Blend(surface, Colors.White, 0.10);
         var accent = GetLuminance(rawColor) < 0.35 ? Blend(rawColor, Colors.White, 0.28) : rawColor;
         var accentFg = GetLuminance(accent) > 0.62 ? Color.FromRgb(17, 24, 39) : Colors.White;

         return new NoteVisualPalette(
            AccentBrush: FrozenBrush(accent),
            AccentForegroundBrush: FrozenBrush(accentFg),
            HeaderBrush: FrozenBrush(header),
            HeaderForegroundBrush: FrozenBrush(Color.FromRgb(249, 250, 251)),
            SurfaceBrush: FrozenBrush(surface),
            ToolbarBrush: FrozenBrush(toolbar),
            BorderBrush: FrozenBrush(border),
            ForegroundBrush: FrozenBrush(Color.FromRgb(243, 244, 246)),
            SecondaryForegroundBrush: FrozenBrush(Color.FromRgb(161, 161, 170)),
            HoverBrush: FrozenBrush(hover));
      }
      else
      {
         var surface = Blend(Colors.White, rawColor, 0.14);
         var header = Blend(Colors.White, rawColor, 0.42);
         var toolbar = Blend(Color.FromRgb(249, 250, 251), rawColor, 0.20);
         var border = Blend(Color.FromRgb(209, 213, 219), rawColor, 0.50);
         var hover = Blend(surface, Colors.Black, 0.06);
         var accent = GetLuminance(rawColor) > 0.72 ? Blend(rawColor, Colors.Black, 0.25) : rawColor;
         var accentFg = GetLuminance(accent) > 0.62 ? Color.FromRgb(17, 24, 39) : Colors.White;

         return new NoteVisualPalette(
            AccentBrush: FrozenBrush(accent),
            AccentForegroundBrush: FrozenBrush(accentFg),
            HeaderBrush: FrozenBrush(header),
            HeaderForegroundBrush: FrozenBrush(Color.FromRgb(17, 24, 39)),
            SurfaceBrush: FrozenBrush(surface),
            ToolbarBrush: FrozenBrush(toolbar),
            BorderBrush: FrozenBrush(border),
            ForegroundBrush: FrozenBrush(Color.FromRgb(31, 41, 55)),
            SecondaryForegroundBrush: FrozenBrush(Color.FromRgb(75, 85, 99)),
            HoverBrush: FrozenBrush(hover));
      }
   }

   public static void ApplyTitleBarTheme(Window window, bool isDark)
   {
      if (!OperatingSystem.IsWindows()) return;

      var hwnd = new WindowInteropHelper(window).Handle;
      if (hwnd == IntPtr.Zero) return;

      var useDark = isDark ? 1 : 0;
      if (DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkMode, ref useDark, sizeof(int)) != 0)
      {
         DwmSetWindowAttribute(hwnd, DwmwaUseImmersiveDarkModeBefore20H1, ref useDark, sizeof(int));
      }
   }
}
