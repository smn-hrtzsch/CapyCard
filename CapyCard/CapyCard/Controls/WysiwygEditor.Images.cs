using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;

namespace CapyCard.Controls
{
    /// <summary>
    /// WysiwygEditor - Bildverarbeitung (Insert, Drag&Drop, Clipboard)
    /// </summary>
    public partial class WysiwygEditor
    {
        #region Drag & Drop / Clipboard Handling

        /// <summary>
        /// Behandelt DragOver-Event für Bild-Vorschau.
        /// </summary>
        private void OnDragOver(object? sender, DragEventArgs e)
        {
#pragma warning disable CS0618 // Obsolete warning unterdrücken
            if (e.Data.Contains(DataFormats.Files))
            {
                e.DragEffects = DragDropEffects.Copy;
            }
            else
            {
                e.DragEffects = DragDropEffects.None;
            }
#pragma warning restore CS0618
        }

        /// <summary>
        /// Behandelt Drop-Event für Bilder.
        /// </summary>
        private async void OnDrop(object? sender, DragEventArgs e)
        {
#pragma warning disable CS0618 // Obsolete warning unterdrücken
            if (!e.Data.Contains(DataFormats.Files)) return;
            
            var files = e.Data.GetFiles();
#pragma warning restore CS0618
            if (files == null) return;
            
            foreach (var file in files)
            {
                var path = file.Path.LocalPath;
                var extension = System.IO.Path.GetExtension(path).ToLowerInvariant();
                
                // Nur Bildformate akzeptieren
                if (extension is ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".webp")
                {
                    await InsertImageFromPathAsync(path);
                }
            }
        }

