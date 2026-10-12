using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TripItemsService.Data;
using TripItemsService.DTOs;
using TripItemsService.Entities;

namespace TripItemsService.Endpoints;

public static class TripItemEndpoints
{
    public static RouteGroupBuilder MapTripItemEndpoints(this RouteGroupBuilder group)
    {
        group.WithTags("Trip Items");

        group.MapGet("/{tripId:guid}", async (
            Guid tripId,
            TripItemsDbContext db
        ) =>
        {
            var items = await db.TripItems
                .AsNoTracking()
                .Where(i => i.TripId == tripId)
                .ToListAsync();

            var categoryPrimarySection = new Dictionary<string, string>();
            foreach (var item in items)
            {
                var cat = GetCategoryKey(item.Section);
                if (cat != null && !categoryPrimarySection.ContainsKey(cat))
                {
                    categoryPrimarySection[cat] = item.Section.ToLower().Trim();
                }
            }

            var grouped = items
                .GroupBy(i =>
                {
                    var cat = GetCategoryKey(i.Section);
                    if (cat != null && categoryPrimarySection.TryGetValue(cat, out var primary))
                    {
                        return primary;
                    }
                    return i.Section.ToLower().Trim();
                })
                .OrderBy(g => g.Key.Contains("misc") || g.Key.Contains("gear") ? 1 : 0)
                .ThenBy(g => g.Key)
                .Select(g => new TripSectionGroupResponse(
                    Section: g.Key,
                    Items: g.Select(i => new TripItemResponse(
                        i.Id,
                        i.TripId,
                        g.Key,
                        i.Title,
                        i.Quantity,
                        i.IsTaken,
                        i.Tag
                    )).ToList()
                ))
                .ToList();

            return Results.Ok(grouped);
        })
        .WithName("GetTripItems")
        .WithSummary("Get grouped packing items for a trip")
        .WithDescription("Retrieves all packing items associated with a given trip ID, grouped by section.")
        .Produces<List<TripSectionGroupResponse>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/{tripId:guid}", async (
            Guid tripId,
            CreateTripItemRequest request,
            IValidator<CreateTripItemRequest> validator,
            TripItemsDbContext db,
            IHttpClientFactory httpClientFactory,
            HttpContext httpContext,
            CancellationToken ct
        ) =>
        {
            var isArchived = await CheckIfTripArchivedAsync(httpClientFactory, httpContext, tripId, ct);
            if (isArchived)
            {
                return Results.BadRequest(new ErrorResponse("Cannot add items to an archived trip. Please unarchive the trip first."));
            }

            var validationResult = await validator.ValidateAsync(request, ct);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            var normalizedTitle = request.Title.Trim().ToLower();
            var itemExists = await db.TripItems.AnyAsync(i =>
                i.TripId == tripId &&
                i.Title.ToLower() == normalizedTitle, ct);

            if (itemExists)
            {
                return Results.BadRequest(new ErrorResponse($"An item with the name '{request.Title.Trim()}' already exists in this trip."));
            }

            var existingSections = await db.TripItems
                .Where(i => i.TripId == tripId)
                .Select(i => i.Section)
                .Distinct()
                .ToListAsync(ct);

            var item = new TripItem
            {
                TripId = tripId,
                Section = ResolveTripSection(request.Section, existingSections),
                Title = request.Title.Trim(),
                Quantity = request.Quantity,
                Tag = request.Tag,
                IsTaken = false,
                CreatedAt = DateTime.UtcNow
            };

            db.TripItems.Add(item);
            await db.SaveChangesAsync(ct);

            var response = new TripItemResponse(
                item.Id,
                item.TripId,
                item.Section,
                item.Title,
                item.Quantity,
                item.IsTaken,
                item.Tag
            );

            return Results.Created($"/trip-items/{item.Id}", response);
        })
        .WithName("CreateTripItem")
        .WithSummary("Add a packing item to a trip")
        .WithDescription("Adds a new packing item with section, title, and quantity to the specified trip, validating that an item with the same name does not already exist and that the trip is not archived.")
        .Produces<TripItemResponse>(StatusCodes.Status201Created)
        .ProducesValidationProblem()
        .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/{tripId:guid}/bulk", async (
            Guid tripId,
            BulkCreateTripItemsRequest request,
            TripItemsDbContext db,
            IHttpClientFactory httpClientFactory,
            HttpContext httpContext,
            CancellationToken ct
        ) =>
        {
            var isArchived = await CheckIfTripArchivedAsync(httpClientFactory, httpContext, tripId, ct);
            if (isArchived)
            {
                return Results.BadRequest(new ErrorResponse("Cannot add items to an archived trip. Please unarchive the trip first."));
            }

            if (request.Items == null || request.Items.Count == 0)
            {
                return Results.BadRequest(new ErrorResponse("Items list cannot be empty"));
            }

            var existingTitles = await db.TripItems
                .Where(i => i.TripId == tripId)
                .Select(i => i.Title.ToLower())
                .ToListAsync(ct);

            var existingSet = new HashSet<string>(existingTitles, StringComparer.OrdinalIgnoreCase);
            var seenInBatch = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var duplicateNames = new List<string>();

            foreach (var req in request.Items)
            {
                var trimmedTitle = req.Title.Trim();
                if (existingSet.Contains(trimmedTitle) || !seenInBatch.Add(trimmedTitle))
                {
                    duplicateNames.Add(trimmedTitle);
                }
            }

            if (duplicateNames.Count > 0)
            {
                return Results.BadRequest(new ErrorResponse($"Cannot add duplicate trip items. The following items already exist or are repeated in the request: {string.Join(", ", duplicateNames.Distinct())}"));
            }

            var existingSections = await db.TripItems
                .Where(i => i.TripId == tripId)
                .Select(i => i.Section)
                .Distinct()
                .ToListAsync(ct);

            var newItems = new List<TripItem>();
            foreach (var req in request.Items)
            {
                var resolved = ResolveTripSection(req.Section, existingSections);
                if (!existingSections.Contains(resolved)) existingSections.Add(resolved);

                newItems.Add(new TripItem
                {
                    TripId = tripId,
                    Section = resolved,
                    Title = req.Title.Trim(),
                    Quantity = req.Quantity > 0 ? req.Quantity : 1,
                    Tag = req.Tag,
                    IsTaken = false,
                    CreatedAt = DateTime.UtcNow
                });
            }

            db.TripItems.AddRange(newItems);
            await db.SaveChangesAsync(ct);

            var response = newItems.Select(i => new TripItemResponse(
                i.Id,
                i.TripId,
                i.Section,
                i.Title,
                i.Quantity,
                i.IsTaken,
                i.Tag
            )).ToList();

            return Results.Ok(response);
        })
        .WithName("BulkCreateTripItems")
        .WithSummary("Bulk add items to a trip")
        .WithDescription("Adds multiple packing items to the specified trip in a single request, verifying no duplicates are added and that the trip is not archived.")
        .Produces<List<TripItemResponse>>(StatusCodes.Status200OK)
        .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPatch("/{id:guid}", async (
            Guid id,
            UpdateTripItemRequest request,
            IValidator<UpdateTripItemRequest> validator,
            TripItemsDbContext db,
            IHttpClientFactory httpClientFactory,
            HttpContext httpContext,
            CancellationToken ct
        ) =>
        {
            var item = await db.TripItems.FindAsync([id], ct);
            if (item == null)
            {
                return Results.NotFound(new ErrorResponse("Trip item not found"));
            }

            var isArchived = await CheckIfTripArchivedAsync(httpClientFactory, httpContext, item.TripId, ct);
            if (isArchived)
            {
                return Results.BadRequest(new ErrorResponse("Cannot edit items in an archived trip. Please unarchive the trip first."));
            }

            var validationResult = await validator.ValidateAsync(request, ct);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            if (!string.IsNullOrWhiteSpace(request.Title))
            {
                var newTitle = request.Title.Trim();
                var normalizedNewTitle = newTitle.ToLower();
                var duplicateExists = await db.TripItems.AnyAsync(i =>
                    i.TripId == item.TripId &&
                    i.Id != id &&
                    i.Title.ToLower() == normalizedNewTitle, ct);

                if (duplicateExists)
                {
                    return Results.BadRequest(new ErrorResponse($"An item with the name '{newTitle}' already exists in this trip."));
                }

                item.Title = newTitle;
            }

            if (!string.IsNullOrWhiteSpace(request.Section))
            {
                var existingSections = await db.TripItems
                    .Where(i => i.TripId == item.TripId && i.Id != item.Id)
                    .Select(i => i.Section)
                    .Distinct()
                    .ToListAsync(ct);
                item.Section = ResolveTripSection(request.Section, existingSections);
            }
            if (request.Quantity.HasValue) item.Quantity = request.Quantity.Value;
            if (request.IsTaken.HasValue) item.IsTaken = request.IsTaken.Value;
            if (request.Tag != null) item.Tag = request.Tag;

            await db.SaveChangesAsync(ct);

            return Results.Ok(new TripItemResponse(
                item.Id,
                item.TripId,
                item.Section,
                item.Title,
                item.Quantity,
                item.IsTaken,
                item.Tag
            ));
        })
        .WithName("UpdateTripItem")
        .WithSummary("Update a trip packing item")
        .WithDescription("Updates fields such as title, section, quantity, packed status, or tag for a specific trip item, validating that the trip is not archived.")
        .Produces<TripItemResponse>(StatusCodes.Status200OK)
        .ProducesValidationProblem()
        .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        group.MapDelete("/{id:guid}", async (
            Guid id,
            TripItemsDbContext db,
            IHttpClientFactory httpClientFactory,
            HttpContext httpContext,
            CancellationToken ct
        ) =>
        {
            var item = await db.TripItems.FindAsync([id], ct);
            if (item == null)
            {
                return Results.NotFound(new ErrorResponse("Trip item not found"));
            }

            var isArchived = await CheckIfTripArchivedAsync(httpClientFactory, httpContext, item.TripId, ct);
            if (isArchived)
            {
                return Results.BadRequest(new ErrorResponse("Cannot delete items from an archived trip. Please unarchive the trip first."));
            }

            db.TripItems.Remove(item);
            await db.SaveChangesAsync(ct);

            return Results.NoContent();
        })
        .WithName("DeleteTripItem")
        .WithSummary("Delete a packing item")
        .WithDescription("Removes a packing item from the trip list.")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .Produces<ErrorResponse>(StatusCodes.Status404NotFound);

        group.MapPost("/{tripId:guid}/sync", async (
            Guid tripId,
            SyncTripItemsRequest request,
            TripItemsDbContext db,
            IHttpClientFactory httpClientFactory,
            HttpContext httpContext,
            CancellationToken ct
        ) =>
        {
            var isArchived = await CheckIfTripArchivedAsync(httpClientFactory, httpContext, tripId, ct);
            if (isArchived)
            {
                return Results.BadRequest(new ErrorResponse("Cannot edit items in an archived trip. Please unarchive the trip first."));
            }

            if (request.ItemIdsToRemove != null && request.ItemIdsToRemove.Count > 0)
            {
                var itemsToRemove = await db.TripItems
                    .Where(i => i.TripId == tripId && request.ItemIdsToRemove.Contains(i.Id))
                    .ToListAsync(ct);
                if (itemsToRemove.Count > 0)
                {
                    db.TripItems.RemoveRange(itemsToRemove);
                }
            }

            var existingSections = await db.TripItems
                .Where(i => i.TripId == tripId)
                .Select(i => i.Section)
                .Distinct()
                .ToListAsync(ct);

            if (request.ItemsToUpdate != null && request.ItemsToUpdate.Count > 0)
            {
                var updateMap = request.ItemsToUpdate.ToDictionary(u => u.Id);
                var itemIds = updateMap.Keys.ToList();
                var itemsToUpdate = await db.TripItems
                    .Where(i => i.TripId == tripId && itemIds.Contains(i.Id))
                    .ToListAsync(ct);

                foreach (var item in itemsToUpdate)
                {
                    if (updateMap.TryGetValue(item.Id, out var update))
                    {
                        if (update.Quantity.HasValue && update.Quantity.Value > 0) item.Quantity = update.Quantity.Value;
                        if (!string.IsNullOrWhiteSpace(update.Section))
                        {
                            var resolved = ResolveTripSection(update.Section, existingSections);
                            item.Section = resolved;
                            if (!existingSections.Contains(resolved)) existingSections.Add(resolved);
                        }
                        if (!string.IsNullOrWhiteSpace(update.Tag)) item.Tag = update.Tag;
                        if (!string.IsNullOrWhiteSpace(update.Title)) item.Title = update.Title.Trim();
                    }
                }
            }

            if (request.ItemsToAdd != null && request.ItemsToAdd.Count > 0)
            {
                var existingTitles = await db.TripItems
                    .Where(i => i.TripId == tripId)
                    .Select(i => i.Title.ToLower())
                    .ToListAsync(ct);

                var existingSet = new HashSet<string>(existingTitles, StringComparer.OrdinalIgnoreCase);
                var newItems = new List<TripItem>();

                foreach (var toAdd in request.ItemsToAdd)
                {
                    var trimmedTitle = toAdd.Title.Trim();
                    if (existingSet.Add(trimmedTitle))
                    {
                        var resolved = ResolveTripSection(toAdd.Section, existingSections);
                        if (!existingSections.Contains(resolved)) existingSections.Add(resolved);

                        newItems.Add(new TripItem
                        {
                            TripId = tripId,
                            Section = resolved,
                            Title = trimmedTitle,
                            Quantity = toAdd.Quantity > 0 ? toAdd.Quantity : 1,
                            Tag = toAdd.Tag,
                            IsTaken = false,
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }

                if (newItems.Count > 0)
                {
                    db.TripItems.AddRange(newItems);
                }
            }

            await db.SaveChangesAsync(ct);

            var allItems = await db.TripItems
                .AsNoTracking()
                .Where(i => i.TripId == tripId)
                .Select(i => new TripItemResponse(
                    i.Id,
                    i.TripId,
                    i.Section,
                    i.Title,
                    i.Quantity,
                    i.IsTaken,
                    i.Tag
                ))
                .ToListAsync(ct);

            return Results.Ok(allItems);
        })
        .WithName("SyncTripItems")
        .WithSummary("Atomic sync of trip items")
        .WithDescription("Applies additions, deletions, and updates to trip items in a single atomic transaction.")
        .Produces<List<TripItemResponse>>(StatusCodes.Status200OK)
        .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        return group;
    }

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

    public static bool IsSameCategory(string? sectionA, string? sectionB)
    {
        var catA = GetCategoryKey(sectionA);
        var catB = GetCategoryKey(sectionB);
        return catA != null && catA == catB;
    }

    public static string ResolveTripSection(string requestedSection, IEnumerable<string>? existingSections)
    {
        var trimmed = requestedSection?.Trim() ?? string.Empty;
        var existingList = existingSections?
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .Distinct()
            .ToList() ?? new List<string>();

        if (existingList.Count > 0)
        {
            var exactMatch = existingList.FirstOrDefault(s => string.Equals(s, trimmed, StringComparison.OrdinalIgnoreCase));
            if (exactMatch != null)
            {
                return exactMatch.ToLowerInvariant();
            }

            var catMatch = existingList.FirstOrDefault(s => IsSameCategory(s, trimmed));
            if (catMatch != null)
            {
                return catMatch.ToLowerInvariant();
            }
        }

        return trimmed.ToLowerInvariant();
    }

    private static async Task<bool> CheckIfTripArchivedAsync(
        IHttpClientFactory httpClientFactory,
        HttpContext httpContext,
        Guid tripId,
        CancellationToken ct)
    {
        try
        {
            var client = httpClientFactory.CreateClient("TripService");
            var authHeader = httpContext.Request.Headers.Authorization.ToString();
            if (!string.IsNullOrEmpty(authHeader))
            {
                client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", authHeader);
            }

            var trip = await client.GetFromJsonAsync<TripSummaryDto>($"/trips/{tripId}", ct);
            return trip?.IsArchived == true;
        }
        catch
        {
            return false;
        }
    }
}