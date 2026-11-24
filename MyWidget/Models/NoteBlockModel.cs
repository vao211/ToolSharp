namespace MyWidget.Models;

public class NoteBlockModel
{
   public string Id { get; set; } = Guid.NewGuid().ToString("N");
   public NoteBlockType Type { get; set; } = NoteBlockType.Text;
   public string Text { get; set; } = string.Empty;
   public bool IsChecked { get; set; }

   // Text formatting options
   public bool IsBold { get; set; }
   public bool IsItalic { get; set; }
   public bool IsUnderline { get; set; }
   public bool IsStrikethrough { get; set; }
   public int FontSizeLevel { get; set; } // -1: Small, 0: Normal, 1: Large, 2: Heading

   // Image properties
   public string ImagePath { get; set; } = string.Empty;
   public bool IsAppResource { get; set; }
   public double ImageMaxHeight { get; set; } = 220;

   public NoteBlockModel Clone()
   {
      return new NoteBlockModel
      {
         Id = Guid.NewGuid().ToString("N"),
         Type = Type,
         Text = Text,
         IsChecked = IsChecked,
         IsBold = IsBold,
         IsItalic = IsItalic,
         IsUnderline = IsUnderline,
         IsStrikethrough = IsStrikethrough,
         FontSizeLevel = FontSizeLevel,
         ImagePath = ImagePath,
         IsAppResource = IsAppResource,
         ImageMaxHeight = ImageMaxHeight
      };
   }
}
