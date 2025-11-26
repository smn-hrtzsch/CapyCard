using CapyCard.Models;
using CapyCard.Services.Pdf;
using QuestPDF.Fluent;
using System.Collections.Generic;
using System.IO;

namespace CapyCard.Services
{
    /// <summary>
    /// Isoliert die Logik zur PDF-Erstellung von den ViewModels.
    /// </summary>
    public static class PdfGenerationService
    {
        /// <summary>
        /// Generiert eine PDF-Datei aus der gegebenen Kartenliste am Zielpfad.
        /// </summary>
        public static void GeneratePdf(string filePath, List<Card> cards, int columnCount)
        {
            var document = new QuestPdfDocument(cards, columnCount);
            document.GeneratePdf(filePath);
        }

        /// <summary>
        /// Generiert eine PDF-Datei in den angegebenen Stream.
        /// </summary>
        public static void GeneratePdf(Stream stream, List<Card> cards, int columnCount)
        {
            var document = new QuestPdfDocument(cards, columnCount);
            
            // QuestPDF benötigt einen Stream, der "Seek" unterstützt.
            // Der Stream vom Android StorageProvider unterstützt das oft nicht.
            // Daher generieren wir erst in einen MemoryStream und kopieren dann.
            if (!stream.CanSeek)
            {
                using (var ms = new MemoryStream())
                {
                    document.GeneratePdf(ms);
                    ms.Position = 0;
                    ms.CopyTo(stream);
                    stream.Flush(); // Ensure all data is written
                }
            }
            else
            {
                document.GeneratePdf(stream);
                stream.Flush();
            }
        }
    }
}