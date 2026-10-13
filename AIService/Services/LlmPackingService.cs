using System.Text.Json;
using AIService.Domain;
using AIService.DTOs;
using AIService.Infrastructure;
using AIService.Settings;
using Microsoft.Extensions.Options;

namespace AIService.Services;

public interface IAIService
{
    Task<AIRecommendationResponse> GenerateRecommendationsAsync(GenerateRecommendationsRequest request, CancellationToken ct = default);
    Task<TripGeneratorResponse> GenerateTripAsync(TripGeneratorRequest request, List<ExistingTripDateDto>? existingTrips = null, CancellationToken ct = default);
    Task<AIPackingCopilotResponse> GeneratePackingCopilotDiffAsync(AIPackingCopilotRequest request, CancellationToken ct = default);
    Task<WeatherRepackResponseDto> GenerateWeatherRepackDiffAsync(WeatherRepackRequestDto request, CancellationToken ct = default);
}

public class LlmPackingService : IAIService
{
    private readonly GeminiClient _gemini;
    private readonly AiSettings _settings;
    private readonly ILogger<LlmPackingService> _logger;
    private static readonly JsonSerializerOptions CaseInsensitive = new() { PropertyNameCaseInsensitive = true };

    public LlmPackingService(GeminiClient gemini, IOptions<AiSettings> options, ILogger<LlmPackingService> logger)
    {
        _gemini = gemini;
        _settings = options.Value;
        _logger = logger;
    }

    public async Task<AIRecommendationResponse> GenerateRecommendationsAsync(GenerateRecommendationsRequest request, CancellationToken ct = default)
    {
        var existingItemsList = request.ExistingItems is { Count: > 0 }
            ? string.Join(", ", request.ExistingItems)
            : "None";

        var systemPrompt = $$"""
        You are PackPilot, an expert and practical travel packing assistant.
        Your mission is to recommend high-utility, context-aware, and critical packing items for an upcoming trip based on the traveler's destination, climate, season, current weather forecast, trip type/tags, duration, and local country specifics (e.g., electrical plug/socket types, voltage, local customs, and weather quirks).
        RULES:
        1. SUGGEST AT MOST {{_settings.MaxRecommendations}} of the most useful, context-driven items. Prioritize items travelers frequently forget (e.g., specific plug adapters, climate layers, rain protection, local essentials).
        2. DO NOT DUPLICATE: Never suggest any items already present in the traveler's checklist: [{{existingItemsList}}]. Perform case-insensitive deduplication against this list.
        3. SECTION CATEGORIES: The "section" field MUST strictly be one of:
        - "clothes"     (Clothes & Footwear)
        - "electro"     (Electronics & Tech)
        - "documents"   (Documents & Money)
        - "toiletries"  (Toiletries & Health)
        - "tools"       (Misc & Gear / Accessories)
        4. TAGS: The "tag" field MUST strictly be one of:
        - "essential"   (Absolute must-have)
        - "important"   (Highly recommended)
        - "safety"      (Health, safety, or legal requirement)
        - "optional"    (Convenience / nice-to-have)
        5. REASONS: The "reason" field must be concise (1 short sentence) explaining specifically *why* based on destination, weather, plug type, or activity.
        6. EMOJI: Provide a single relevant Unicode emoji in "iconEmoji" for each item (e.g., "🔌", "☂️", "🧴", "🥾", "💊").
        7. OUTPUT FORMAT: Output MUST be STRICTLY raw, valid JSON matching the schema below. Do NOT wrap in markdown code blocks (NO ```json), and do NOT include any introductory or concluding text.
        JSON SCHEMA:
        {
            "recommendations": [
                {
                    "section": "electro",
                    "title": "Type C / F Power Adapter",
                    "quantity": 1,
                    "tag": "essential",
                    "iconEmoji": "🔌",
                    "reason": "Spain uses Type C and F electrical outlets at 230V."
                }
            ],
            "summaryNotes": "1-2 brief, friendly travel tips tailored to the destination and forecast."
        }
        """;

        var userMessage = $"""
        Trip Destination: {request.City}, {request.Country}
        Duration: {request.TripDays} days
        Trip Type: {request.TripType}
        Season: {request.CurrentSeason ?? "Not specified"}
        Weather / Forecast: {request.WeatherSummary ?? "Typical for this season"}
        """;

        var content = await _gemini.GenerateContentAsync(systemPrompt, userMessage, temperature: 0.5, ct);
        if (content is null)
        {
            _logger.LogWarning("Gemini returned empty response for recommendations");
            return new AIRecommendationResponse(new(), "Failed to generate recommendations from AI.");
        }

        return JsonSerializer.Deserialize<AIRecommendationResponse>(content, CaseInsensitive)
               ?? new AIRecommendationResponse(new(), string.Empty);
    }

