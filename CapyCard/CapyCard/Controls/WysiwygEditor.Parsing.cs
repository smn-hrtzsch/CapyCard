using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using CapyCard.Services;
using Material.Icons;
using Material.Icons.Avalonia;

namespace CapyCard.Controls
{
    /// <summary>
    /// WysiwygEditor - Markdown-Parsing und formatierte Anzeige.
    /// </summary>
    public partial class WysiwygEditor
    {
        private static readonly FontFamily FormulaFallbackFontFamily = new("STIX Two Math, Cambria Math, Times New Roman, Noto Serif");
        private static readonly FontFamily TableFontFamily = new("Menlo, Cascadia Mono, Consolas, Courier New");
        private static readonly IBrush TableBorderBrush = new SolidColorBrush(Color.FromArgb(100, 170, 170, 170));
        private static readonly IBrush TableHeaderBackgroundBrush = new SolidColorBrush(Color.FromArgb(45, 255, 255, 255));

        private enum InlineFormulaContext
        {
            Default,
            TableCell
        }

        #region Display Update

        /// <summary>
        /// Aktualisiert die formatierte Anzeige im TextBlock.
        /// </summary>
        private void UpdateFormattedDisplay()
        {
            var editorText = EditorTextBox.Text ?? string.Empty;

            // Watermark-Sichtbarkeit aktualisieren
            UpdateWatermarkVisibility();

            // Inlines initialisieren falls nötig
            if (FormattedDisplay.Inlines == null)
            {
                FormattedDisplay.Inlines = new InlineCollection();
            }

            // Inlines leeren
            FormattedDisplay.Inlines.Clear();

            // Wenn leer, nichts anzeigen
            if (string.IsNullOrEmpty(editorText))
            {
                return;
            }

            // Platzhalter durch echte Base64-Daten ersetzen für die Anzeige
            var displayMarkdown = ConvertPlaceholdersToBase64(editorText);
            var formulaColor = TryGetSolidColor(FormattedDisplay.Foreground);

            // Formatierte Inlines erstellen und hinzufügen
            var inlines = ParseMarkdownToInlines(displayMarkdown, configureImage: null, formulaColor);
            foreach (var inline in inlines)
            {
                FormattedDisplay.Inlines.Add(inline);
            }
        }

        #endregion

        #region Static Helper Methods

        /// <summary>
        /// Parst Markdown-Text und gibt formatierte Inlines zurück.
        /// </summary>
        public static InlineCollection ParseMarkdownToInlines(string markdown)
        {
            return ParseMarkdownToInlines(markdown, null, null);
        }

        /// <summary>
        /// Parst Markdown-Text und erlaubt optionale Bildkonfiguration (z.B. klickbar).
        /// </summary>
        public static InlineCollection ParseMarkdownToInlines(string markdown, Action<Image>? configureImage)
        {
            return ParseMarkdownToInlines(markdown, configureImage, null);
        }

        /// <summary>
        /// Parst Markdown-Text mit optionaler Formel-Farbe.
        /// </summary>
        public static InlineCollection ParseMarkdownToInlines(
            string markdown,
            Action<Image>? configureImage,
            Color? formulaColor)
        {
            var inlines = new InlineCollection();

            if (string.IsNullOrEmpty(markdown))
            {
                return inlines;
            }

            var document = MarkdownService.Parse(markdown);
            for (var index = 0; index < document.Blocks.Count; index++)
            {
                RenderBlock(inlines, document.Blocks[index], configureImage, formulaColor);

                if (index < document.Blocks.Count - 1)
                {
                    inlines.Add(new LineBreak());
                }
            }

            return inlines;
        }

        /// <summary>
        /// Entfernt Markdown-Formatierung und gibt reinen Text zurück.
        /// </summary>
        public static string StripMarkdown(string markdown)
        {
            return MarkdownService.StripMarkdown(markdown);
        }