        /// <summary>
        /// Behandelt Einfügen aus der Zwischenablage.
        /// Gibt true zurück wenn ein Bild eingefügt wurde.
        /// </summary>
        private async Task<bool> HandleClipboardPasteAsync()
        {
            System.Diagnostics.Debug.WriteLine("[WysiwygEditor] HandleClipboardPasteAsync called.");

            // 1. Try Platform Specific Service (Mobile)
            if (CapyCard.Services.ClipboardService.Current != null)
            {
                System.Diagnostics.Debug.WriteLine("[WysiwygEditor] Using ClipboardService.Current.");
                if (await CapyCard.Services.ClipboardService.Current.HasImageAsync())
                {
                    System.Diagnostics.Debug.WriteLine("[WysiwygEditor] HasImageAsync returned true.");
                    try
                    {
                        using var stream = await CapyCard.Services.ClipboardService.Current.GetImageAsync();
                        if (stream != null)
                        {
                            System.Diagnostics.Debug.WriteLine($"[WysiwygEditor] Got stream from service. Length: {stream.Length}");
                            using var ms = new System.IO.MemoryStream();
                            await stream.CopyToAsync(ms);
                            var bytes = ms.ToArray();
                            if (bytes.Length > 0)
                            {
                                InsertImageFromBytes(bytes, DetectMimeType(bytes));
                                return true;
                            }
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine("[WysiwygEditor] Stream from service was null.");
                        }
                    }
                    catch (Exception ex) 
                    { 
                        System.Diagnostics.Debug.WriteLine($"[WysiwygEditor] Exception using service: {ex}");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[WysiwygEditor] HasImageAsync returned false.");
                }
            }

            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard == null)
            {
                System.Diagnostics.Debug.WriteLine("[WysiwygEditor] TopLevel Clipboard is null.");
                return false;
            }
            
            try
            {
#pragma warning disable CS0618 // Obsolete API - GetFormatsAsync/GetDataAsync
                var formats = await clipboard.GetFormatsAsync();
                System.Diagnostics.Debug.WriteLine($"[WysiwygEditor] Avalonia Formats: {string.Join(", ", formats)}");
                
                // Prüfe zuerst auf Dateien (funktioniert am zuverlässigsten)
                if (Array.Exists(formats, f => f == DataFormats.Files))
                {
                    var files = await clipboard.GetDataAsync(DataFormats.Files);
                    
                    if (files is IEnumerable<IStorageItem> storageFiles)
                    {
                        foreach (var file in storageFiles)
                        {
                            var path = file.Path.LocalPath;
                            var extension = System.IO.Path.GetExtension(path).ToLowerInvariant();
                            
                            if (extension is ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".webp" or ".tiff")
                            {
                                await InsertImageFromPathAsync(path);
                                return true;
                            }
                        }
                    }
                }
                
                // macOS: Prüfe ob Bildformate verfügbar sind und nutze pngpaste/osascript
                bool hasImageFormat = Array.Exists(formats, f => 
                    f.Contains("png", StringComparison.OrdinalIgnoreCase) ||
                    f.Contains("tiff", StringComparison.OrdinalIgnoreCase) ||
                    f.Contains("Bitmap", StringComparison.OrdinalIgnoreCase) ||
                    f.Contains("image", StringComparison.OrdinalIgnoreCase));
                
                if (hasImageFormat && OperatingSystem.IsMacOS())
                {
                    // Verwende Swift-Script um Bild aus Zwischenablage zu extrahieren
                    var tempPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"clipboard_{Guid.NewGuid()}.png");
                    
                    try
                    {
                        // Methode 1: Versuche mit swift das Bild zu speichern (zuverlässiger)
                        var swiftCode = $@"
import AppKit
import Foundation

if let image = NSPasteboard.general.readObjects(forClasses: [NSImage.self], options: nil)?.first as? NSImage,
   let tiffData = image.tiffRepresentation,
   let bitmap = NSBitmapImageRep(data: tiffData),
   let pngData = bitmap.representation(using: .png, properties: [:]) {{
    try? pngData.write(to: URL(fileURLWithPath: ""{tempPath}""))
    print(""success"")
}}
";
                        var psi = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "/usr/bin/swift",
                            Arguments = $"-e '{swiftCode.Replace("\n", " ").Replace("'", "\\'")}'",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        };
                        
                        using var process = System.Diagnostics.Process.Start(psi);
                        if (process != null)
                        {
                            var output = await process.StandardOutput.ReadToEndAsync();
                            await process.WaitForExitAsync();
                            
                            if (System.IO.File.Exists(tempPath))
                            {
                                var fileInfo = new System.IO.FileInfo(tempPath);
                                if (fileInfo.Length > 0)
                                {
                                    await InsertImageFromPathAsync(tempPath);
                                    
                                    // Temporäre Datei nach kurzer Verzögerung löschen
                                    _ = Task.Run(async () =>
                                    {
                                        await Task.Delay(1000);
                                        try { System.IO.File.Delete(tempPath); } catch { }
                                    });
                                    
                                    return true;
                                }
                            }
                        }
                    }
                    catch
                    {
                        // Swift fehlgeschlagen, versuche Fallback mit osascript
                    }
                    
                    // Methode 2: Fallback mit osascript
                    try
                    {
                        var psi2 = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "/bin/bash",
                            Arguments = $"-c \"osascript -e 'set png to (the clipboard as «class PNGf»)' -e 'set f to open for access POSIX file \\\"{tempPath}\\\" with write permission' -e 'write png to f' -e 'close access f'\"",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            CreateNoWindow = true
                        };
                        
                        using var process2 = System.Diagnostics.Process.Start(psi2);
                        if (process2 != null)
                        {
                            await process2.WaitForExitAsync();
                            
                            if (process2.ExitCode == 0 && System.IO.File.Exists(tempPath))
                            {
                                var fileInfo = new System.IO.FileInfo(tempPath);
                                if (fileInfo.Length > 0)
                                {
                                    await InsertImageFromPathAsync(tempPath);
                                    
                                    _ = Task.Run(async () =>
                                    {
                                        await Task.Delay(1000);
                                        try { System.IO.File.Delete(tempPath); } catch { }
                                    });
                                    
                                    return true;
                                }
                            }
                        }
                    }
                    catch
                    {
                        // osascript auch fehlgeschlagen
                    }
                    finally
                    {
                        // Aufräumen falls Datei existiert aber nicht genutzt wurde
                        if (System.IO.File.Exists(tempPath))
                        {
                            try { System.IO.File.Delete(tempPath); } catch { }
                        }
                    }
                }
                