    public async Task<TripGeneratorResponse> GenerateTripAsync(TripGeneratorRequest request, List<ExistingTripDateDto>? existingTrips = null, CancellationToken ct = default)
    {
        var today = DateTime.UtcNow;
        var currentYear = today.Year;

        var relevantTrips = existingTrips?
            .Where(t => t.Id == null || request.Skeleton?.TripId == null || t.Id != request.Skeleton.TripId)
            .ToList() ?? new();

        var bookedTripsSection = BuildBookedTripsSection(relevantTrips);
        var systemPrompt = request.Skeleton != null
            ? BuildTripUpdatePrompt(request.Skeleton, bookedTripsSection, today)
            : BuildTripCreatePrompt(bookedTripsSection, today, currentYear);

        var content = await _gemini.GenerateContentAsync(systemPrompt, request.Message, temperature: 0.4, ct);
        if (content is null)
        {
            _logger.LogWarning("Gemini returned empty response for trip generator");
            return new TripGeneratorResponse(false, null, "AI Service was unable to process the request.", null, null, null);
        }

        var result = JsonSerializer.Deserialize<TripGeneratorResponse>(content, CaseInsensitive);
        if (result is null)
            return new TripGeneratorResponse(false, null, "Failed to parse AI Trip Generator output.", null, null, null);

        return ValidateConflicts(result, relevantTrips);
    }

    public async Task<AIPackingCopilotResponse> GeneratePackingCopilotDiffAsync(AIPackingCopilotRequest request, CancellationToken ct = default)
    {
        var existingItems = request.ExistingItems ?? new();
        var itemsJson = JsonSerializer.Serialize(existingItems.Select(i => new
        {
            id = i.Id, title = i.Title, section = i.Section,
            quantity = i.Quantity, isPacked = i.IsPacked, tag = i.Tag
        }), new JsonSerializerOptions { WriteIndented = true });

        var contextLines = new System.Text.StringBuilder();
        if (!string.IsNullOrWhiteSpace(request.Destination)) contextLines.AppendLine($"- Destination: {request.Destination}");
        if (!string.IsNullOrWhiteSpace(request.DateRange)) contextLines.AppendLine($"- Travel Dates: {request.DateRange}");
        if (request.NightsCount.HasValue) contextLines.AppendLine($"- Duration: {request.NightsCount.Value} nights");
        var travelContext = contextLines.Length > 0
            ? contextLines.ToString().TrimEnd()
            : "No specific destination details provided. Apply general travel logic.";

        var systemPrompt = $$"""
        You are ReadyRoam's expert AI Packing Copilot.
        You act as an intelligent packing advisor that proposes a structured patch (Diff/Suggestions) to an EXISTING packing list, rather than regenerating a list from scratch or mutating a database.

        TRAVEL CONTEXT:
        {{travelContext}}

        CURRENT CHECKLIST (existingItems):
        {{itemsJson}}

        SECTION MATCHING RULES (section):
        1. CRITICAL: Inspect the section names present in the CURRENT CHECKLIST (existingItems).
        2. If an item belongs to a category that already has items in existingItems (e.g. clothes, toiletries, tech, documents, gear), you MUST use that EXACT section name from existingItems so items are inserted directly into the existing section rather than creating a duplicate section.
        3. Only if a category does not exist in existingItems, use one of these 6 standard categories:
        - "Essentials & Documents"
        - "Clothes & Footwear"
        - "Toiletries & Hygiene"
        - "Electronics & Tech"
        - "Health & Wellness"
        - "Miscellaneous & Gear"

        COPILOT DIFF RULES:
        1. ADDITIONS ("additions"): Suggest relevant items to add. DO NOT DUPLICATE items in existingItems. Provide: "section", "title", "quantity", "tag" ("Essential"|"Important"|"Safety"|"Optional"), "reason".
        2. REMOVALS ("removals"): Suggest removing redundant items. "itemId" MUST match an "id" from existingItems. NEVER remove isPacked:true items unless the user explicitly requested it. Provide: "itemId", "title", "section", "reason".
        3. MODIFICATIONS ("modifications"): Suggest quantity or tag changes. "itemId" MUST match an "id" from existingItems. Provide: "itemId", "title", "section", "newQuantity", "newTag", "reason".
        4. NEXT ACTIONS ("nextActions"): 1-3 short logical follow-up actions as first-person phrases.
        5. EXPLANATION ("explanation"): Concise 1-2 sentence summary of suggestions.
        6. STRICT DOMAIN: Packing and travel preparation only. No restaurant, museum, flight, or hotel recommendations.

        OUTPUT FORMAT: STRICTLY raw, valid JSON. No markdown fences.

        JSON SCHEMA:
        {
          "success": true,
          "explanation": "...",
          "additions": [{"section":"...","title":"...","quantity":1,"tag":"...","reason":"..."}],
          "removals": [{"itemId":"...","title":"...","section":"...","reason":"..."}],
          "modifications": [{"itemId":"...","title":"...","section":"...","newQuantity":1,"newTag":"...","reason":"..."}],
          "nextActions": ["..."],
          "errorMessage": null
        }
        """;

        var content = await _gemini.GenerateContentAsync(systemPrompt, request.Prompt, temperature: 0.4, ct);
        if (content is null)
            return new AIPackingCopilotResponse(false, null, new(), new(), new(), new(), "AI Service was unable to process the Copilot request.");

        var output = JsonSerializer.Deserialize<GeminiCopilotDiffOutput>(content, CaseInsensitive);
        if (output is null)
            return new AIPackingCopilotResponse(false, null, new(), new(), new(), new(), "Failed to parse AI Copilot output.");

        return CopilotDiffReconciler.Reconcile(existingItems, output, request.Prompt);
    }