        private static void RenderBlock(
            InlineCollection target,
            MarkdownService.MarkdownBlock block,
            Action<Image>? configureImage,
            Color? formulaColor)
        {
            switch (block)
            {
                case MarkdownService.MarkdownBlankLineBlock:
                    break;

                case MarkdownService.MarkdownParagraphBlock paragraphBlock:
                    AddInlineSegments(target, paragraphBlock.Inlines, configureImage, formulaColor);
                    break;

                case MarkdownService.MarkdownListBlock listBlock:
                    RenderListBlock(target, listBlock, configureImage, formulaColor);
                    break;

                case MarkdownService.MarkdownChecklistBlock checklistBlock:
                    RenderChecklistBlock(target, checklistBlock, configureImage, formulaColor);
                    break;

                case MarkdownService.MarkdownQuoteBlock quoteBlock:
                    RenderQuoteBlock(target, quoteBlock, configureImage, formulaColor);
                    break;

                case MarkdownService.MarkdownTableBlock tableBlock:
                    RenderTableBlock(target, tableBlock, configureImage, formulaColor);
                    break;

                case MarkdownService.MarkdownFormulaBlock formulaBlock:
                    RenderFormulaBlock(target, formulaBlock, formulaColor);
                    break;
            }
        }

        private static void RenderListBlock(
            InlineCollection target,
            MarkdownService.MarkdownListBlock listBlock,
            Action<Image>? configureImage,
            Color? formulaColor)
        {
            for (var index = 0; index < listBlock.Items.Count; index++)
            {
                var item = listBlock.Items[index];
                var indent = new string(' ', item.IndentLevel * 4);
                var prefix = listBlock.IsOrdered
                    ? $"{item.Number ?? index + 1}. "
                    : "• ";

                target.Add(new Run(indent + prefix));
                AddInlineSegments(target, item.Inlines, configureImage, formulaColor);

                if (index < listBlock.Items.Count - 1)
                {
                    target.Add(new LineBreak());
                }
            }
        }

        private static void RenderChecklistBlock(
            InlineCollection target,
            MarkdownService.MarkdownChecklistBlock checklistBlock,
            Action<Image>? configureImage,
            Color? formulaColor)
        {
            for (var index = 0; index < checklistBlock.Items.Count; index++)
            {
                var item = checklistBlock.Items[index];
                var indent = new string(' ', item.IndentLevel * 4);

                if (!string.IsNullOrEmpty(indent))
                {
                    target.Add(new Run(indent));
                }

                var markerIcon = new MaterialIcon
                {
                    Kind = item.IsChecked
                        ? MaterialIconKind.CheckboxMarked
                        : MaterialIconKind.CheckboxBlankOutline,
                    Width = 14,
                    Height = 14,
                    Margin = new Avalonia.Thickness(0, 0, 4, 0)
                };

                target.Add(new InlineUIContainer(markerIcon)
                {
                    BaselineAlignment = BaselineAlignment.Center
                });

                AddInlineSegments(target, item.Inlines, configureImage, formulaColor);

                if (index < checklistBlock.Items.Count - 1)
                {
                    target.Add(new LineBreak());
                }
            }
        }

        private static void RenderQuoteBlock(
            InlineCollection target,
            MarkdownService.MarkdownQuoteBlock quoteBlock,
            Action<Image>? configureImage,
            Color? formulaColor)
        {
            for (var index = 0; index < quoteBlock.Lines.Count; index++)
            {
                var line = quoteBlock.Lines[index];

                var contentText = new TextBlock
                {
                    TextWrapping = TextWrapping.Wrap,
                    FontStyle = FontStyle.Italic,
                    Foreground = new SolidColorBrush(Color.FromRgb(224, 224, 224))
                };
                contentText.Inlines ??= new InlineCollection();
                var quoteFormulaColor = TryGetSolidColor(contentText.Foreground) ?? formulaColor;
                AddInlineSegments(contentText.Inlines, line.Inlines, configureImage, quoteFormulaColor);

                var leftIndent = Math.Max(0, line.Level - 1) * 8;
                var quoteContainer = new Border
                {
                    Margin = new Avalonia.Thickness(leftIndent, 2, 0, 2),
                    Padding = new Avalonia.Thickness(10, 4, 2, 4),
                    BorderBrush = new SolidColorBrush(Color.FromArgb(170, 150, 150, 150)),
                    BorderThickness = new Avalonia.Thickness(3, 0, 0, 0),
                    Child = contentText
                };

                target.Add(new InlineUIContainer(quoteContainer));

                if (index < quoteBlock.Lines.Count - 1)
                {
                    target.Add(new LineBreak());
                }
            }
        }

