using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using MyWidget.Models;
using MyWidget.Services;

namespace MyWidget.ViewModels;

public class NoteBlockViewModel : ViewModelBase
{
   private BitmapImage? _imageSource;
   private bool _isImageMissing;
   private bool _shouldFocusOnLoad;

   public NoteBlockViewModel(NoteBlockModel model, NoteViewModel parentNote)
   {
      Model = model;
      ParentNote = parentNote;

      ToggleBoldCommand = new RelayCommand(() => IsBold = !IsBold);
      ToggleItalicCommand = new RelayCommand(() => IsItalic = !IsItalic);
      ToggleUnderlineCommand = new RelayCommand(() => IsUnderline = !IsUnderline);
      ToggleStrikethroughCommand = new RelayCommand(() => IsStrikethrough = !IsStrikethrough);
      CycleFontSizeCommand = new RelayCommand(CycleFontSize);
      CycleImageSizeCommand = new RelayCommand(CycleImageSize);
      CopyImageToAppDataCommand = new RelayCommand(CopyImageToAppData);
      OpenImageExternallyCommand = new RelayCommand(OpenImageExternally);
      MoveUpCommand = new RelayCommand(() => ParentNote.MoveBlock(this, -1));
      MoveDownCommand = new RelayCommand(() => ParentNote.MoveBlock(this, +1));
      DeleteBlockCommand = new RelayCommand(() => ParentNote.RemoveBlock(this));

      if (Model.Type == NoteBlockType.Image)
         ReloadImage();
   }

   public NoteBlockModel Model { get; }
   public NoteViewModel ParentNote { get; }

   public string Id => Model.Id;
   public NoteBlockType Type => Model.Type;

   public bool IsTextType => Type == NoteBlockType.Text;
   public bool IsCheckBoxType => Type == NoteBlockType.CheckBox;
   public bool IsImageType => Type == NoteBlockType.Image;
   public bool IsQuoteType => Type == NoteBlockType.Quote;
   public bool IsDividerType => Type == NoteBlockType.Divider;

   public bool ShouldFocusOnLoad
   {
      get => _shouldFocusOnLoad;
      set => SetProperty(ref _shouldFocusOnLoad, value);
   }

   public string Text
   {
      get => Model.Text;
      set
      {
         if (Model.Text == value) return;
         Model.Text = value;
         OnPropertyChanged();
         ParentNote.NotifyContentModified();
      }
   }

   public bool IsChecked
   {
      get => Model.IsChecked;
      set
      {
         if (Model.IsChecked == value) return;
         Model.IsChecked = value;
         OnPropertyChanged();
         OnPropertyChanged(nameof(TextDecorations));
         OnPropertyChanged(nameof(CheckTextOpacity));
         ParentNote.NotifyContentModified();
      }
   }

   public double CheckTextOpacity => IsChecked ? 0.58 : 1.0;

   public bool IsBold
   {
      get => Model.IsBold;
      set
      {
         if (Model.IsBold == value) return;
         Model.IsBold = value;
         OnPropertyChanged();
         OnPropertyChanged(nameof(FontWeight));
         ParentNote.NotifyContentModified();
      }
   }

   public bool IsItalic
   {
      get => Model.IsItalic;
      set
      {
         if (Model.IsItalic == value) return;
         Model.IsItalic = value;
         OnPropertyChanged();
         OnPropertyChanged(nameof(FontStyle));
         ParentNote.NotifyContentModified();
      }
   }

   public bool IsUnderline
   {
      get => Model.IsUnderline;
      set
      {
         if (Model.IsUnderline == value) return;
         Model.IsUnderline = value;
         OnPropertyChanged();
         OnPropertyChanged(nameof(TextDecorations));
         ParentNote.NotifyContentModified();
      }
   }

   public bool IsStrikethrough
   {
      get => Model.IsStrikethrough;
      set
      {
         if (Model.IsStrikethrough == value) return;
         Model.IsStrikethrough = value;
         OnPropertyChanged();
         OnPropertyChanged(nameof(TextDecorations));
         ParentNote.NotifyContentModified();
      }
   }

   public int FontSizeLevel
   {
      get => Model.FontSizeLevel;
      set
      {
         if (Model.FontSizeLevel == value) return;
         Model.FontSizeLevel = value;
         OnPropertyChanged();
         OnPropertyChanged(nameof(FontSize));
         OnPropertyChanged(nameof(FontSizeBadge));
         ParentNote.NotifyContentModified();
      }
   }

