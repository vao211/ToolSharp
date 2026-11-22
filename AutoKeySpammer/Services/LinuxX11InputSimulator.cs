using System;
using System.Runtime.InteropServices;

namespace AutoKeySpammer.Services;

public class LinuxX11InputSimulator : IInputSimulator
{
   public void ClickKey(string keyName)
   {
      var display = XOpenDisplay(null);
      if (display == IntPtr.Zero)
      {
         Console.WriteLine(
            "[LinuxX11] LỖI: Không thể mở X11 Display. Nếu bạn đang dùng Wayland, XTest không hoạt động!");
         return;
      }

      var formattedKey = keyName.ToLower() switch
      {
         "space" => "space",
         "enter" or "return" => "return",
         "escape" => "escape",
         _ => keyName.ToLower()
      };

      var keysym = XStringToKeysym(formattedKey);
      if (keysym == 0 && formattedKey.Length == 1) keysym = XStringToKeysym(formattedKey);

      if (keysym != 0)
      {
         var keycode = XKeysymToKeycode(display, keysym);
         if (keycode != 0)
         {
            XTestFakeKeyEvent(display, keycode, true, 0);
            XTestFakeKeyEvent(display, keycode, false, 0);
            XFlush(display);
         }
         else
         {
            Console.WriteLine($"[LinuxX11] Không tìm thấy Keycode cho phím: {keyName}");
         }
      }
      else
      {
         Console.WriteLine($"[LinuxX11] Không chuyển đổi được Keysym cho phím: {keyName}");
      }

      XCloseDisplay(display);
   }

   [DllImport("libX11.so.6")]
   private static extern IntPtr XOpenDisplay(string? display_name);

   [DllImport("libX11.so.6")]
   private static extern int XCloseDisplay(IntPtr display);

   [DllImport("libX11.so.6")]
   private static extern ulong XStringToKeysym(string string_key);

   [DllImport("libX11.so.6")]
   private static extern uint XKeysymToKeycode(IntPtr display, ulong keysym);

   [DllImport("libXtst.so.6")]
   private static extern int XTestFakeKeyEvent(IntPtr display, uint keycode, bool is_press, uint delay);

   [DllImport("libX11.so.6")]
   private static extern int XFlush(IntPtr display);
}