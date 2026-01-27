using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CapyCard.Services.ImportExport.Formats;
using CapyCard.Services.ImportExport.Models;

namespace CapyCard.Services.ImportExport
{
    /// <summary>
    /// Interface für den Import/Export-Service.
    /// </summary>
    public interface IImportExportService
    {
        /// <summary>
        /// Gibt alle verfügbaren Format-Handler zurück.
        /// </summary>
        IEnumerable<IFormatHandler> GetAvailableHandlers();

        /// <summary>
        /// Gibt alle unterstützten Dateiendungen für den Import zurück.
        /// </summary>
        IEnumerable<string> GetSupportedImportExtensions();

        /// <summary>
        /// Gibt alle verfügbaren Export-Formate zurück.
        /// </summary>
        IEnumerable<ExportFormat> GetAvailableExportFormats();

        /// <summary>
        /// Findet den passenden Handler für eine Datei.
        /// </summary>
        IFormatHandler? GetHandlerForFile(string fileName);

        /// <summary>
        /// Findet den Handler für ein bestimmtes Export-Format.
        /// </summary>
        IFormatHandler? GetHandlerForFormat(ExportFormat format);

        /// <summary>
        /// Analysiert eine Datei und gibt Vorschau-Informationen zurück.
        /// </summary>
        Task<ImportPreview> AnalyzeFileAsync(Stream stream, string fileName);

        /// <summary>
        /// Importiert eine Datei.
        /// </summary>
        Task<ImportResult> ImportAsync(Stream stream, string fileName, ImportOptions options);

        /// <summary>
        /// Exportiert in einen Stream.
        /// </summary>
        Task<ExportResult> ExportAsync(Stream stream, ExportOptions options);
    }
}
