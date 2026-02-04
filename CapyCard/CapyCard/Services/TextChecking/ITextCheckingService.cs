using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CapyCard.Models;

namespace CapyCard.Services.TextChecking
{
    public interface ITextCheckingService
    {
        Task<IReadOnlyList<TextIssue>> CheckAsync(string text, string locale, CancellationToken ct);
        Task<bool> AddWordAsync(string word, string locale, CancellationToken ct);
    }
}
