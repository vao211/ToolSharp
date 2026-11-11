using System;
using System.Runtime.InteropServices;

namespace AutoKeySpammer.Services;

public class WindowsInputSimulator : IInputSimulator
{
   [DllImport("user32.dll")]
   private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

   [DllImport("user32.dll")]
   private static extern uint MapVirtualKey(uint uCode, uint uMapType);

   private const uint KEYEVENTF_KEYUP = 0x0002;

   public void ClickKey(string keyName)
   {
      byte vk = GetVirtualKeyCode(keyName);
      if (vk != 0)
      {
         byte scanCode = (byte)MapVirtualKey(vk, 0);

         keybd_event(vk, scanCode, 0, 0);
         keybd_event(vk, scanCode, KEYEVENTF_KEYUP, 0);
      }
      else
      {
         Console.WriteLine($"[WindowsInput] Không nhận diện được mã phím cho: {keyName}");
      }
   }

   private byte GetVirtualKeyCode(string key)
   {
      if (string.IsNullOrEmpty(key)) return 0;

      if (key.Length == 1)
      {
         char c = char.ToUpper(key[0]);
         if ((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))
         {
            return (byte)c;
         }
      }

      return key.ToUpper() switch
      {
         "SPACE" => 0x20,
         "ENTER" or "RETURN" => 0x0D,
         "ESCAPE" => 0x1B,
         "TAB" => 0x09,
         "BACKSPACE" => 0x08,
         "SHIFT" => 0x10,
         "CTRL" or "CONTROL" => 0x11,
         "ALT" => 0x12,
         "UP" => 0x26,
         "DOWN" => 0x28,
         "LEFT" => 0x25,
         "RIGHT" => 0x27,
         _ => 0
      };
   }
}