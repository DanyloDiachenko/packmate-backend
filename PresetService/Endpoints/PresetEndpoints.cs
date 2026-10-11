using System.Security.Claims;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using PresetService.Data;
using PresetService.DTOs;
using PresetService.Entities;
using PresetService.Extensions;

namespace PresetService.Endpoints;

public static class PresetEndpoints
{
    public static RouteGroupBuilder MapPresetEndpoints(this RouteGroupBuilder group)
    {
        group.WithTags("Presets");

        group.MapGet("/", async (
            ClaimsPrincipal userClaims,
            PresetDbContext db,
            CancellationToken ct
        ) =>
        {
            var userId = userClaims.GetUserId();

            var presets = await db.Presets
                .AsNoTracking()
                .Where(p => p.UserId == userId)
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new PresetSummaryResponse(
                    p.Id,
                    p.UserId,
                    p.Name,
                    p.Description,
                    p.Items.Count,
                    p.CreatedAt,
                    p.UpdatedAt
                ))
                .ToListAsync(ct);

            return Results.Ok(presets);
        })
        .WithName("GetUserPresets")
        .WithSummary("Get all packing presets for the authenticated user")
        .Produces<List<PresetSummaryResponse>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapGet("/{id:guid}", async (
            Guid id,
            ClaimsPrincipal userClaims,
            PresetDbContext db,
            CancellationToken ct
        ) =>
        {
            var userId = userClaims.GetUserId();

            var preset = await db.Presets
                .AsNoTracking()
                .Include(p => p.Items)
                .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId, ct);

            if (preset == null)
            {
                return Results.NotFound(new ErrorResponse("Preset not found"));
            }

            return Results.Ok(ToPresetResponse(preset));
        })
        .WithName("GetPresetById")
        .WithSummary("Get a specific preset with grouped items by ID")
        .Produces<PresetResponse>(StatusCodes.Status200OK)
        .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/", async (
            CreatePresetRequest request,
            IValidator<CreatePresetRequest> validator,
            ClaimsPrincipal userClaims,
            PresetDbContext db,
            CancellationToken ct
        ) =>
        {
            var validationResult = await validator.ValidateAsync(request, ct);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            var userId = userClaims.GetUserId();

            var preset = new Preset
            {
                UserId = userId,
                Name = request.Name.Trim(),
                Description = request.Description?.Trim(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            var itemsToInsert = new List<PresetItem>();

            var totalPassedItems = request.Categories?.Values.Sum(list => list?.Count ?? 0) ?? 0;
            if (totalPassedItems == 0)
            {
                return Results.BadRequest(new ErrorResponse("Preset cannot be empty. Please include at least one item."));
            }

            foreach (var (sectionKey, dtoList) in request.Categories!)
            {
                if (dtoList == null) continue;
                var cleanSection = sectionKey.Trim().ToLowerInvariant();
                foreach (var itemDto in dtoList)
                {
                    itemsToInsert.Add(new PresetItem
                    {
                        Preset = preset,
                        Section = cleanSection,
                        Title = itemDto.Title.Trim(),
                        Quantity = itemDto.Quantity > 0 ? itemDto.Quantity : 1,
                        Tag = itemDto.Tag?.Trim()
                    });
                }
            }

            preset.Items = itemsToInsert;
            db.Presets.Add(preset);
            await db.SaveChangesAsync(ct);

            return Results.Created($"/presets/{preset.Id}", ToPresetResponse(preset));
        })
        .WithName("CreatePreset")
        .WithSummary("Create a new packing preset")
        .Produces<PresetResponse>(StatusCodes.Status201Created)
        .ProducesValidationProblem()
        .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPut("/{id:guid}", async (
            Guid id,
            UpdatePresetRequest request,
            IValidator<UpdatePresetRequest> validator,
            ClaimsPrincipal userClaims,
            PresetDbContext db,
            CancellationToken ct
        ) =>
        {
            var validationResult = await validator.ValidateAsync(request, ct);
            if (!validationResult.IsValid)
            {
                return Results.ValidationProblem(validationResult.ToDictionary());
            }

            var userId = userClaims.GetUserId();

            var preset = await db.Presets
                .Include(p => p.Items)
                .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId, ct);

            if (preset == null)
            {
                return Results.NotFound(new ErrorResponse("Preset not found"));
            }

            preset.Name = request.Name.Trim();
            preset.Description = request.Description?.Trim();
            preset.UpdatedAt = DateTime.UtcNow;

            if (request.Categories != null)
            {
                var totalPassedItems = request.Categories.Values.Sum(list => list?.Count ?? 0);
                if (totalPassedItems == 0)
                {
                    return Results.BadRequest(new ErrorResponse("Preset cannot be empty. Please include at least one item."));
                }

                db.PresetItems.RemoveRange(preset.Items);
                preset.Items.Clear();

                foreach (var (sectionKey, dtoList) in request.Categories)
                {
                    if (dtoList == null) continue;
                    var cleanSection = sectionKey.Trim().ToLowerInvariant();
                    foreach (var itemDto in dtoList)
                    {
                        preset.Items.Add(new PresetItem
                        {
                            PresetId = preset.Id,
                            Section = cleanSection,
                            Title = itemDto.Title.Trim(),
                            Quantity = itemDto.Quantity > 0 ? itemDto.Quantity : 1,
                            Tag = itemDto.Tag?.Trim()
                        });
                    }
                }
            }

            await db.SaveChangesAsync(ct);

            return Results.Ok(ToPresetResponse(preset));
        })
        .WithName("UpdatePreset")
        .WithSummary("Update preset details and replace its items/categories")
        .Produces<PresetResponse>(StatusCodes.Status200OK)
        .ProducesValidationProblem()
        .Produces<ErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapDelete("/{id:guid}", async (
            Guid id,
            ClaimsPrincipal userClaims,
            PresetDbContext db,
            CancellationToken ct
        ) =>
        {
            var userId = userClaims.GetUserId();

            var preset = await db.Presets
                .FirstOrDefaultAsync(p => p.Id == id && p.UserId == userId, ct);

            if (preset == null)
            {
                return Results.NotFound(new ErrorResponse("Preset not found"));
            }

            db.Presets.Remove(preset);
            await db.SaveChangesAsync(ct);

            return Results.NoContent();
        })
        .WithName("DeletePreset")
        .WithSummary("Delete a preset by ID")
        .Produces(StatusCodes.Status204NoContent)
        .Produces<ErrorResponse>(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        return group;
    }

    private static PresetResponse ToPresetResponse(Preset preset)
    {
        var groupedCategories = preset.Items
            .GroupBy(i => i.Section.Trim().ToLowerInvariant())
            .OrderBy(g => g.Key.Contains("misc") || g.Key.Contains("gear") ? 1 : 0)
            .ThenBy(g => g.Key)
            .ToDictionary(
                g => g.Key,
                g => g.Select(i => new PresetItemDto(
                    i.Id,
                    i.Section,
                    i.Title,
                    i.Quantity,
                    i.Tag
                )).ToList()
            );

        return new PresetResponse(
            preset.Id,
            preset.UserId,
            preset.Name,
            preset.Description,
            groupedCategories,
            preset.CreatedAt,
            preset.UpdatedAt
        );
    }
}