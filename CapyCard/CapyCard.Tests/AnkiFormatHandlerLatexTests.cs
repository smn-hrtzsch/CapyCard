using System.Reflection;
using CapyCard.Services.ImportExport.Formats;

namespace CapyCard.Tests
{
    public class AnkiFormatHandlerLatexTests
    {
        [Fact]
        public void ConvertMarkdownToAnki_UsesMathJaxDelimitersForLatex()
        {
            var handler = new AnkiFormatHandler();
            var method = typeof(AnkiFormatHandler).GetMethod("ConvertMarkdownToAnki", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(method);

            var markdown = "Inline $\\frac{a}{b}$\n\n$$\n\\sum_{i=0}^{n} i = \\frac{n(n+1)}{2}\n$$";
            var mediaFiles = new Dictionary<string, string>();
            var tempDir = Path.Combine(Path.GetTempPath(), "capycard_anki_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var html = Assert.IsType<string>(method!.Invoke(handler, new object[] { markdown, tempDir, mediaFiles }));

                Assert.Contains("\\(\\frac{a}{b}\\)", html);
                Assert.Contains("\\[", html);
                Assert.Contains("\\sum_{i=0}^{n} i = \\frac{n(n+1)}{2}", html);
                Assert.Contains("\\]", html);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, recursive: true);
                }
            }
        }

        [Fact]
        public void ConvertAnkiToMarkdown_ConvertsMathJaxToMarkdownLatex()
        {
            var method = typeof(AnkiFormatHandler).GetMethod("ConvertAnkiToMarkdown", BindingFlags.Static | BindingFlags.NonPublic);

            Assert.NotNull(method);

            var html = "<div>Inline: \\(\\frac{a}{b}\\)</div><div>\\[\\sum_{i=0}^{n} i\\]</div>";
            var mediaMap = new Dictionary<string, string>();
            var tempDir = Path.Combine(Path.GetTempPath(), "capycard_anki_test_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);

            try
            {
                var markdown = Assert.IsType<string>(method!.Invoke(null, new object[] { html, mediaMap, tempDir }));

                Assert.Contains("$\\frac{a}{b}$", markdown);
                Assert.Contains("$$", markdown);
                Assert.Contains("\\sum_{i=0}^{n} i", markdown);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, recursive: true);
                }
            }
        }
    }
}
