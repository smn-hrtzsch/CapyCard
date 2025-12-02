using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CapyCard.Data;
using CapyCard.Models;
using Microsoft.EntityFrameworkCore;

namespace CapyCard.Services
{
    /// <summary>
    /// Service für Bildverwaltung (Upload, Speicherung, Abruf).
    /// </summary>
    public static class ImageService
    {
        private static readonly string[] SupportedMimeTypes = { "image/png", "image/jpeg", "image/gif", "image/webp" };

        /// <summary>
        /// Erstellt ein neues CardImage aus Binärdaten.
        /// </summary>
        /// <param name="imageData">Die Bilddaten als Byte-Array.</param>
        /// <param name="mimeType">Der MIME-Typ des Bildes.</param>
        /// <param name="fileName">Optionaler Dateiname.</param>
        /// <returns>Ein CardImage-Objekt (noch nicht gespeichert).</returns>
        public static CardImage CreateCardImage(byte[] imageData, string mimeType, string? fileName = null)
        {
            if (!IsSupportedMimeType(mimeType))
            {
                throw new ArgumentException($"MIME-Typ '{mimeType}' wird nicht unterstützt.");
            }

            return new CardImage
            {
                ImageId = GenerateImageId(),
                Base64Data = Convert.ToBase64String(imageData),
                MimeType = mimeType,
                FileName = fileName
            };
        }

        /// <summary>
        /// Erstellt ein neues CardImage aus einem Stream.
        /// </summary>
        public static async Task<CardImage> CreateCardImageAsync(Stream imageStream, string mimeType, string? fileName = null)
        {
            using var memoryStream = new MemoryStream();
            await imageStream.CopyToAsync(memoryStream);
            return CreateCardImage(memoryStream.ToArray(), mimeType, fileName);
        }

        /// <summary>
        /// Erstellt ein neues CardImage aus einem Dateipfad.
        /// </summary>
        public static async Task<CardImage> CreateCardImageFromFileAsync(string filePath)
        {
            var mimeType = GetMimeTypeFromExtension(Path.GetExtension(filePath));
            var imageData = await File.ReadAllBytesAsync(filePath);
            return CreateCardImage(imageData, mimeType, Path.GetFileName(filePath));
        }

        /// <summary>
        /// Speichert ein CardImage in der Datenbank.
        /// </summary>
        public static async Task SaveCardImageAsync(CardImage image, int cardId)
        {
            image.CardId = cardId;
            using var context = new FlashcardDbContext();
            context.CardImages.Add(image);
            await context.SaveChangesAsync();
        }

        /// <summary>
        /// Lädt ein CardImage anhand seiner ID.
        /// </summary>
        public static async Task<CardImage?> GetCardImageAsync(string imageId)
        {
            using var context = new FlashcardDbContext();
            return await context.CardImages
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.ImageId == imageId);
        }

        /// <summary>
        /// Lädt ein CardImage anhand seiner ID synchron.
        /// </summary>
        public static CardImage? GetCardImage(string imageId)
        {
            using var context = new FlashcardDbContext();
            return context.CardImages
                .AsNoTracking()
                .FirstOrDefault(i => i.ImageId == imageId);
        }

        /// <summary>
        /// Konvertiert Base64-Daten zu einem Byte-Array.
        /// </summary>
        public static byte[] GetImageBytes(CardImage image)
        {
            return Convert.FromBase64String(image.Base64Data);
        }

        /// <summary>
        /// Erstellt einen Data-URI für die Anzeige im UI.
        /// </summary>
        public static string GetDataUri(CardImage image)
        {
            return $"data:{image.MimeType};base64,{image.Base64Data}";
        }

        /// <summary>
        /// Löscht nicht mehr referenzierte Bilder einer Karte.
        /// </summary>
        public static async Task CleanupUnusedImagesAsync(int cardId, string[] usedImageIds)
        {
            using var context = new FlashcardDbContext();
            var unusedImages = await context.CardImages
                .Where(i => i.CardId == cardId && !usedImageIds.Contains(i.ImageId))
                .ToListAsync();

            if (unusedImages.Count > 0)
            {
                context.CardImages.RemoveRange(unusedImages);
                await context.SaveChangesAsync();
            }
        }

        /// <summary>
        /// Generiert eine eindeutige Bild-ID.
        /// </summary>
        private static string GenerateImageId()
        {
            return $"img-{Guid.NewGuid():N}";
        }

        /// <summary>
        /// Prüft ob der MIME-Typ unterstützt wird.
        /// </summary>
        private static bool IsSupportedMimeType(string mimeType)
        {
            return Array.IndexOf(SupportedMimeTypes, mimeType.ToLowerInvariant()) >= 0;
        }

        /// <summary>
        /// Ermittelt den MIME-Typ anhand der Dateiendung.
        /// </summary>
        private static string GetMimeTypeFromExtension(string extension)
        {
            return extension.ToLowerInvariant() switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                _ => "image/png" // Fallback
            };
        }
    }
}