   public FontWeight FontWeight => IsBold ? FontWeights.SemiBold : FontWeights.Normal;
   public FontStyle FontStyle => IsItalic ? FontStyles.Italic : FontStyles.Normal;

   public double FontSize => FontSizeLevel switch
   {
      -1 => 12.0,
      1 => 17.0,
      2 => 21.0,
      _ => 14.0
   };

   public string FontSizeBadge => FontSizeLevel switch
   {
      -1 => "S",
      1 => "L",
      2 => "H",
      _ => "M"
   };

   public TextDecorationCollection? TextDecorations
   {
      get
      {
         var strike = IsStrikethrough || (IsCheckBoxType && IsChecked);
         if (!IsUnderline && !strike)
            return null;

         var col = new TextDecorationCollection();
         if (IsUnderline)
         {
            foreach (var d in System.Windows.TextDecorations.Underline)
               col.Add(d);
         }
         if (strike)
         {
            foreach (var d in System.Windows.TextDecorations.Strikethrough)
               col.Add(d);
         }
         col.Freeze();
         return col;
      }
   }

   public string FullImagePath => DataStorageService.Instance.ResolveImageFullPath(Model.ImagePath, Model.IsAppResource);

   public bool IsAppResource => Model.IsAppResource;
   public bool IsExternalImage => IsImageType && !Model.IsAppResource && !IsImageMissing;

   public BitmapImage? ImageSource
   {
      get => _imageSource;
      private set => SetProperty(ref _imageSource, value);
   }

   public bool IsImageMissing
   {
      get => _isImageMissing;
      private set
      {
         if (SetProperty(ref _isImageMissing, value))
            OnPropertyChanged(nameof(IsExternalImage));
      }
   }

   public double ImageMaxHeight
   {
      get => Model.ImageMaxHeight <= 40 ? 220 : Model.ImageMaxHeight;
      set
      {
         if (Math.Abs(Model.ImageMaxHeight - value) < 0.5) return;
         Model.ImageMaxHeight = value;
         OnPropertyChanged();
         ParentNote.NotifyContentModified();
      }
   }

   public ICommand ToggleBoldCommand { get; }
   public ICommand ToggleItalicCommand { get; }
   public ICommand ToggleUnderlineCommand { get; }
   public ICommand ToggleStrikethroughCommand { get; }
   public ICommand CycleFontSizeCommand { get; }
   public ICommand CycleImageSizeCommand { get; }
   public ICommand CopyImageToAppDataCommand { get; }
   public ICommand OpenImageExternallyCommand { get; }
   public ICommand MoveUpCommand { get; }
   public ICommand MoveDownCommand { get; }
   public ICommand DeleteBlockCommand { get; }

   public void ReloadImage()
   {
      if (Type != NoteBlockType.Image) return;

      var fullPath = FullImagePath;
      if (string.IsNullOrWhiteSpace(fullPath) || !File.Exists(fullPath))
      {
         ImageSource = null;
         IsImageMissing = true;
      }
      else
      {
         var bmp = DataStorageService.Instance.LoadBitmapSafe(fullPath, decodePixelWidth: 1000);
         ImageSource = bmp;
         IsImageMissing = bmp is null;
      }

      OnPropertyChanged(nameof(FullImagePath));
      OnPropertyChanged(nameof(IsAppResource));
      OnPropertyChanged(nameof(IsExternalImage));
   }

   private void CycleFontSize()
   {
      FontSizeLevel = FontSizeLevel switch
      {
         0 => 1,
         1 => 2,
         2 => -1,
         _ => 0
      };
   }

   private void CycleImageSize()
   {
      ImageMaxHeight = ImageMaxHeight switch
      {
         <= 165 => 240,
         <= 255 => 380,
         <= 400 => 620,
         _ => 150
      };
   }

   private void CopyImageToAppData()
   {
      var full = FullImagePath;
      if (string.IsNullOrWhiteSpace(full) || !File.Exists(full)) return;

      var (stored, isAppRes) = DataStorageService.Instance.ImportImageFile(full, copyToAppData: true);
      if (isAppRes)
      {
         Model.ImagePath = stored;
         Model.IsAppResource = true;
         ReloadImage();
         ParentNote.NotifyContentModified();
      }
   }

   private void OpenImageExternally()
   {
      var full = FullImagePath;
      if (string.IsNullOrWhiteSpace(full) || !File.Exists(full)) return;

      try
      {
         Process.Start(new ProcessStartInfo
         {
            FileName = full,
            UseShellExecute = true
         });
      }
      catch
      {
      }
   }
}
