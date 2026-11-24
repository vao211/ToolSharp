using System.Windows.Media;
using MyWidget.Services;

namespace MyWidget.ViewModels;

public class ColorSwatchItem : ViewModelBase
{
   private bool _isSelected;
   private SolidColorBrush _previewBrush = Brushes.DodgerBlue;

   public ColorSwatchItem(string colorHex, bool isFollowAppAccent = false)
   {
      ColorHex = colorHex;
      IsFollowAppAccent = isFollowAppAccent;
      RefreshBrush();
   }

   /// <summary>
   /// Empty string when IsFollowAppAccent is true.
   /// </summary>
   public string ColorHex { get; }

   public bool IsFollowAppAccent { get; }

   public SolidColorBrush PreviewBrush
   {
      get => _previewBrush;
      private set => SetProperty(ref _previewBrush, value);
   }

   public bool IsSelected
   {
      get => _isSelected;
      set => SetProperty(ref _isSelected, value);
   }

   public void RefreshBrush()
   {
      var hex = IsFollowAppAccent ? ThemeService.CurrentAccentHex : ColorHex;
      if (ThemeService.TryParseHexColor(hex, out var c))
      {
         var b = new SolidColorBrush(c);
         b.Freeze();
         PreviewBrush = b;
      }
   }
}
