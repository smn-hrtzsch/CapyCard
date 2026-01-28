using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CapyCard.Services.ImportExport.Formats;
using CapyCard.Services.ImportExport.Models;

namespace CapyCard.Services.ImportExport
{
    /// <summary>
    /// Koordiniert Import/Export-Operationen und delegiert an Format-Handler.
    /// </summary>
    public class ImportExportService : IImportExportService
    {
        private readonly List<IFormatHandler> _handlers;

        public ImportExportService()
        {
            _handlers = new List<IFormatHandler>
            {
                new CapyCardFormatHandler(),
                new CsvFormatHandler(),
                new AnkiFormatHandler(),
                new JsonFormatHandler(),
            };
        }

        /// <inheritdoc/>
        public IEnumerable<IFormatHandler> GetAvailableHandlers()
        {
            return _handlers.Where(h => h.IsAvailable);
        }

        /// <inheritdoc/>
        public IEnumerable<string> GetSupportedImportExtensions()
        {
            return GetAvailableHandlers()
                .SelectMany(h => h.SupportedExtensions)
                .Distinct();
        }

        /// <inheritdoc/>
        public IEnumerable<ExportFormat> GetAvailableExportFormats()
        {
            var formats = new List<ExportFormat> { ExportFormat.CapyCard, ExportFormat.Csv };

#if !BROWSER
            formats.Add(ExportFormat.Anki);
#endif

            return formats;
        }

        /// <inheritdoc/>
        public IFormatHandler? GetHandlerForFile(string fileName)
        {
            var extension = Path.GetExtension(fileName)?.ToLowerInvariant();
            if (string.IsNullOrEmpty(extension))
                return null;

            return GetAvailableHandlers()
                .FirstOrDefault(h => h.SupportedExtensions.Contains(extension));
        }

        /// <inheritdoc/>
        public IFormatHandler? GetHandlerForFormat(ExportFormat format)
        {
            return format switch
            {
                ExportFormat.CapyCard => _handlers.OfType<CapyCardFormatHandler>().FirstOrDefault(),
                ExportFormat.Csv => _handlers.OfType<CsvFormatHandler>().FirstOrDefault(),
                ExportFormat.Anki => _handlers.OfType<AnkiFormatHandler>().FirstOrDefault(),
                _ => null
            };
        }

        /// <inheritdoc/>
        public async Task<ImportPreview> AnalyzeFileAsync(Stream stream, string fileName)
        {
            var handler = GetHandlerForFile(fileName);
            if (handler == null)
            {
                return ImportPreview.Failed($"Unbekanntes Dateiformat: {Path.GetExtension(fileName)}");
            }

            try
            {
                return await handler.AnalyzeAsync(stream, fileName);
            }
            catch (Exception ex)
            {
                return ImportPreview.Failed($"Fehler beim Analysieren: {ex.Message}");
            }
        }

        /// <inheritdoc/>
        public async Task<ImportResult> ImportAsync(Stream stream, string fileName, ImportOptions options)
        {
            var handler = GetHandlerForFile(fileName);
            if (handler == null)
            {
                return ImportResult.Failed($"Unbekanntes Dateiformat: {Path.GetExtension(fileName)}");
            }

            try
            {
                return await handler.ImportAsync(stream, fileName, options);
            }
            catch (Exception ex)
            {
                return ImportResult.Failed($"Fehler beim Import: {ex.Message}");
            }
        }

        /// <inheritdoc/>
        public async Task<ExportResult> ExportAsync(Stream stream, ExportOptions options)
        {
            var handler = GetHandlerForFormat(options.Format);
            if (handler == null)
            {
                return ExportResult.Failed($"Export-Format nicht verfügbar: {options.Format}");
            }

            if (!handler.IsAvailable)
            {
                return ExportResult.Failed($"{handler.FormatName} ist auf dieser Plattform nicht verfügbar.");
            }

            try
            {
                return await handler.ExportAsync(stream, options);
            }
            catch (Exception ex)
            {
                return ExportResult.Failed($"Fehler beim Export: {ex.Message}");
            }
        }
    }
}
