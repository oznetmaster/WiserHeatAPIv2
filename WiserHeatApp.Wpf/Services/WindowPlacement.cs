using System;
using System.IO;
using System.Text.Json;
using System.Windows;

namespace WiserHeatApp.Wpf.Services;

internal static class WindowPlacement
{
 private sealed class Bounds
 {
 public double Left { get; set; }
 public double Top { get; set; }
 public double Width { get; set; }
 public double Height { get; set; }
 public bool IsMaximized { get; set; }
 }

 private static string FilePath => Path.Combine(
 Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
 "WiserHeatApp", "window.bounds.json");

 public static void Restore(Window window)
 {
 try
 {
 var path = FilePath;
 if (!File.Exists(path)) return;
 var json = File.ReadAllText(path);
 var b = JsonSerializer.Deserialize<Bounds>(json);
 if (b == null) return;

 // Ensure within virtual screen bounds
 var vLeft = SystemParameters.VirtualScreenLeft;
 var vTop = SystemParameters.VirtualScreenTop;
 var vWidth = SystemParameters.VirtualScreenWidth;
 var vHeight = SystemParameters.VirtualScreenHeight;

 double left = b.Left;
 double top = b.Top;
 double width = Math.Max(300, b.Width);
 double height = Math.Max(200, b.Height);

 if (left < vLeft || left > vLeft + vWidth -50) left = vLeft +100;
 if (top < vTop || top > vTop + vHeight -50) top = vTop +100;

 window.Left = left;
 window.Top = top;
 window.Width = width;
 window.Height = height;

 if (b.IsMaximized)
 {
 // Set after initial layout
 window.Loaded += (_, __) => window.WindowState = WindowState.Maximized;
 }
 }
 catch
 {
 // ignore
 }
 }

 public static void Save(Window window)
 {
 try
 {
 Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
 Rect r = window.RestoreBounds;
 var b = new Bounds
 {
 Left = r.Left,
 Top = r.Top,
 Width = Math.Max(300, r.Width),
 Height = Math.Max(200, r.Height),
 IsMaximized = window.WindowState == WindowState.Maximized
 };
 var json = JsonSerializer.Serialize(b, new JsonSerializerOptions { WriteIndented = true });
 File.WriteAllText(FilePath, json);
 }
 catch
 {
 // ignore
 }
 }
}