                // Fallback: Versuche direkt aus Clipboard zu lesen (Windows/Linux)
                foreach (var format in formats)
                {
                    if (format.Contains("text", StringComparison.OrdinalIgnoreCase) ||
                        format.Contains("string", StringComparison.OrdinalIgnoreCase) ||
                        format.Contains("unicode", StringComparison.OrdinalIgnoreCase) ||
                        format.Contains("html", StringComparison.OrdinalIgnoreCase) ||
                        format.Contains("rtf", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    
                    try
                    {
                        var data = await clipboard.GetDataAsync(format);
                        
                        if (data is byte[] imageBytes && imageBytes.Length > 0 && IsImageData(imageBytes))
                        {
                            InsertImageFromBytes(imageBytes, DetectMimeType(imageBytes));
                            return true;
                        }
                        else if (data is System.IO.Stream stream && stream.CanRead)
                        {
                            using var ms = new System.IO.MemoryStream();
                            await stream.CopyToAsync(ms);
                            var bytes = ms.ToArray();
                            if (bytes.Length > 0 && IsImageData(bytes))
                            {
                                InsertImageFromBytes(bytes, DetectMimeType(bytes));
                                return true;
                            }
                        }
                    }
                    catch
                    {
                        // Format konnte nicht gelesen werden
                    }
                }
#pragma warning restore CS0618
            }
            catch
            {
                // Fehler beim Clipboard-Zugriff ignorieren
            }
            
            return false;
        }

