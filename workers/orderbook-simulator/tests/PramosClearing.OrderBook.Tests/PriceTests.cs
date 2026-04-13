using Xunit;
using PramosClearing.OrderBook.Domain.ValueObjects;

namespace PramosClearing.OrderBook.Tests;

public sealed class PriceTests
{
    [Theory]
    [InlineData(0.01)]
    [InlineData(1.00)]
    [InlineData(999.99)]
    public void Constructor_AcceptsPositiveValues(double value)
    {
        var price = new Price((decimal)value);
        Assert.Equal((decimal)value, price.Value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_ThrowsOnNonPositiveValues(double value)
    {
        Assert.Throws<ArgumentException>(() => new Price((decimal)value));
    }

    [Fact]
    public void ImplicitConversion_ToDecimal()
    {
        var price = new Price(100m);
        decimal value = price;
        Assert.Equal(100m, value);
    }

    [Fact]
    public void ImplicitConversion_FromDecimal()
    {
        Price price = 100m;
        Assert.Equal(100m, price.Value);
    }
}
