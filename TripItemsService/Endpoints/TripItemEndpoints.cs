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

            var grouped = items
                .GroupBy(i => i.Section.ToLower().Trim())
                .OrderBy(g => g.Key.Contains("misc") || g.Key.Contains("gear") ? 1 : 0)
                .ThenBy(g => g.Key)
                .Select(g => new TripSectionGroupResponse(
                    Section: g.Key,
                    Items: g.Select(i => new TripItemResponse(
                        i.Id,
                        i.TripId,
                        i.Section,
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

            var item = new TripItem
            {
                TripId = tripId,
                Section = request.Section.ToLower().Trim(),
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

            var newItems = request.Items.Select(req => new TripItem
            {
                TripId = tripId,
                Section = req.Section.ToLower().Trim(),
                Title = req.Title.Trim(),
                Quantity = req.Quantity > 0 ? req.Quantity : 1,
                Tag = req.Tag,
                IsTaken = false,
                CreatedAt = DateTime.UtcNow
            }).ToList();

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

            if (!string.IsNullOrWhiteSpace(request.Section)) item.Section = request.Section.ToLower().Trim();
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

        return group;
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