        private static void RenderTableBlock(
            InlineCollection target,
            MarkdownService.MarkdownTableBlock tableBlock,
            Action<Image>? configureImage,
            Color? formulaColor)
        {
            var columnCount = Math.Max(
                tableBlock.Header.Count,
                tableBlock.Rows.Count == 0 ? 0 : tableBlock.Rows.Max(row => row.Count));

            if (columnCount == 0)
            {
                return;
            }

            var rowCount = 1 + tableBlock.Rows.Count;
            var grid = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Left
            };

            for (var column = 0; column < columnCount; column++)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            }

            for (var row = 0; row < rowCount; row++)
            {
                grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            }

            for (var column = 0; column < columnCount; column++)
            {
                var headerCellInlines = column < tableBlock.Header.Count
                    ? tableBlock.Header[column].Inlines
                    : Array.Empty<MarkdownService.MarkdownInline>();

                AddTableCell(
                    grid,
                    row: 0,
                    column,
                    headerCellInlines,
                    isHeader: true,
                    isLastRow: rowCount == 1,
                    isLastColumn: column == columnCount - 1,
                    configureImage,
                    formulaColor);
            }

            for (var rowIndex = 0; rowIndex < tableBlock.Rows.Count; rowIndex++)
            {
                var row = tableBlock.Rows[rowIndex];
                var visualRowIndex = rowIndex + 1;
                var isLastRow = visualRowIndex == rowCount - 1;

                for (var column = 0; column < columnCount; column++)
                {
                    var cellInlines = column < row.Count
                        ? row[column].Inlines
                        : Array.Empty<MarkdownService.MarkdownInline>();

                    AddTableCell(
                        grid,
                        visualRowIndex,
                        column,
                        cellInlines,
                        isHeader: false,
                        isLastRow,
                        isLastColumn: column == columnCount - 1,
                        configureImage,
                        formulaColor);
                }
            }

            var tableBorder = new Border
            {
                Margin = new Avalonia.Thickness(0, 4, 0, 4),
                BorderBrush = TableBorderBrush,
                BorderThickness = new Avalonia.Thickness(1),
                CornerRadius = new Avalonia.CornerRadius(6),
                ClipToBounds = true,
                Child = grid
            };