    public async Task<WeatherRepackResponseDto> GenerateWeatherRepackDiffAsync(WeatherRepackRequestDto request, CancellationToken ct = default)
    {
        var systemPrompt = """
        You are ReadyRoam's Weather Intelligence Copilot.
        Analyze weather changes for an upcoming trip and propose packing adjustments (Diff).
        CRITICAL GUARDRAILS:
        1. ID INTEGRITY: Do not duplicate items that already exist in existingTripItems.
        2. NO HALLUCINATIONS: Propose only realistic, compact travel items directly related to the weather delta.
        3. CONSERVATIVE QUANTITIES: Quantity must be 1.
        4. STANDARD SECTIONS ONLY: Choose section from: ['Clothes & Footwear', 'Toiletries', 'Accessories', 'Documents & Money', 'Health & Medication', 'Electronics'].
        5. STRUCTURED JSON OUTPUT: Return valid raw JSON matching the exact schema without markdown fences.

        JSON SCHEMA:
        {
          "highlights": [{"icon":"cloud.rain.fill","iconColor":"38BDF8","title":"Rain expected on Thursday","detail":"Showers forecast during daytime outdoor sightseeing. High probability (70%)."}],
          "proposals": [{"title":"Light Packable Rain Jacket","section":"Clothes & Footwear","quantity":1,"tag":"essential","reason":"Sudden heavy rainfall forecast on Thursday (70%)"}],
          "existingMatchedNotice": "Sunglasses & sunscreen already verified in your list. No duplicates suggested."
        }
        """;

        try
        {
            var content = await _gemini.GenerateContentAsync(
                systemPrompt,
                JsonSerializer.Serialize(request, new JsonSerializerOptions { WriteIndented = true }),
                temperature: 0.3, ct);

            if (content is not null)
            {
                var output = JsonSerializer.Deserialize<GeminiWeatherRepackOutput>(content, CaseInsensitive);
                if (output is not null)
                    return WeatherRepackReconciler.Reconcile(request, output);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to call Gemini API for Weather Repack");
        }

        return WeatherRepackReconciler.Fallback(request);
    }

    // ── Private prompt builders ──────────────────────────────────────────

    private static string BuildBookedTripsSection(List<ExistingTripDateDto> trips)
    {
        if (trips.Count == 0) return string.Empty;

        var list = string.Join("\n", trips.Select(t =>
            $"- \"{t.Destination}\": {t.DepartDate:yyyy-MM-dd} to {t.ReturnDate:yyyy-MM-dd}"));

        return $"""

            EXISTING BOOKED TRIPS & DATE AVAILABILITY RULES:
            The traveler already has the following trips booked:
            {list}

            AVAILABILITY VALIDATION RULES:
            - You MUST ensure the proposed trip dates do NOT conflict or overlap with any of the traveler's existing booked trips.
            - Overlap definition: A new trip overlaps with an existing trip if (NewDepart < ExistingReturn && NewReturn > ExistingDepart) or if both trips are single-day trips on the exact same date. Adjacent dates are ALLOWED.
            - If the traveler specifies EXACT dates that conflict: set success=false, provide errorMessage explaining the conflict, set missingFields=["DepartDate","ReturnDate"], skeleton=null.
            - If the traveler specifies an APPROXIMATE timeframe, choose concrete dates that avoid all booked trips. If the entire timeframe is blocked, set success=false.
            """;
    }

    private static string BuildTripUpdatePrompt(TripSkeletonDto skeleton, string bookedSection, DateTime today)
    {
        var existingJson = JsonSerializer.Serialize(skeleton, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        return $$"""
            You are ReadyRoam's expert AI Trip Architect & Packing Specialist.
            You are in TRIP UPDATE MODE. The traveler has provided an EXISTING TRIP SKELETON below and sent a message requesting updates, modifications, refinements, or additional items for their trip.

            CURRENT DATE CONTEXT: Today is {{today:yyyy-MM-dd}} (UTC).

            EXISTING TRIP SKELETON:
            {{existingJson}}
            {{bookedSection}}

            TRIP UPDATE RULES:
            1. Use the existing skeleton as your starting base. Do NOT discard unchanged details.
            2. Apply the traveler's requested changes directly to this skeleton. Update dates, destination, tags, or packing items as requested.
            3. Formulate "successMessage": a concise, friendly explanation (1-2 sentences) of what you modified.
            4. Set "success": true, "errorMessage": null, "missingFields": null on success.
            5. If the message is completely contradictory or impossible to apply: set success=false, errorMessage explains why, skeleton=null.
            6. Provide "nextPossibleActions": 0-3 short packing or trip refinement suggestions. NO dining, museum, or travel booking suggestions.

            OUTPUT FORMAT: STRICTLY raw, valid JSON. No markdown fences.

            JSON SCHEMA:
            {
              "success": true,
              "successMessage": "...",
              "errorMessage": null,
              "missingFields": null,
              "skeleton": { "city":"...", "country":"...", "countryFlag":"...", "bannerImage":"...", "departDate":"YYYY-MM-DD", "returnDate":"YYYY-MM-DD", "durationDays":7, "tags":[], "weatherSummary":"...", "currentSeason":"...", "items":[] },
              "nextPossibleActions": []
            }
            """;
    }

    private static string BuildTripCreatePrompt(string bookedSection, DateTime today, int currentYear)
    {
        return $$"""
            You are ReadyRoam's expert AI Trip Architect & Packing Specialist.
            Your job is to analyze natural language travel prompts, extract trip parameters, validate required info, and generate a structured Trip Skeleton with weather context, packing items, and next actionable steps.

            CURRENT DATE CONTEXT: Today is {{today:yyyy-MM-dd}} (UTC). Default to year {{currentYear}} unless a specific future date or month requires {{currentYear + 1}}.
            {{bookedSection}}

            REQUIRED INFORMATION:
            1. Destination (City and Country) — both MUST be identifiable.
            2. Travel Dates / Timeframe — exact dates or flexible timeframe (month/season + duration).

            VALIDATION LOGIC:
            - If Destination is missing or ambiguous: set success=false, errorMessage explains what is missing, missingFields=["City","Country"], skeleton=null.
            - If no timeframe can be identified: set success=false, errorMessage explains, missingFields=["DepartDate","ReturnDate"], skeleton=null.
            - If all required details are present: set success=true, generate full skeleton with 8-15 curated packing items across sections: "clothes","electro","documents","toiletries","tools".

            PACKING ITEMS: Each item must include "section","title","quantity","tag" ("essential"|"important"|"safety"|"optional"),"iconEmoji","reason".
            NEXT POSSIBLE ACTIONS: 0-3 packing or luggage refinement suggestions only. NO dining, museum ticket, flight, or hotel suggestions.

            OUTPUT FORMAT: STRICTLY raw, valid JSON. No markdown fences.

            JSON SCHEMA:
            {
              "success": true,
              "successMessage": "...",
              "errorMessage": null,
              "missingFields": null,
              "skeleton": { "city":"...", "country":"...", "countryFlag":"...", "bannerImage":"...", "departDate":"YYYY-MM-DD", "returnDate":"YYYY-MM-DD", "durationDays":7, "tags":[], "weatherSummary":"...", "currentSeason":"...", "items":[] },
              "nextPossibleActions": []
            }
            """;
    }

    private static TripGeneratorResponse ValidateConflicts(TripGeneratorResponse result, List<ExistingTripDateDto> trips)
    {
        if (!result.Success || result.Skeleton is null || trips.Count == 0) return result;

        var skeleton = result.Skeleton;
        var conflict = trips.FirstOrDefault(t =>
            (skeleton.DepartDate < t.ReturnDate && skeleton.ReturnDate > t.DepartDate) ||
            (skeleton.DepartDate == skeleton.ReturnDate && t.DepartDate == t.ReturnDate && skeleton.DepartDate == t.DepartDate));

        if (conflict is null) return result;

        return new TripGeneratorResponse(
            Success: false,
            SuccessMessage: null,
            ErrorMessage: $"The requested dates ({skeleton.DepartDate:yyyy-MM-dd} to {skeleton.ReturnDate:yyyy-MM-dd}) conflict with your existing trip to '{conflict.Destination}' ({conflict.DepartDate:yyyy-MM-dd} to {conflict.ReturnDate:yyyy-MM-dd}). Multiple trips cannot cover the same date range.",
            MissingFields: new() { "DepartDate", "ReturnDate" },
            Skeleton: null,
            NextPossibleActions: new() { "Choose a different date range", "Reschedule conflicting trip", "Adjust duration to avoid date collision" }
        );
    }
}