        /// <summary>
        /// Prüft anhand der Magic Bytes ob es sich um Bilddaten handelt.
        /// </summary>
        private static bool IsImageData(byte[] data)
        {
            if (data.Length < 8) return false;
            
            // PNG: 89 50 4E 47 0D 0A 1A 0A
            if (data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47)
                return true;
            
            // JPEG: FF D8 FF
            if (data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
                return true;
            
            // GIF: 47 49 46 38
            if (data[0] == 0x47 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x38)
                return true;
            
            // BMP: 42 4D
            if (data[0] == 0x42 && data[1] == 0x4D)
                return true;
            
            // TIFF: 49 49 2A 00 oder 4D 4D 00 2A
            if ((data[0] == 0x49 && data[1] == 0x49 && data[2] == 0x2A && data[3] == 0x00) ||
                (data[0] == 0x4D && data[1] == 0x4D && data[2] == 0x00 && data[3] == 0x2A))
                return true;
            
            // WebP: 52 49 46 46 ... 57 45 42 50
            if (data[0] == 0x52 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x46 &&
                data.Length > 11 && data[8] == 0x57 && data[9] == 0x45 && data[10] == 0x42 && data[11] == 0x50)
                return true;
            
            return false;
        }

        /// <summary>
        /// Erkennt den MIME-Type anhand der Magic Bytes.
        /// </summary>
        private static string DetectMimeType(byte[] data)
        {
            if (data.Length < 4) return "image/png";
            
            if (data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47)
                return "image/png";
            
            if (data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
                return "image/jpeg";
            
            if (data[0] == 0x47 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x38)
                return "image/gif";
            
            if (data[0] == 0x42 && data[1] == 0x4D)
                return "image/bmp";
            
            if ((data[0] == 0x49 && data[1] == 0x49) || (data[0] == 0x4D && data[1] == 0x4D))
                return "image/tiff";
            
            if (data[0] == 0x52 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x46)
                return "image/webp";
            
            return "image/png";
        }

        #endregion

        #region Image Insertion

        /// <summary>
        /// Öffnet einen Datei-Dialog und fügt ein Bild ein.
        /// </summary>
        private async Task InsertImageAsync()
        {
            if ((OperatingSystem.IsIOS() || OperatingSystem.IsAndroid()) && CapyCard.Services.PhotoPickerService.Current != null)
            {
                try
                {
                    var stream = await CapyCard.Services.PhotoPickerService.Current.PickPhotoAsync();
                    if (stream != null)
                    {
                        using var ms = new System.IO.MemoryStream();
                        await stream.CopyToAsync(ms);
                        var bytes = ms.ToArray();
                        if (bytes.Length > 0)
                        {
                            InsertImageFromBytes(bytes, DetectMimeType(bytes));
                        }
                    }
                }
                catch
                {
                    // Ignore errors
                }
                return;
            }

            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel == null) return;

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Bild auswählen",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Bilder")
                    {
                        Patterns = new[] { "*.png", "*.jpg", "*.jpeg", "*.gif", "*.bmp", "*.webp" }
                    }
                }
            });

            if (files.Count > 0)
            {
                var file = files[0];
                await InsertImageFromPathAsync(file.Path.LocalPath);
            }
        }

        /// <summary>
        /// Fügt ein Bild von einem Dateipfad ein.
        /// Im Editor wird ein kurzer Platzhalter angezeigt, die echten Daten werden separat gespeichert.
        /// </summary>
        private async Task InsertImageFromPathAsync(string path)
        {
            try
            {
                // Bild als Base64 kodieren
                var imageBytes = await System.IO.File.ReadAllBytesAsync(path);
                var base64 = Convert.ToBase64String(imageBytes);
                
                // MIME-Typ bestimmen
                var extension = System.IO.Path.GetExtension(path).ToLowerInvariant();
                var mimeType = extension switch
                {
                    ".png" => "image/png",
                    ".jpg" or ".jpeg" => "image/jpeg",
                    ".gif" => "image/gif",
                    ".bmp" => "image/bmp",
                    ".webp" => "image/webp",
                    _ => "image/png"
                };
                
                // Speichere Base64-Daten im Dictionary
                var imageId = _nextImageId++;
                var dataUri = $"data:{mimeType};base64,{base64}";
                _imageDataStore[imageId] = dataUri;
                
                // Im Editor nur kurzen Platzhalter anzeigen
                // Format: ![Bild 1](Bild wird nach Fokuswechsel angezeigt)
                var placeholderMarkdown = $"![Bild {imageId}](Bild wird nach Fokuswechsel angezeigt)";
                InsertTextAtCursor(placeholderMarkdown);
            }
            catch
            {
                // Fehler beim Laden des Bildes ignorieren
            }
        }

        /// <summary>
        /// Fügt ein Bild direkt aus Bytes ein (für Zwischenablage).
        /// </summary>
        private void InsertImageFromBytes(byte[] imageBytes, string mimeType = "image/png")
        {
            try
            {
                var base64 = Convert.ToBase64String(imageBytes);
                
                // Speichere Base64-Daten im Dictionary
                var imageId = _nextImageId++;
                var dataUri = $"data:{mimeType};base64,{base64}";
                _imageDataStore[imageId] = dataUri;
                
                // Im Editor nur kurzen Platzhalter anzeigen
                var placeholderMarkdown = $"![Bild {imageId}](Bild wird nach Fokuswechsel angezeigt)";
                InsertTextAtCursor(placeholderMarkdown);
            }
            catch
            {
                // Fehler beim Einfügen des Bildes ignorieren
            }
        }

        #endregion

        #region Image Placeholder Conversion

        /// <summary>
        /// Konvertiert Base64-Bilder im Text zu kurzen Platzhaltern für den Editor.
        /// </summary>
        private string ConvertBase64ToPlaceholders(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            
            // Finde alle Base64-Bilder: ![...](data:image/...;base64,...)
            var base64ImageRegex = new Regex(@"!\[([^\]]*)\]\((data:image/[^;]+;base64,[^)]+)\)", RegexOptions.Compiled);
            
            return base64ImageRegex.Replace(text, match =>
            {
                var altText = match.Groups[1].Value;
                var dataUri = match.Groups[2].Value;
                
                // Speichere Base64-Daten und erstelle Platzhalter
                var imageId = _nextImageId++;
                _imageDataStore[imageId] = dataUri;
                
                return $"![Bild {imageId}](Bild wird nach Fokuswechsel angezeigt)";
            });
        }

        /// <summary>
        /// Konvertiert Platzhalter zurück zu Base64-Bildern für die Speicherung.
        /// </summary>
        private string ConvertPlaceholdersToBase64(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            
            // Regex für Platzhalter: ![Bild 123](Bild wird nach Fokuswechsel angezeigt)
            var placeholderRegex = new Regex(@"!\[Bild (\d+)\]\(Bild wird nach Fokuswechsel angezeigt\)", RegexOptions.Compiled);
            
            var result = placeholderRegex.Replace(text, match =>
            {
                if (int.TryParse(match.Groups[1].Value, out var imageId) && 
                    _imageDataStore.TryGetValue(imageId, out var dataUri))
                {
                    return $"![Bild]({dataUri})";
                }
                return match.Value; // Platzhalter ohne Daten belassen
            });
            
            return result;
        }

        #endregion
    }
}
