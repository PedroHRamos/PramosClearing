using Xunit;
using PramosClearing.OrderBook.Domain.ValueObjects;

namespace PramosClearing.OrderBook.Tests;

public sealed class QuantityTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    [InlineData(10000)]
    public void Constructor_AcceptsPositiveValues(int value)
    {
        var qty = new Quantity(value);
        Assert.Equal(value, qty.Value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_ThrowsOnNonPositiveValues(int value)
    {
        Assert.Throws<ArgumentException>(() => new Quantity(value));
    }

    [Fact]
    public void ImplicitConversion_ToInt()
    {
        var qty = new Quantity(500);
        int value = qty;
        Assert.Equal(500, value);
    }

    [Fact]
    public void ImplicitConversion_FromInt()
    {
        Quantity qty = 500;
        Assert.Equal(500, qty.Value);
    }
}
