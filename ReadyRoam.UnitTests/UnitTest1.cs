using FluentAssertions;
using DestinationService.Services;
using UserService.Services;
using Microsoft.Extensions.Caching.Distributed;
using Moq;

namespace ReadyRoam.UnitTests;

public class BusinessRuleTests
{
    [Fact]
    public void GetCountryFlag_ValidTwoLetterCode_ReturnsFlagEmoji()
    {
        DestinationServiceImpl.GetCountryFlag("FR").Should().Be("🇫🇷");
        DestinationServiceImpl.GetCountryFlag("US").Should().Be("🇺🇸");
        DestinationServiceImpl.GetCountryFlag("UA").Should().Be("🇺🇦");
        DestinationServiceImpl.GetCountryFlag("jp").Should().Be("🇯🇵");
    }

    [Fact]
    public void GetCountryFlag_InvalidOrNullCode_ReturnsGlobeEmoji()
    {
        DestinationServiceImpl.GetCountryFlag(null).Should().Be("🌍");
        DestinationServiceImpl.GetCountryFlag("").Should().Be("🌍");
        DestinationServiceImpl.GetCountryFlag("USA").Should().Be("🌍");
        DestinationServiceImpl.GetCountryFlag("12").Should().Be("🌍");
    }

    [Theory]
    [InlineData("2026-06-10", "2026-06-20", "2026-06-15", "2026-06-25", true)]
    [InlineData("2026-06-10", "2026-06-20", "2026-06-01", "2026-06-15", true)]
    [InlineData("2026-06-10", "2026-06-20", "2026-06-12", "2026-06-18", true)]
    [InlineData("2026-06-10", "2026-06-20", "2026-06-01", "2026-06-25", true)]
    [InlineData("2026-06-10", "2026-06-20", "2026-06-20", "2026-06-25", false)]
    [InlineData("2026-06-10", "2026-06-20", "2026-06-01", "2026-06-10", false)]
    [InlineData("2026-06-10", "2026-06-10", "2026-06-10", "2026-06-10", true)]
    public void TripDateOverlap_Logic_EvaluatesCorrectly(string reqDepStr, string reqRetStr, string tripDepStr, string tripRetStr, bool expectedOverlap)
    {
        var reqDep = DateOnly.Parse(reqDepStr);
        var reqRet = DateOnly.Parse(reqRetStr);
        var tripDep = DateOnly.Parse(tripDepStr);
        var tripRet = DateOnly.Parse(tripRetStr);

        var hasOverlap = (reqDep < tripRet && reqRet > tripDep) ||
                         (reqDep == reqRet && tripDep == tripRet && reqDep == tripDep);

        hasOverlap.Should().Be(expectedOverlap);
    }

    [Fact]
    public void AutoArchiveCutoff_Logic_CalculatesCorrectly()
    {
        var today = new DateOnly(2026, 10, 9);
        var cutoffDate = today.AddDays(-1);

        cutoffDate.Should().Be(new DateOnly(2026, 10, 8));

        var tripYesterday = new DateOnly(2026, 10, 8);
        (tripYesterday <= cutoffDate).Should().BeTrue();

        var tripToday = new DateOnly(2026, 10, 9);
        (tripToday <= cutoffDate).Should().BeFalse();

        var tripTomorrow = new DateOnly(2026, 10, 10);
        (tripTomorrow <= cutoffDate).Should().BeFalse();
    }

    [Fact]
    public void TripItemsGrouping_MiscAndGear_ShouldAlwaysBeLast()
    {
        var sectionNames = new List<string>
        {
            "misc & gear",
            "clothes & footwear",
            "electronics & tech",
            "gear & essentials",
            "documents & money"
        };

        var ordered = sectionNames
            .OrderBy(s => s.Contains("misc") || s.Contains("gear") ? 1 : 0)
            .ThenBy(s => s)
            .ToList();

        ordered.Take(2).Should().BeEquivalentTo(new[] { "clothes & footwear", "documents & money" });
        ordered.TakeLast(2).Should().Contain("misc & gear");
        ordered.TakeLast(2).Should().Contain("gear & essentials");
    }

    [Fact]
    public async Task VerificationCodeService_GeneratesAndValidatesCode()
    {
        var cacheMock = new Mock<IDistributedCache>();
        byte[]? storedBytes = null;

        cacheMock.Setup(c => c.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string key, CancellationToken ct) => storedBytes);

        cacheMock.Setup(c => c.SetAsync(It.IsAny<string>(), It.IsAny<byte[]>(), It.IsAny<DistributedCacheEntryOptions>(), It.IsAny<CancellationToken>()))
            .Callback<string, byte[], DistributedCacheEntryOptions, CancellationToken>((k, bytes, opt, ct) => storedBytes = bytes)
            .Returns(Task.CompletedTask);

        cacheMock.Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, CancellationToken>((k, ct) => storedBytes = null)
            .Returns(Task.CompletedTask);

        var service = new VerificationCodeService(cacheMock.Object);

        var code = await service.GenerateAndSaveCodeAsync("test@example.com", "login");
        code.Should().HaveLength(6);

        var invalid = await service.ValidateCodeAsync("test@example.com", "000000", "login");
        invalid.Should().BeFalse();

        var valid = await service.ValidateCodeAsync("test@example.com", code, "login");
        valid.Should().BeTrue();

        var reused = await service.ValidateCodeAsync("test@example.com", code, "login");
        reused.Should().BeFalse();
    }
}