            target.Add(new InlineUIContainer(tableBorder));
        }

        private static void RenderFormulaBlock(
            InlineCollection target,
            MarkdownService.MarkdownFormulaBlock formulaBlock,
            Color? formulaColor)
        {
            if (string.IsNullOrWhiteSpace(formulaBlock.Content))
            {
                return;
            }

            var formulaControl = CreateFormulaControl(formulaBlock.Content, isInline: false, formulaColor);
            if (formulaControl == null)
            {
                target.Add(new Run("$$"));
                target.Add(new LineBreak());
                target.Add(new Run(formulaBlock.Content)
                {
                    FontFamily = FormulaFallbackFontFamily
                });
                target.Add(new LineBreak());
                target.Add(new Run("$$"));
                return;
            }

            target.Add(new InlineUIContainer(formulaControl)
            {
                BaselineAlignment = BaselineAlignment.Center
            });
        }

        private static void AddTableCell(
            Grid grid,
            int row,
            int column,
            IReadOnlyList<MarkdownService.MarkdownInline> inlines,
            bool isHeader,
            bool isLastRow,
            bool isLastColumn,
            Action<Image>? configureImage,
            Color? formulaColor)
        {
            var hasInlineFormula = inlines.Any(static inline => inline is MarkdownService.MarkdownFormulaInline);

            var textBlock = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                FontFamily = TableFontFamily,
                FontWeight = isHeader ? FontWeight.SemiBold : FontWeight.Normal,
                VerticalAlignment = VerticalAlignment.Center
            };

            if (hasInlineFormula)
            {
                textBlock.LineHeight = 28;
            }

            textBlock.Inlines ??= new InlineCollection();
            AddInlineSegments(
                textBlock.Inlines,
                inlines,
                configureImage,
                formulaColor,
                InlineFormulaContext.TableCell);

            var verticalPadding = hasInlineFormula ? 6 : 4;

            var cellBorder = new Border
            {
                Padding = new Avalonia.Thickness(8, verticalPadding),
                Background = isHeader ? TableHeaderBackgroundBrush : null,
                BorderBrush = TableBorderBrush,
                BorderThickness = new Avalonia.Thickness(
                    left: 0,
                    top: 0,
                    right: isLastColumn ? 0 : 1,
                    bottom: isLastRow ? 0 : 1),
                Child = textBlock
            };

            Grid.SetRow(cellBorder, row);
            Grid.SetColumn(cellBorder, column);
            grid.Children.Add(cellBorder);
        }

        private static void AddInlineSegments(
            InlineCollection target,
            IReadOnlyList<MarkdownService.MarkdownInline> segments,
            Action<Image>? configureImage,
            Color? formulaColor,
            InlineFormulaContext formulaContext = InlineFormulaContext.Default)
        {
            foreach (var segment in segments)
            {
                switch (segment)
                {
                    case MarkdownService.MarkdownTextInline textInline:
                        var run = new Run(textInline.Text);
                        if (textInline.IsBold)
                        {
                            run.FontWeight = FontWeight.Bold;
                        }

                        if (textInline.IsItalic)
                        {
                            run.FontStyle = FontStyle.Italic;
                        }

                        if (textInline.IsUnderline)
                        {
                            run.TextDecorations = TextDecorations.Underline;
                        }

                        if (textInline.IsHighlight)
                        {
                            // Orange-Gelb mit guter Lesbarkeit in Dark und Light Mode.
                            run.Background = new SolidColorBrush(Color.FromRgb(255, 193, 7));
                            run.Foreground = Brushes.Black;
                        }

                        target.Add(run);
                        break;

                    case MarkdownService.MarkdownFormulaInline formulaInline:
                        var inlineFormulaControl = CreateFormulaControl(
                            formulaInline.Content,
                            isInline: true,
                            formulaColor,
                            formulaContext == InlineFormulaContext.TableCell);
                        if (inlineFormulaControl != null)
                        {
                            target.Add(new InlineUIContainer(inlineFormulaControl)
                            {
                                BaselineAlignment = formulaContext == InlineFormulaContext.TableCell
                                    ? BaselineAlignment.Center
                                    : BaselineAlignment.TextBottom
                            });
                        }
                        else
                        {
                            target.Add(new Run("$" + formulaInline.Content + "$")
                            {
                                FontFamily = FormulaFallbackFontFamily
                            });
                        }
                        break;

                    case MarkdownService.MarkdownImageInline imageInline:
                        AddImageSegment(target, imageInline, configureImage);
                        break;
                }
            }
        }

        private static Control? CreateFormulaControl(
            string latex,
            bool isInline,
            Color? formulaColor,
            bool isInsideTableCell = false)
        {
            if (string.IsNullOrWhiteSpace(latex))
            {
                return null;
            }

            var renderedFormula = FormulaRenderingService.RenderFormula(
                latex.Trim(),
                isInline,
                formulaColor ?? ResolveFormulaTextColor());

            if (renderedFormula == null)
            {
                return null;
            }

            var image = new Image
            {
                Source = renderedFormula.Bitmap,
                Width = renderedFormula.Width,
                Height = renderedFormula.Height,
                VerticalAlignment = isInsideTableCell ? VerticalAlignment.Center : VerticalAlignment.Bottom,
                HorizontalAlignment = HorizontalAlignment.Left
            };

            RenderOptions.SetBitmapInterpolationMode(image, BitmapInterpolationMode.HighQuality);

            if (isInline)
            {
                var maxInlineHeight = isInsideTableCell ? 22d : 24d;
                if (image.Height > maxInlineHeight)
                {
                    var scale = maxInlineHeight / image.Height;
                    image.Width *= scale;
                    image.Height = maxInlineHeight;
                }

                image.Margin = isInsideTableCell
                    ? new Avalonia.Thickness(0, 1, 0, 1)
                    : new Avalonia.Thickness(1, 1, 1, -1);
                image.Stretch = Stretch.Fill;
                return image;
            }

            image.Margin = new Avalonia.Thickness(0, 4, 0, 4);
            image.Stretch = Stretch.Fill;

            var viewbox = new Viewbox
            {
                Stretch = Stretch.Uniform,
                StretchDirection = StretchDirection.DownOnly,
                HorizontalAlignment = HorizontalAlignment.Left,
                Child = image
            };

            return new Border
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                ClipToBounds = false,
                Child = viewbox
            };
        }

        private static Color? TryGetSolidColor(IBrush? brush)
        {
            return brush is ISolidColorBrush solidBrush
                ? solidBrush.Color
                : null;
        }

        private static Color ResolveFormulaTextColor()
        {
            var app = Application.Current;
            var theme = app?.ActualThemeVariant;

            if (app?.Resources.TryGetResource("TextControlForeground", theme, out var textBrushResource) == true)
            {
                if (textBrushResource is ISolidColorBrush solidBrush)
                {
                    return solidBrush.Color;
                }
            }

            if (app?.Resources.TryGetResource("SystemBaseHighColor", theme, out var baseColorResource) == true)
            {
                if (baseColorResource is Color color)
                {
                    return color;
                }

                if (baseColorResource is ISolidColorBrush colorBrush)
                {
                    return colorBrush.Color;
                }
            }

            if (theme == ThemeVariant.Dark)
            {
                return Color.FromRgb(236, 236, 236);
            }

            if (theme == ThemeVariant.Light)
            {
                return Color.FromRgb(28, 28, 28);
            }

            return Color.FromRgb(236, 236, 236);
        }

        private static void AddImageSegment(
            InlineCollection target,
            MarkdownService.MarkdownImageInline imageInline,
            Action<Image>? configureImage)
        {
            if (string.IsNullOrWhiteSpace(imageInline.Source))
            {
                return;
            }

            try
            {
                var image = new Image
                {
                    MaxWidth = 300,
                    MaxHeight = 200,
                    Stretch = Stretch.Uniform,
                    Margin = new Avalonia.Thickness(2)
                };

                if (imageInline.Source.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                {
                    var bitmap = LoadImageFromDataUri(imageInline.Source);
                    if (bitmap == null)
                    {
                        target.Add(CreateImageErrorRun("[Bild konnte nicht geladen werden]"));
                        return;
                    }

                    image.Source = bitmap;
                }
                else if (System.IO.File.Exists(imageInline.Source))
                {
                    image.Source = new Bitmap(imageInline.Source);
                }
                else
                {
                    target.Add(CreateImageErrorRun("[Bild nicht gefunden]"));
                    return;
                }

                configureImage?.Invoke(image);
                target.Add(new InlineUIContainer(image));
            }
            catch
            {
                target.Add(CreateImageErrorRun("[Bild nicht gefunden]"));
            }
        }

        private static Run CreateImageErrorRun(string text)
        {
            return new Run(text)
            {
                Foreground = Brushes.Gray,
                FontStyle = FontStyle.Italic
            };
        }

        /// <summary>
        /// Lädt ein Bild aus einer Base64-Data-URI.
        /// </summary>
        private static Bitmap? LoadImageFromDataUri(string dataUri)
        {
            try
            {
                // Format: data:image/png;base64,xxxxx
                var match = Regex.Match(dataUri.Trim(), @"^data:image/[^;]+;base64,(.+)$");
                if (!match.Success)
                {
                    return null;
                }

                var base64Data = match.Groups[1].Value.Trim();
                if (base64Data.EndsWith(")", StringComparison.Ordinal))
                {
                    base64Data = base64Data.Substring(0, base64Data.Length - 1);
                }

                var imageBytes = Convert.FromBase64String(base64Data);
                using var stream = new System.IO.MemoryStream(imageBytes);
                return new Bitmap(stream);
            }
            catch
            {
                return null;
            }
        }

        #endregion
    }
}
