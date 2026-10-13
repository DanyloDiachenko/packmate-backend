using AIService.DTOs;

namespace AIService.Domain;

public static class CopilotDiffReconciler
{
    public static AIPackingCopilotResponse Reconcile(
        List<CopilotExistingItemDto> existingItems,
        GeminiCopilotDiffOutput output,
        string? userPrompt)
    {
        if (!output.Success)
        {
            return new AIPackingCopilotResponse(false, output.Explanation,
                new(), new(), new(), new(),
                output.ErrorMessage ?? "Copilot was unable to produce suggestions.");
        }

        var existingTitles = new HashSet<string>(
            existingItems.Select(i => i.Title.Trim().ToLowerInvariant()),
            StringComparer.OrdinalIgnoreCase);
        var byId = existingItems.ToDictionary(i => i.Id);
        var byTitle = existingItems
            .GroupBy(i => i.Title.Trim().ToLowerInvariant(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var existingSections = existingItems
            .Select(i => i.Section).Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim()).Distinct().ToList();

        var promptMentionsRemove = !string.IsNullOrWhiteSpace(userPrompt) &&
            (userPrompt.Contains("remove", StringComparison.OrdinalIgnoreCase) ||
             userPrompt.Contains("delete", StringComparison.OrdinalIgnoreCase) ||
             userPrompt.Contains("packed", StringComparison.OrdinalIgnoreCase) ||
             userPrompt.Contains("прибери", StringComparison.OrdinalIgnoreCase) ||
             userPrompt.Contains("видали", StringComparison.OrdinalIgnoreCase));

        return new AIPackingCopilotResponse(
            Success: true,
            Explanation: output.Explanation ?? "Generated packing suggestions based on your request.",
            Additions: BuildAdditions(output.Additions, existingTitles, existingSections),
            Removals: BuildRemovals(output.Removals, byId, byTitle, promptMentionsRemove),
            Modifications: BuildModifications(output.Modifications, byId, byTitle),
            NextActions: (output.NextActions ?? new()).Where(a => !string.IsNullOrWhiteSpace(a)).Take(3).ToList(),
            ErrorMessage: null
        );
    }

    private static List<CopilotAdditionDto> BuildAdditions(
        List<GeminiAdditionDto>? raw,
        HashSet<string> existingTitles,
        List<string> existingSections)
    {
        var result = new List<CopilotAdditionDto>();
        foreach (var item in raw ?? new())
        {
            var title = item.Title?.Trim();
            if (string.IsNullOrWhiteSpace(title) || !existingTitles.Add(title.ToLowerInvariant())) continue;

            result.Add(new CopilotAdditionDto(
                Id: Guid.NewGuid(),
                Section: SectionNameResolver.Resolve(item.Section, existingSections),
                Title: title,
                Quantity: item.Quantity > 0 ? item.Quantity : 1,
                Tag: !string.IsNullOrWhiteSpace(item.Tag) ? item.Tag : "Important",
                Reason: !string.IsNullOrWhiteSpace(item.Reason) ? item.Reason : "Recommended for your trip"
            ));
        }
        return result;
    }

    private static List<CopilotRemovalDto> BuildRemovals(
        List<GeminiRemovalDto>? raw,
        Dictionary<Guid, CopilotExistingItemDto> byId,
        Dictionary<string, CopilotExistingItemDto> byTitle,
        bool promptMentionsRemove)
    {
        var result = new List<CopilotRemovalDto>();
        var processed = new HashSet<Guid>();
        foreach (var item in raw ?? new())
        {
            var matched = ResolveItem(item.ItemId, item.Title, byId, byTitle);
            if (matched == null || !processed.Add(matched.Id)) continue;
            if (matched.IsPacked && !promptMentionsRemove) continue;

            result.Add(new CopilotRemovalDto(
                Id: Guid.NewGuid(),
                ItemId: matched.Id,
                Title: matched.Title,
                Section: matched.Section,
                Reason: !string.IsNullOrWhiteSpace(item.Reason) ? item.Reason : "Not needed for this trip"
            ));
        }
        return result;
    }

    private static List<CopilotModificationDto> BuildModifications(
        List<GeminiModificationDto>? raw,
        Dictionary<Guid, CopilotExistingItemDto> byId,
        Dictionary<string, CopilotExistingItemDto> byTitle)
    {
        var result = new List<CopilotModificationDto>();
        var processed = new HashSet<Guid>();
        foreach (var item in raw ?? new())
        {
            var matched = ResolveItem(item.ItemId, item.Title, byId, byTitle);
            if (matched == null || !processed.Add(matched.Id)) continue;

            var newQty = item.NewQuantity > 0 ? item.NewQuantity : matched.Quantity;
            var newTag = !string.IsNullOrWhiteSpace(item.NewTag) ? item.NewTag : matched.Tag;
            if (newQty == matched.Quantity && string.Equals(newTag, matched.Tag, StringComparison.OrdinalIgnoreCase)) continue;

            result.Add(new CopilotModificationDto(
                Id: Guid.NewGuid(),
                ItemId: matched.Id,
                Title: matched.Title,
                Section: matched.Section,
                CurrentQuantity: matched.Quantity,
                NewQuantity: newQty,
                NewTag: newTag,
                Reason: !string.IsNullOrWhiteSpace(item.Reason) ? item.Reason : "Adjusted for your trip"
            ));
        }
        return result;
    }

    private static CopilotExistingItemDto? ResolveItem(
        string? rawId, string? title,
        Dictionary<Guid, CopilotExistingItemDto> byId,
        Dictionary<string, CopilotExistingItemDto> byTitle)
    {
        if (!string.IsNullOrWhiteSpace(rawId) && Guid.TryParse(rawId, out var id) && byId.TryGetValue(id, out var m1))
            return m1;
        if (!string.IsNullOrWhiteSpace(title) && byTitle.TryGetValue(title.Trim().ToLowerInvariant(), out var m2))
            return m2;
        return null;
    }
}
