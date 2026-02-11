using System.Reflection;
using CapyCard.ViewModels;

namespace CapyCard.Tests
{
    public class ImportViewModelPromptTests
    {
        [Fact]
        public void GenerateSystemPrompt_ContainsExtendedMarkdownAndEscapingRules()
        {
            var prompt = InvokeGenerateSystemPrompt();

            Assert.Contains("Tabellen", prompt);
            Assert.Contains("Checklisten", prompt);
            Assert.Contains("Zitate", prompt);
            Assert.Contains("$...$ und Block mit $$...$$", prompt);
            Assert.Contains("Strict-LaTeX", prompt);
            Assert.Contains("\\Sigma", prompt);
            Assert.Contains("Keine Unicode-/Pseudoformeln", prompt);
            Assert.Contains("Es gibt nur eine Ebene \"subDecks\"", prompt);
            Assert.Contains("KEIN weiteres Feld \"subDecks\"", prompt);
            Assert.Contains("Backslashes immer escapen", prompt);
            Assert.Contains("\\\\frac{a}{b}", prompt);
            Assert.Contains("\\n", prompt);
            Assert.Contains("| Regel | Formel |", prompt);
            Assert.DoesNotContain("NICHT unterstützt: Code-Blöcke, Tabellen oder Zitate", prompt);
        }

        private static string InvokeGenerateSystemPrompt()
        {
            var viewModel = new ImportViewModel();
            var method = typeof(ImportViewModel).GetMethod("GenerateSystemPrompt", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(method);

            var result = method!.Invoke(viewModel, null);
            return Assert.IsType<string>(result);
        }
    }
}
