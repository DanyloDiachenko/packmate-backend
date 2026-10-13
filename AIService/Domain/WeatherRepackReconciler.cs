using AIService.DTOs;

namespace AIService.Domain;

public static class WeatherRepackReconciler
{
    public static WeatherRepackResponseDto Reconcile(WeatherRepackRequestDto request, GeminiWeatherRepackOutput output)
    {
        var existing = new HashSet<string>(
            request.ExistingTripItems.Select(i => i.Trim().ToLowerInvariant()),
            StringComparer.OrdinalIgnoreCase);

        var proposals = new List<WeatherProposalDto>();
        foreach (var prop in output.Proposals ?? new())
        {
            var title = prop.Title?.Trim();
            if (string.IsNullOrWhiteSpace(title) || existing.Contains(title.ToLowerInvariant())) continue;

            proposals.Add(new WeatherProposalDto(
                Id: Guid.NewGuid(),
                Title: title,
                Section: SectionNameResolver.Resolve(prop.Section),
                Quantity: prop.Quantity > 0 ? prop.Quantity : 1,
                Tag: !string.IsNullOrWhiteSpace(prop.Tag) ? prop.Tag : "recommended",
                Reason: !string.IsNullOrWhiteSpace(prop.Reason) ? prop.Reason : "Recommended for weather changes"
            ));
        }

        return new WeatherRepackResponseDto(
            Highlights: output.Highlights ?? new(),
            Proposals: proposals,
            ExistingMatchedNotice: output.ExistingMatchedNotice
        );
    }

    public static WeatherRepackResponseDto Fallback(WeatherRepackRequestDto request)
    {
        var highlights = new List<WeatherHighlightDto>();
        var proposals = new List<WeatherProposalDto>();
        var existing = new HashSet<string>(
            request.ExistingTripItems.Select(i => i.Trim().ToLowerInvariant()),
            StringComparer.OrdinalIgnoreCase);

        foreach (var delta in request.ForecastDelta)
        {
            if (delta.RainProbability.HasValue && delta.RainProbability.Value >= 50)
            {
                highlights.Add(new WeatherHighlightDto("cloud.rain.fill", "38BDF8",
                    $"Rain expected on {delta.Date}",
                    $"Showers forecast ({delta.RainProbability}% probability). Consider waterproof gear."));

                if (existing.Add("compact travel umbrella"))
                    proposals.Add(new WeatherProposalDto(Guid.NewGuid(), "Compact Travel Umbrella",
                        "Clothes & Footwear", 1, "essential", $"High rain probability ({delta.RainProbability}%)"));
            }

            if (delta.NightTemp.HasValue && delta.NightTemp.Value <= 10)
            {
                highlights.Add(new WeatherHighlightDto("thermometer.snowflake", "818CF8",
                    $"Cooler evenings ({delta.NightTemp}°C)",
                    $"Nighttime temperature drops to {delta.NightTemp}°C."));

                if (existing.Add("light warm fleece layer"))
                    proposals.Add(new WeatherProposalDto(Guid.NewGuid(), "Light Warm Fleece Layer",
                        "Clothes & Footwear", 1, "recommended", $"Cool evening forecast ({delta.NightTemp}°C)"));
            }
        }

        return new WeatherRepackResponseDto(highlights, proposals, "Verified items against current packing list.");
    }
}
