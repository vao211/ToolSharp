using System.Text.Json.Serialization;

namespace MyWidget.Models;

public class NoteModel
{
   public string Id { get; set; } = Guid.NewGuid().ToString("N");
   public string Title { get; set; } = string.Empty;

   /// <summary>
   /// Empty string means follow the App's primary Accent Color.
   /// Otherwise a hex color such as "#FACC15".
   /// </summary>
   public string ColorHex { get; set; } = "#FACC15";

   public NotePinMode PinMode { get; set; } = NotePinMode.Desktop;
   public bool IsOpen { get; set; } = true;
   public bool IsLocked { get; set; }
   public bool IsCollapsed { get; set; }

   public double BackgroundOpacity { get; set; } = 0.96;
   public double ContentOpacity { get; set; } = 1.0;

   /// <summary>
   /// Backward compatibility for older notes.json files that stored a single Opacity value.
   /// </summary>
   [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
   public double? Opacity
   {
      get => null;
      set
      {
         if (value.HasValue)
            BackgroundOpacity = Math.Clamp(value.Value, 0.0, 1.0);
      }
   }

   public double Left { get; set; } = double.NaN;
   public double Top { get; set; } = double.NaN;
   public double Width { get; set; } = 320;
   public double Height { get; set; } = 360;
   public double ExpandedHeight { get; set; } = 360;

   public DateTime CreatedAt { get; set; } = DateTime.Now;
   public DateTime UpdatedAt { get; set; } = DateTime.Now;

   public List<NoteBlockModel> Blocks { get; set; } = [];
}
