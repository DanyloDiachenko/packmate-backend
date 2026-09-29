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
        });

        group.MapPost("/{tripId:guid}", async (
            Guid tripId,
            CreateTripItemRequest request,
            IValidator<CreateTripItemRequest> validator,
            TripItemsDbContext db
        ) =>
        {
            var validationResult = await validator.ValidateAsync(request);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
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
            await db.SaveChangesAsync();

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
        });

        group.MapPost("/{tripId:guid}/bulk", async (
            Guid tripId,
            BulkCreateTripItemsRequest request,
            TripItemsDbContext db
        ) =>
        {
            if (request.Items == null || request.Items.Count == 0)
            {
                return Results.BadRequest(new { message = "Items list cannot be empty" });
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
            await db.SaveChangesAsync();

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
        });

        group.MapPatch("/{id:guid}", async (
            Guid id,
            UpdateTripItemRequest request,
            IValidator<UpdateTripItemRequest> validator,
            TripItemsDbContext db
        ) =>
        {
            var validationResult = await validator.ValidateAsync(request);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            var item = await db.TripItems.FindAsync(id);
            if (item == null)
            {
                return Results.NotFound(new { message = "Trip item not found" });
            }

            if (!string.IsNullOrWhiteSpace(request.Section)) item.Section = request.Section.ToLower().Trim();
            if (!string.IsNullOrWhiteSpace(request.Title)) item.Title = request.Title.Trim();
            if (request.Quantity.HasValue) item.Quantity = request.Quantity.Value;
            if (request.IsTaken.HasValue) item.IsTaken = request.IsTaken.Value;
            if (request.Tag != null) item.Tag = request.Tag;

            await db.SaveChangesAsync();

            return Results.Ok(new TripItemResponse(
                item.Id,
                item.TripId,
                item.Section,
                item.Title,
                item.Quantity,
                item.IsTaken,
                item.Tag
            ));
        });

        group.MapDelete("/{id:guid}", async (
            Guid id,
            TripItemsDbContext db
        ) =>
        {
            var item = await db.TripItems.FindAsync(id);
            if (item == null)
            {
                return Results.NotFound(new { message = "Trip item not found" });
            }

            db.TripItems.Remove(item);
            await db.SaveChangesAsync();

            return Results.NoContent();
        });

        return group;
    }
}