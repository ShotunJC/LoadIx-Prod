using FluentAssertions;
using LoadIx.Domain.Common;
using LoadIx.Domain.Customers;

namespace LoadIx.UnitTests.Domain;

public sealed class DomainPrimitiveTests
{
    [Fact]
    public void MoneyPreservesSupportedDecimalPrecision()
    {
        var result = Money.Create(1234567890.1234m, "zar");

        result.IsSuccess.Should().BeTrue();
        result.Value.Amount.Should().Be(1234567890.1234m);
        result.Value.CurrencyCode.Should().Be("ZAR");
    }

    [Fact]
    public void CustomerRejectsInvalidPaymentTerms()
    {
        var address = Address.Create(
            "1 Test Road",
            null,
            "Test City",
            "Test Region",
            "0001",
            "ZA").Value;

        var result = Customer.Create(
            Guid.NewGuid(),
            TenantId.New(),
            "Test Customer (Pty) Ltd",
            "Test Customer",
            "TEST-001",
            null,
            366,
            address);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("customer.invalid_payment_terms");
    }

    [Fact]
    public void WeightRejectsMoreThanThreeDecimalPlaces()
    {
        var result = Weight.Create(1.2345m, WeightUnit.Kilograms);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("weight.invalid_precision");
    }
}
