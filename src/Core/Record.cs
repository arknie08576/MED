namespace MED.Core;

/// <summary>
/// X[i] is:
/// - double? when attribute i is Numeric
/// - string? when attribute i is Nominal
/// </summary>
public sealed class Record
{
    public int Id { get; }
    public object?[] X { get; }
    public string Label { get; }

    public Record(int id, object?[] x, string label)
    {
        Id = id;
        X = x;
        Label = label;
    }
}
