using FluentAssertions;
using Pennylane.Application.Extensions;

namespace Pennylane.Application.Tests.Extensions;
public class DateTimeExtensionsTests
{
    [Theory]
    [InlineData(null)]
    public void ConvertToLocalTime_Nullable_ShouldReturnNull(DateTime? utcDate)
    {
        var local = utcDate.ConvertToLocalTime();
        local.Should().BeNull();
    }

    [Fact]
    public void ConvertToLocalTime_ShouldMatchSystemLocalTimeZone()
    {
        // Arrange
        var utcDate = new DateTime(2023, 4, 6, 12, 0, 0, DateTimeKind.Utc); // UTC fixed
        var expected = TimeZoneInfo.ConvertTimeFromUtc(utcDate, TimeZoneInfo.Local); // conversion dynamique

        // Act
        var actual = utcDate.ConvertToLocalTime(); // m�thode d�extension

        // Assert
        actual.Should().Be(expected);
    }

    [Theory]
    [InlineData(2026, 6, 30, "30 juin 2026")]
    [InlineData(2026, 10, 31, "31 octobre 2026")]
    [InlineData(2025, 1, 1, "01 janvier 2025")]
    [InlineData(2024, 12, 25, "25 décembre 2024")]
    public void ToFrenchLongDate_ShouldFormatCorrectly(int year, int month, int day, string expected)
    {
        // Arrange
        DateTime? date = new DateTime(year, month, day);

        // Act
        var result = date.ToFrenchLongDate();

        // Assert
        result.Should().Be(expected);
    }
}
