namespace StockInvestment.Domain.ValueObjects;

public class Price
{
    public decimal Value { get; private set; }

    private Price(decimal value)
    {
        if (value < 0)
            throw new ArgumentException("Price cannot be negative", nameof(value));

        Value = value;
    }

    public static Price Create(decimal value)
    {
        return new Price(value);
    }

    public override string ToString() => Value.ToString("N2");

    public override bool Equals(object? obj)
    {
        if (obj is Price other)
            return Value == other.Value;
        return false;
    }

    public override int GetHashCode() => Value.GetHashCode();
}

