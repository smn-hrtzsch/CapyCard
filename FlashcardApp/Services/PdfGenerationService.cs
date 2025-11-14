using FlashcardApp.Models;
using FlashcardApp.Services.Pdf;
using QuestPDF.Fluent;
using System.Collections.Generic;

namespace FlashcardApp.Services
{
    /// <summary>
    /// Isoliert die Logik zur PDF-Erstellung von den ViewModels.
    /// </summary>
    public static class PdfGenerationService
    {
        /// <summary>
        /// Generiert eine PDF-Datei aus der gegebenen Kartenliste am Zielpfad.
        /// </summary>
        public static void GeneratePdf(string filePath, List<Card> cards)
        {
            // Erstellt eine Instanz unseres QuestPDF-Dokument-Layouts
            var document = new QuestPdfDocument(cards);
            
            // Generiert die PDF-Datei und speichert sie.
            // (Wirft eine Ausnahme, wenn die Datei gesperrt ist)
            document.GeneratePdf(filePath);
        }
    }
}