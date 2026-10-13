namespace AIService.Domain;

public static class SectionNameResolver
{
    public static string? GetCategoryKey(string? section)
    {
        if (string.IsNullOrWhiteSpace(section)) return null;
        var s = section.Trim().ToLowerInvariant();
        if (s.Contains("cloth") || s.Contains("footwear") || s.Contains("wear") || s.Contains("shoe") || s.Contains("pant") || s.Contains("shirt")) return "clothes";
        if (s.Contains("doc") || s.Contains("essent") || s.Contains("money") || s.Contains("passport") || s.Contains("wallet")) return "documents";
        if (s.Contains("toil") || s.Contains("hygiene") || s.Contains("bath") || s.Contains("cosmetic")) return "toiletries";
        if (s.Contains("electr") || s.Contains("tech") || s.Contains("gadget") || s.Contains("device")) return "electronics";
        if (s.Contains("health") || s.Contains("well") || s.Contains("med") || s.Contains("first aid") || s.Contains("pharmacy")) return "health";
        if (s.Contains("misc") || s.Contains("gear") || s.Contains("tool")) return "misc";
        return null;
    }

    public static bool IsSameCategory(string? a, string? b)
    {
        var ca = GetCategoryKey(a);
        return ca != null && ca == GetCategoryKey(b);
    }

    public static string Resolve(string? requested, IEnumerable<string>? existingSections = null)
    {
        var trimmed = requested?.Trim() ?? string.Empty;
        var existing = (existingSections ?? Enumerable.Empty<string>())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .Distinct()
            .ToList();

        if (existing.Count > 0)
        {
            var exact = existing.FirstOrDefault(s => string.Equals(s, trimmed, StringComparison.OrdinalIgnoreCase));
            if (exact != null) return exact;

            var byCategory = existing.FirstOrDefault(s => IsSameCategory(s, trimmed));
            if (byCategory != null) return byCategory;

            if (existing.All(s => s == s.ToLowerInvariant()))
                return Normalize(trimmed).ToLowerInvariant();
        }

        return Normalize(trimmed);
    }

    public static string Normalize(string? section)
    {
        if (string.IsNullOrWhiteSpace(section)) return "Miscellaneous & Gear";
        var s = section.Trim().ToLowerInvariant();
        if (s.Contains("doc") || s.Contains("essent") || s.Contains("money") || s.Contains("passport") || s.Contains("wallet")) return "Essentials & Documents";
        if (s.Contains("cloth") || s.Contains("footwear") || s.Contains("wear") || s.Contains("shoe") || s.Contains("pant") || s.Contains("shirt")) return "Clothes & Footwear";
        if (s.Contains("toil") || s.Contains("hygiene") || s.Contains("bath") || s.Contains("cosmetic")) return "Toiletries & Hygiene";
        if (s.Contains("electr") || s.Contains("tech") || s.Contains("gadget") || s.Contains("device")) return "Electronics & Tech";
        if (s.Contains("health") || s.Contains("well") || s.Contains("med") || s.Contains("first aid") || s.Contains("pharmacy")) return "Health & Wellness";
        if (s.Contains("misc") || s.Contains("gear") || s.Contains("tool")) return "Miscellaneous & Gear";
        return section.Trim();
    }
}
