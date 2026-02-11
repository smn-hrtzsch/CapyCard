using System.Linq;
using CapyCard.Services;

namespace CapyCard.Tests
{
    public class MarkdownServiceTests
    {
        [Fact]
        public void NormalizeForPaste_UnifiesLineEndingsAndStripsOuterCodeFence()
        {
            var input = "```markdown\r\nA\r\n\r\n\r\nB\r\n```";

            var normalized = MarkdownService.NormalizeForPaste(input);

            Assert.Equal("A\n\nB", normalized);
        }

        [Fact]
        public void Parse_TableBlock_RecognizesHeaderAndRows()
        {
            var markdown = "| Begriff | Formel |\n| --- | --- |\n| Gerade | y = mx + b |";

            var document = MarkdownService.Parse(markdown);

            var table = Assert.IsType<MarkdownService.MarkdownTableBlock>(Assert.Single(document.Blocks));
            Assert.Equal(2, table.Header.Count);
            Assert.Single(table.Rows);
            Assert.Equal("Begriff", ExtractText(table.Header[0].Inlines));
            Assert.Equal("Formel", ExtractText(table.Header[1].Inlines));
            Assert.Equal("Gerade", ExtractText(table.Rows[0][0].Inlines));
        }

        [Fact]
        public void Parse_TableBlock_PreservesBackslashesInsideCells()
        {
            var markdown = "| Symbol | Bedeutung |\n| --- | --- |\n| $\\Sigma$ | Eingabealphabet |";

            var document = MarkdownService.Parse(markdown);

            var table = Assert.IsType<MarkdownService.MarkdownTableBlock>(Assert.Single(document.Blocks));
            var formula = Assert.IsType<MarkdownService.MarkdownFormulaInline>(
                Assert.Single(table.Rows[0][0].Inlines, static inline => inline is MarkdownService.MarkdownFormulaInline));

            Assert.Equal("\\Sigma", formula.Content);
        }

        [Fact]
        public void Parse_TableBlock_DoesNotSplitPipeInsideInlineFormula()
        {
            var markdown = "| Ausdruck | Bedeutung |\n| --- | --- |\n| $a|b$ | Trennstrich in Formel |";

            var document = MarkdownService.Parse(markdown);

            var table = Assert.IsType<MarkdownService.MarkdownTableBlock>(Assert.Single(document.Blocks));
            Assert.Equal(2, table.Header.Count);
            Assert.Equal(2, table.Rows[0].Count);

            var formula = Assert.IsType<MarkdownService.MarkdownFormulaInline>(
                Assert.Single(table.Rows[0][0].Inlines, static inline => inline is MarkdownService.MarkdownFormulaInline));

            Assert.Equal("a|b", formula.Content);
            Assert.Equal("Trennstrich in Formel", ExtractText(table.Rows[0][1].Inlines));
        }

        [Fact]
        public void Parse_ChecklistBlock_RecognizesCheckedAndUncheckedItems()
        {
            var markdown = "- [ ] Offen\n- [x] Erledigt";

            var document = MarkdownService.Parse(markdown);

            var checklist = Assert.IsType<MarkdownService.MarkdownChecklistBlock>(Assert.Single(document.Blocks));
            Assert.Equal(2, checklist.Items.Count);
            Assert.False(checklist.Items[0].IsChecked);
            Assert.True(checklist.Items[1].IsChecked);
            Assert.Equal("Offen", ExtractText(checklist.Items[0].Inlines));
            Assert.Equal("Erledigt", ExtractText(checklist.Items[1].Inlines));
        }

        [Fact]
        public void Parse_QuoteBlock_RecognizesQuoteLevels()
        {
            var markdown = "> Ebene 1\n>> Ebene 2";

            var document = MarkdownService.Parse(markdown);

            var quote = Assert.IsType<MarkdownService.MarkdownQuoteBlock>(Assert.Single(document.Blocks));
            Assert.Equal(2, quote.Lines.Count);
            Assert.Equal(1, quote.Lines[0].Level);
            Assert.Equal(2, quote.Lines[1].Level);
            Assert.Equal("Ebene 1", ExtractText(quote.Lines[0].Inlines));
            Assert.Equal("Ebene 2", ExtractText(quote.Lines[1].Inlines));
        }

        [Fact]
        public void Parse_InlineFormula_ProtectsFormulaFromInlineMarkup()
        {
            var markdown = "Formel $L(M)=Sigma*$ und *kursiv*";

            var document = MarkdownService.Parse(markdown);
            var paragraph = Assert.IsType<MarkdownService.MarkdownParagraphBlock>(Assert.Single(document.Blocks));

            var formula = Assert.IsType<MarkdownService.MarkdownFormulaInline>(
                Assert.Single(paragraph.Inlines, static inline => inline is MarkdownService.MarkdownFormulaInline));
            Assert.Equal("L(M)=Sigma*", formula.Content);

            var italic = Assert.IsType<MarkdownService.MarkdownTextInline>(
                Assert.Single(paragraph.Inlines, static inline => inline is MarkdownService.MarkdownTextInline text && text.IsItalic));
            Assert.Equal("kursiv", italic.Text);
        }

        [Fact]
        public void Parse_BlockFormula_RecognizesMultilineFormula()
        {
            var markdown = "$$\nL(M) = { w in Sigma* }\nq_0 -> q_1\n$$";

            var document = MarkdownService.Parse(markdown);

            var formulaBlock = Assert.IsType<MarkdownService.MarkdownFormulaBlock>(Assert.Single(document.Blocks));
            Assert.Contains("Sigma*", formulaBlock.Content);
            Assert.Contains("q_0", formulaBlock.Content);
        }

        [Fact]
        public void Parse_FormulaRegression_SigmaGammaAndQ0StayInFormula()
        {
            var markdown = "$L(M) = { w∈Σ* | ∃q∈F ∃u,v∈Γ*: (ε,q_0,w) ⇝*_M (u,q,v) }$";

            var document = MarkdownService.Parse(markdown);
            var paragraph = Assert.IsType<MarkdownService.MarkdownParagraphBlock>(Assert.Single(document.Blocks));

            var formula = Assert.IsType<MarkdownService.MarkdownFormulaInline>(Assert.Single(paragraph.Inlines));
            Assert.Contains("Σ*", formula.Content);
            Assert.Contains("Γ*", formula.Content);
            Assert.Contains("q_0", formula.Content);
        }

        [Fact]
        public void Parse_InlineFormula_StrictLatex_PreservesCommands()
        {
            var markdown = "$L(M)=\\{w\\in\\Sigma^* \\mid \\exists q\\in F\\}$";

            var document = MarkdownService.Parse(markdown);
            var paragraph = Assert.IsType<MarkdownService.MarkdownParagraphBlock>(Assert.Single(document.Blocks));
            var formula = Assert.IsType<MarkdownService.MarkdownFormulaInline>(Assert.Single(paragraph.Inlines));

            Assert.Contains("\\Sigma", formula.Content);
            Assert.Contains("\\exists", formula.Content);
            Assert.Contains("\\mid", formula.Content);
        }

        private static string ExtractText(System.Collections.Generic.IReadOnlyList<MarkdownService.MarkdownInline> inlines)
        {
            return string.Concat(inlines.Select(inline => inline switch
            {
                MarkdownService.MarkdownTextInline textInline => textInline.Text,
                MarkdownService.MarkdownFormulaInline formulaInline => "$" + formulaInline.Content + "$",
                MarkdownService.MarkdownImageInline imageInline => imageInline.AltText,
                _ => string.Empty
            }));
        }
    }
}
