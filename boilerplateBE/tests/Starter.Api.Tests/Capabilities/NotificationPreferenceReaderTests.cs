using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Starter.Domain.Common;
using Starter.Infrastructure.Capabilities;
using Starter.Infrastructure.Persistence;
using Xunit;

namespace Starter.Api.Tests.Capabilities;

/// <summary>
/// Verifies that <see cref="NotificationPreferenceReaderService"/> honours the
/// opt-out semantic: missing row → enabled; explicit false → disabled; explicit
/// true → enabled.
/// </summary>
public sealed class NotificationPreferenceReaderTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly NotificationPreferenceReaderService _sut;

    public NotificationPreferenceReaderTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _context = new ApplicationDbContext(options);
        _sut = new NotificationPreferenceReaderService(_context);
    }

    public void Dispose() => _context.Dispose();

    [Fact]
    public async Task IsEmailEnabledAsync_NoPreferenceRow_ReturnsTrue()
    {
        // Arrange — no row seeded for this user/type
        var userId = Guid.NewGuid();

        // Act
        var result = await _sut.IsEmailEnabledAsync(userId, "mention");

        // Assert — opt-out default: no preference row means enabled
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsEmailEnabledAsync_ExplicitlyDisabled_ReturnsFalse()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var pref = NotificationPreference.Create(userId, "mention", emailEnabled: false);
        _context.Set<NotificationPreference>().Add(pref);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.IsEmailEnabledAsync(userId, "mention");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsEmailEnabledAsync_ExplicitlyEnabled_ReturnsTrue()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var pref = NotificationPreference.Create(userId, "mention", emailEnabled: true);
        _context.Set<NotificationPreference>().Add(pref);
        await _context.SaveChangesAsync();

        // Act
        var result = await _sut.IsEmailEnabledAsync(userId, "mention");

        // Assert
        result.Should().BeTrue();
    }
}
