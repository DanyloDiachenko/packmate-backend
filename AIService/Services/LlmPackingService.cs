using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AIService.DTOs;

namespace AIService.Services;

public interface IAIService
{
    Task<AIRecommendationResponse> GenerateRecommendationsAsync(GenerateRecommendationsRequest request, CancellationToken ct = default);
    Task<TripGeneratorResponse> GenerateTripAsync(TripGeneratorRequest request, List<ExistingTripDateDto>? existingTrips = null, CancellationToken ct = default);
}

public class LlmPackingService : IAIService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<LlmPackingService> _logger;

    public LlmPackingService(HttpClient http, IConfiguration config, ILogger<LlmPackingService> logger)
    {
        _http = http;
        _config = config;
        _logger = logger;
    }

    public async Task<AIRecommendationResponse> GenerateRecommendationsAsync(GenerateRecommendationsRequest request, CancellationToken ct = default)
    {
        var apiKey = _config["AiSettings:ApiKey"] ?? string.Empty;
        var model = _config["AiSettings:Model"] ?? "gemini-3.6-flash";
        var maxItems = _config["AiSettings:MaxRecommendations"] ?? "5";

        var existingItemsList = request.ExistingItems != null && request.ExistingItems.Count > 0
            ? string.Join(", ", request.ExistingItems)
            : "None";

        var systemPrompt = $$"""
        You are PackPilot, an expert and practical travel packing assistant.
        Your mission is to recommend high-utility, context-aware, and critical packing items for an upcoming trip based on the traveler's destination, climate, season, current weather forecast, trip type/tags, duration, and local country specifics (e.g., electrical plug/socket types, voltage, local customs, and weather quirks).
        RULES:
        1. SUGGEST AT MOST {{maxItems}} of the most useful, context-driven items. Prioritize items travelers frequently forget (e.g., specific plug adapters, climate layers, rain protection, local essentials).
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
                },
                {
                    "section": "tools",
                    "title": "Compact Umbrella",
                    "quantity": 1,
                    "tag": "important",
                    "iconEmoji": "☂️",
                    "reason": "Forecast predicts light rain showers over the weekend."
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

        var payload = new
        {
            system_instruction = new
            {
                parts = new[]
                {
                    new { text = systemPrompt }
                }
            },
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[]
                    {
                        new { text = userMessage }
                    }
                }
            },
            generationConfig = new
            {
                responseMimeType = "application/json",
                temperature = 0.5
            }
        };

        var baseUrl = _config["AiSettings:BaseUrl"]?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl) || baseUrl.Contains("gemini.google.com"))
        {
            baseUrl = "https://generativelanguage.googleapis.com/v1beta";
        }

        var endpoint = $"{baseUrl}/models/{model}:generateContent?key={apiKey}";

        var requestMessage = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            requestMessage.Headers.Add("x-goog-api-key", apiKey);
        }

        var response = await _http.SendAsync(requestMessage, ct);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Gemini API request failed with status {StatusCode}: {Error}", response.StatusCode, errorBody);
            response.EnsureSuccessStatusCode();
        }

        var responseJson = await response.Content.ReadAsStringAsync(ct);
        var geminiResponse = JsonSerializer.Deserialize<GeminiGenerateContentResponse>(responseJson);

        var contentString = geminiResponse?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;
        if (string.IsNullOrWhiteSpace(contentString))
        {
            _logger.LogWarning("Gemini API returned an empty response.");
            return new AIRecommendationResponse(new List<RecommendationItemDto>(), "Failed to generate recommendations from AI.");
        }

        var cleanedJson = contentString.Trim();
        if (cleanedJson.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
        {
            cleanedJson = cleanedJson.Substring(7);
        }
        else if (cleanedJson.StartsWith("```", StringComparison.OrdinalIgnoreCase))
        {
            cleanedJson = cleanedJson.Substring(3);
        }

        if (cleanedJson.EndsWith("```", StringComparison.OrdinalIgnoreCase))
        {
            cleanedJson = cleanedJson.Substring(0, cleanedJson.Length - 3);
        }

        cleanedJson = cleanedJson.Trim();

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var recommendations = JsonSerializer.Deserialize<AIRecommendationResponse>(cleanedJson, options);

        return recommendations ?? new AIRecommendationResponse(new List<RecommendationItemDto>(), string.Empty);
    }

    public async Task<TripGeneratorResponse> GenerateTripAsync(TripGeneratorRequest request, List<ExistingTripDateDto>? existingTrips = null, CancellationToken ct = default)
    {
        var apiKey = _config["AiSettings:ApiKey"] ?? string.Empty;
        var model = _config["AiSettings:Model"] ?? "gemini-3.6-flash";

        var today = DateTime.UtcNow;
        var currentYear = today.Year;

        var relevantTrips = existingTrips?
            .Where(t => t.Id == null || request.Skeleton?.TripId == null || t.Id != request.Skeleton.TripId)
            .ToList() ?? new List<ExistingTripDateDto>();

        var bookedTripsPromptSection = string.Empty;
        if (relevantTrips.Count > 0)
        {
            var bookedList = string.Join("\n", relevantTrips.Select(t =>
                $"- \"{t.Destination}\": {t.DepartDate:yyyy-MM-dd} to {t.ReturnDate:yyyy-MM-dd}"
            ));

            bookedTripsPromptSection = $"""

            EXISTING BOOKED TRIPS & DATE AVAILABILITY RULES:
            The traveler already has the following trips booked:
            {bookedList}

            AVAILABILITY VALIDATION RULES:
            - You MUST ensure the proposed trip dates do NOT conflict or overlap with any of the traveler's existing booked trips.
            - Overlap definition: A new trip overlaps with an existing trip if (NewDepart < ExistingReturn && NewReturn > ExistingDepart) or if both trips are single-day trips on the exact same date. Adjacent dates (where a new trip starts on the return date of another trip or ends on the departure date of another trip) are ALLOWED.
            - If the traveler specifies EXACT dates and they conflict with an existing booked trip:
              * Set "success": false.
              * Set "successMessage": null.
              * Set "errorMessage": "The requested dates conflict with your existing trip to [Destination] ([DepartDate] to [ReturnDate]). Multiple trips cannot cover the same date range."
              * Set "missingFields": ["DepartDate", "ReturnDate"].
              * Set "skeleton": null.
              * Set "nextPossibleActions": List of 2-3 suggestions (e.g. "Choose an alternative week", "Adjust dates to avoid the conflict").
            - If the traveler specifies an APPROXIMATE timeframe, month, or season (e.g. "in July", "1 week beach vacation in Barcelona, Spain in July"):
              * Choose concrete calendar dates ("departDate" and "returnDate") within that requested month/period that completely AVOID and do NOT overlap with these booked trips.
              * If ALL available consecutive dates for the requested duration in that month/timeframe are completely blocked by booked trips:
                Set "success": false.
                Set "successMessage": null.
                Set "errorMessage": "All dates for a [duration]-day trip in [month/timeframe] conflict with your existing booked trips. Please choose another month or adjust the duration."
                Set "missingFields": ["DepartDate", "ReturnDate"].
                Set "skeleton": null.
                Set "nextPossibleActions": List of 2-3 suggestions for free weeks in adjacent months.
            """;
        }

        string systemPrompt;
        if (request.Skeleton != null)
        {
            var existingSkeletonJson = JsonSerializer.Serialize(request.Skeleton, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            systemPrompt = $$"""
            You are ReadyRoam's expert AI Trip Architect & Packing Specialist.
            You are in TRIP UPDATE MODE. The traveler has provided an EXISTING TRIP SKELETON below and sent a message requesting updates, modifications, refinements, or additional items for their trip.

            CURRENT DATE CONTEXT: Today is {{today:yyyy-MM-dd}} (UTC).

            EXISTING TRIP SKELETON:
            {{existingSkeletonJson}}
            {{bookedTripsPromptSection}}

            TRIP UPDATE RULES:
            1. You MUST use the existing skeleton as your starting base. Do NOT discard unchanged details.
            2. Apply the traveler's requested changes directly to this skeleton:
               - If dates or duration change (either exact dates or flexible timeframe like 'reschedule to 1 week in August'):
                 * Choose concrete calendar dates within the requested window that do NOT overlap with booked trips.
                 * Recalculate "departDate", "returnDate", and "durationDays". Update "weatherSummary" and "currentSeason" to match the new dates.
               - If destination changes: update "city", "country", "countryFlag", "bannerImage", "weatherSummary", and "currentSeason".
               - If trip type/tags change: add, remove, or replace relevant "tags".
               - If packing items are requested (add, remove, replace, modify quantities, optimize):
                 * Retain existing items that the user didn't ask to remove or modify.
                 * Add newly requested items with appropriate "section", "title", "quantity", "tag" ("essential"|"important"|"safety"|"optional"), "iconEmoji", and "reason".
                 * Remove or modify items as requested by the user.
                 * Allowed sections: "clothes", "electro", "documents", "toiletries", "tools".
               - Retain all existing fields (city, country, bannerImage, items, tags) that the user did not ask to alter.
            3. Formulate "successMessage":
               - When successful, generate a concise, friendly explanation (1-2 sentences) of what you modified or checked (e.g. what dates were updated, which items were added or removed, or if an item was already present in the trip items list).
            4. Set "success": true, "errorMessage": null, "missingFields": null.
            5. If the message is completely contradictory, incomprehensible, or impossible to apply:
               Set "success": false.
               Set "successMessage": null.
               Set "errorMessage": Clear explanation of what cannot be updated or needs clarification.
               Set "missingFields": null.
               Set "skeleton": null.
               Set "nextPossibleActions": null.
            6. Provide "nextPossibleActions": List of 0-3 short, highly relevant prompt suggestions ("quick action pills") that the traveler can click to refine their packing list or trip.
               STRICT RULES FOR NEXT POSSIBLE ACTIONS:
               - ReadyRoam is strictly a packing and travel luggage preparation app.
               - Allowed types of actions ONLY:
                 * Adding packing items tailored to specific places, landmarks, or activities planned at the destination (e.g. "Add comfortable walking shoes for city sightseeing and museum walking", "Add modest attire for cathedral visits", "Add beach towel and swimwear").
                 * Adding weather-appropriate packing items (e.g. "Add compact rain umbrella and waterproof jacket", "Add high SPF sun protection").
                 * Luggage or packing constraints (e.g. "Optimize packing list for carry-on luggage only", "Minimize liquid toiletries").
                 * Practical date adjustments if relevant (e.g. "Adjust dates to a different weekend").
               - STRICTLY FORBIDDEN (DO NOT GENERATE):
                 * NO restaurant, bistro, bakery, cafe, food, or dining recommendations.
                 * NO museum ticket reminders, entrance ticket booking, or reservation alerts.
                 * NO general travel reminders, flight reminders, or non-packing advice.
               - IMPORTANT: If no clear, relevant packing or trip adjustments are needed, send an empty array []! Do NOT hallucinate filler suggestions. It is much better to return [] than irrelevant actions.

            OUTPUT FORMAT: Output MUST be STRICTLY raw, valid JSON matching the schema below. Do NOT wrap in markdown code blocks (NO ```json), and do NOT include any introductory or concluding text.

            JSON SCHEMA:
            {
              "success": true,
              "successMessage": "I've updated your trip dates to Nov 10–16, recalculated the 7-day duration, and added a warm waterproof jacket to your clothes section. Type C power adapter was already in your essentials!",
              "errorMessage": null,
              "missingFields": null,
              "skeleton": {
                "city": "Budapest",
                "country": "Hungary",
                "countryFlag": "🇭🇺",
                "bannerImage": "https://images.unsplash.com/photo-1549877452-9c387954fbc2?auto=format&fit=crop&w=1600&q=80",
                "departDate": "2026-11-08",
                "returnDate": "2026-11-13",
                "durationDays": 6,
                "tags": ["Business", "City Break"],
                "weatherSummary": "Chilly autumn weather, 6-11°C with partly cloudy skies",
                "currentSeason": "Autumn",
                "items": [
                  {
                    "section": "documents",
                    "title": "Passport & EU Travel Documents",
                    "quantity": 1,
                    "tag": "essential",
                    "iconEmoji": "🛂",
                    "reason": "Required for international border control and hotel check-in."
                  }
                ]
              },
              "nextPossibleActions": [
                "Optimize luggage for carry-on only",
                "Add formal business meeting attire"
              ]
            }
            """;
        }
        else
        {
            systemPrompt = $$"""
            You are ReadyRoam's expert AI Trip Architect & Packing Specialist.
            Your job is to analyze natural language travel prompts from travelers, extract their trip parameters, validate if all required information is available, and generate a structured Trip Skeleton with weather context, packing items, and next actionable steps.

            CURRENT DATE CONTEXT: Today is {{today:yyyy-MM-dd}} (UTC). Default to year {{currentYear}} unless a specific future date or month requires {{currentYear + 1}} (for example, if today is in October and the user requests 'in July', schedule for July {{currentYear + 1}}).
            {{bookedTripsPromptSection}}

            REQUIRED INFORMATION:
            1. Destination (City and Country). Both MUST be identifiable (e.g. "Barcelona, Spain", "Budapest, Hungary", "Tokyo, Japan").
            2. Travel Dates / Timeframe:
               - EXACT DATES: If the user provides specific dates (e.g. 'Nov 8 - Nov 13', 'October 5 to October 12'), use those exact dates.
               - FLEXIBLE / APPROXIMATE TIMEFRAME: If the user specifies a general timeframe, month, season, and/or duration without exact calendar days (e.g. '1 week beach vacation in Barcelona, Spain in July', 'a weekend in Rome in September', '5 days in Paris this summer'):
                 * Intelligently choose concrete calendar dates ("departDate" and "returnDate") within that requested month or season matching the duration (e.g. 1 week = 7 days, weekend = 3 days; default = 5-7 days if duration is unspecified).
                 * Ensure the chosen dates completely AVOID and do NOT overlap with any of the traveler's existing booked trips listed above. Pick a convenient block (e.g., Saturday to Saturday or mid-month) that is completely free.
                 * If the user asks for a month that has already passed in {{currentYear}} relative to today ({{today:yyyy-MM-dd}}), schedule for {{currentYear + 1}}.

            VALIDATION LOGIC:
            - If Destination (City or Country) is missing, ambiguous, or incomplete (e.g. 'I want a vacation in July' with no destination, or 'I go to Ukraine for spa' with no city):
              Set "success": false.
              Set "successMessage": null.
              Provide a clear, friendly "errorMessage" in English explaining what is missing (e.g. "Please specify the destination city and timeframe (e.g., '1 week beach vacation in Barcelona in July').").
              Set "missingFields" to ["City", "Country"].
              Set "skeleton": null.
              Set "nextPossibleActions": null.
            - If no timeframe, dates, month, or season can be identified at all (e.g. 'trip to Barcelona' with zero timing):
              Set "success": false.
              Set "successMessage": null.
              Provide a clear, friendly "errorMessage" in English (e.g. "Please specify when you plan to travel (e.g., 'in July', 'next weekend', or 'Nov 8 - Nov 13').").
              Set "missingFields" to ["DepartDate", "ReturnDate"].
              Set "skeleton": null.
              Set "nextPossibleActions": null.
            - If the requested timeframe is completely blocked by existing booked trips and no free window of the requested duration exists in that month/period:
              Set "success": false.
              Set "successMessage": null.
              Provide a clear, friendly "errorMessage" explaining that the requested month or timeframe is fully blocked by your existing trips and suggest picking another month.
              Set "missingFields": ["DepartDate", "ReturnDate"].
              Set "skeleton": null.
              Set "nextPossibleActions": List of 2-3 suggestions for open dates in adjacent months.
            - If all required details are present (either exact dates or flexible timeframe with available dates):
              Set "success": true.
              Set "successMessage": A concise, friendly summary (1-2 sentences) explaining the generated trip, mentioning the destination, the selected dates, duration, and context (e.g. "I've planned your 1-week beach vacation in Barcelona, Spain from July 11 to July 18, {{currentYear + 1}}, perfectly scheduled around your other trips with summer beach essentials!").
              Set "errorMessage": null.
              Set "missingFields": null.
              Generate the "skeleton" object with:
                - "city": Normalized destination city name.
                - "country": Normalized destination country name.
                - "countryFlag": ISO flag emoji (e.g. 🇪🇸, 🇭🇺, 🇫🇷, 🇯🇵).
                - "bannerImage": High-quality scenic Unsplash photo URL representing the city or country, or null.
                - "departDate": "YYYY-MM-DD".
                - "returnDate": "YYYY-MM-DD".
                - "durationDays": Calculated integer days (minimum 1).
                - "tags": List of trip types/activities (e.g. ["Beach", "Leisure", "City Break", "Adventure"]).
                - "weatherSummary": Estimated/predicted weather for destination and dates (e.g. "Warm Mediterranean sunshine, 28-32°C, dry and sunny").
                - "currentSeason": Seasonal context (e.g. "Summer", "Autumn", "Winter", "Spring").
                - "items": Curated starter list of 8-15 context-aware packing items categorized across:
                    - "clothes"
                    - "electro"
                    - "documents"
                    - "toiletries"
                    - "tools"
                  Each item must include "section", "title", "quantity", "tag" ("essential"|"important"|"safety"|"optional"), "iconEmoji", and "reason".
              Generate "nextPossibleActions": List of 0-3 short, highly relevant prompt suggestions ("quick action pills") the traveler can click to refine their packing list or trip.
                STRICT RULES FOR NEXT POSSIBLE ACTIONS:
                - ReadyRoam is strictly a packing and travel luggage preparation app.
                - Allowed types of actions ONLY:
                  * Adding packing items tailored to specific places, landmarks, or activities planned at the destination (e.g. "Add comfortable walking shoes for city sightseeing and museum walking", "Add modest attire for cathedral visits", "Add beach towel and water shoes").
                  * Adding weather-appropriate packing items (e.g. "Add compact umbrella and rain jacket", "Add high SPF sun protection").
                  * Luggage or packing constraints (e.g. "Optimize packing list for carry-on luggage only", "Minimize liquid toiletries").
                  * Practical date adjustments if applicable (e.g. "Adjust dates to a different weekend").
                - STRICTLY FORBIDDEN (DO NOT GENERATE):
                  * NO restaurant, bistro, bakery, cafe, food, or dining recommendations.
                  * NO museum ticket reminders, entrance ticket booking, or reservation alerts.
                  * NO general travel reminders, flight reminders, or non-packing advice.
                - IMPORTANT: If no highly relevant packing or trip adjustments are needed, send an empty array []! Do NOT hallucinate filler suggestions. An empty array [] is completely acceptable and preferred over irrelevant suggestions.

            OUTPUT FORMAT: Output MUST be STRICTLY raw, valid JSON matching the schema below. Do NOT wrap in markdown code blocks (NO ```json), and do NOT include any introductory or concluding text.

            JSON SCHEMA:
            {
              "success": true,
              "successMessage": "I've planned your 1-week beach vacation in Barcelona, Spain from July 11 to July 18, 2027, with curated summer beach essentials!",
              "errorMessage": null,
              "missingFields": null,
              "skeleton": {
                "city": "Barcelona",
                "country": "Spain",
                "countryFlag": "🇪🇸",
                "bannerImage": "https://images.unsplash.com/photo-1583422409516-2895a77efded?auto=format&fit=crop&w=1600&q=80",
                "departDate": "2027-07-11",
                "returnDate": "2027-07-18",
                "durationDays": 7,
                "weatherSummary": "Hot and sunny Mediterranean weather, 28-32°C with warm sea temperatures",
                "currentSeason": "Summer",
                "items": [
                  {
                    "section": "clothes",
                    "title": "Swimwear & Beach Cover-up",
                    "quantity": 2,
                    "tag": "essential",
                    "iconEmoji": "🩱",
                    "reason": "Essential for Barcelona's Barceloneta and Bogatell beaches."
                  },
                  {
                    "section": "toiletries",
                    "title": "Reef-Safe High SPF Sunscreen",
                    "quantity": 1,
                    "tag": "essential",
                    "iconEmoji": "🧴",
                    "reason": "Intense Mediterranean UV index in July requires strong sun protection."
                  }
                ]
              },
              "nextPossibleActions": [
                "Optimize luggage for carry-on only",
                "Add beach towel and water shoes",
                "Add comfortable walking shoes for Park Güell and Gothic Quarter"
              ]
            }
            """;
        }

        var payload = new
        {
            system_instruction = new
            {
                parts = new[]
                {
                    new { text = systemPrompt }
                }
            },
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[]
                    {
                        new { text = request.Message }
                    }
                }
            },
            generationConfig = new
            {
                responseMimeType = "application/json",
                temperature = 0.4
            }
        };

        var baseUrl = _config["AiSettings:BaseUrl"]?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl) || baseUrl.Contains("gemini.google.com"))
        {
            baseUrl = "https://generativelanguage.googleapis.com/v1beta";
        }

        var endpoint = $"{baseUrl}/models/{model}:generateContent?key={apiKey}";

        var requestMessage = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            requestMessage.Headers.Add("x-goog-api-key", apiKey);
        }

        var response = await _http.SendAsync(requestMessage, ct);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Gemini API request failed with status {StatusCode}: {Error}", response.StatusCode, errorBody);
            response.EnsureSuccessStatusCode();
        }

        var responseJson = await response.Content.ReadAsStringAsync(ct);
        var geminiResponse = JsonSerializer.Deserialize<GeminiGenerateContentResponse>(responseJson);

        var contentString = geminiResponse?.Candidates?.FirstOrDefault()?.Content?.Parts?.FirstOrDefault()?.Text;
        if (string.IsNullOrWhiteSpace(contentString))
        {
            _logger.LogWarning("Gemini API returned an empty response for Trip Generator.");
            return new TripGeneratorResponse(false, null, "AI Service was unable to process the request.", null, null, null);
        }

        var cleanedJson = contentString.Trim();
        if (cleanedJson.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
        {
            cleanedJson = cleanedJson.Substring(7);
        }
        else if (cleanedJson.StartsWith("```", StringComparison.OrdinalIgnoreCase))
        {
            cleanedJson = cleanedJson.Substring(3);
        }

        if (cleanedJson.EndsWith("```", StringComparison.OrdinalIgnoreCase))
        {
            cleanedJson = cleanedJson.Substring(0, cleanedJson.Length - 3);
        }

        cleanedJson = cleanedJson.Trim();

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var generatorResponse = JsonSerializer.Deserialize<TripGeneratorResponse>(cleanedJson, options);

        if (generatorResponse == null)
        {
            return new TripGeneratorResponse(false, null, "Failed to parse AI Trip Generator output.", null, null, null);
        }

        if (generatorResponse.Success && generatorResponse.Skeleton != null && relevantTrips.Count > 0)
        {
            var skeleton = generatorResponse.Skeleton;
            var conflict = relevantTrips.FirstOrDefault(t =>
                (skeleton.DepartDate < t.ReturnDate && skeleton.ReturnDate > t.DepartDate) ||
                (skeleton.DepartDate == skeleton.ReturnDate && t.DepartDate == t.ReturnDate && skeleton.DepartDate == t.DepartDate)
            );

            if (conflict != null)
            {
                return new TripGeneratorResponse(
                    false,
                    null,
                    $"The requested dates ({skeleton.DepartDate:yyyy-MM-dd} to {skeleton.ReturnDate:yyyy-MM-dd}) conflict with your existing trip to '{conflict.Destination}' ({conflict.DepartDate:yyyy-MM-dd} to {conflict.ReturnDate:yyyy-MM-dd}). Multiple trips cannot cover the same date range.",
                    new List<string> { "DepartDate", "ReturnDate" },
                    null,
                    new List<string>
                    {
                        "Choose a different date range",
                        "Reschedule conflicting trip",
                        "Adjust duration to avoid date collision"
                    }
                );
            }
        }

        return generatorResponse;
    }
}

public record GeminiGenerateContentResponse(
    [property: JsonPropertyName("candidates")] List<GeminiCandidate>? Candidates
);

public record GeminiCandidate(
    [property: JsonPropertyName("content")] GeminiContent? Content,
    [property: JsonPropertyName("finishReason")] string? FinishReason
);

public record GeminiContent(
    [property: JsonPropertyName("parts")] List<GeminiPart>? Parts,
    [property: JsonPropertyName("role")] string? Role
);

public record GeminiPart(
    [property: JsonPropertyName("text")] string? Text
);