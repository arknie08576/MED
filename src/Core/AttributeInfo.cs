using System.Collections.Generic;

namespace MED.Core;

public sealed class AttributeInfo
{
    public string Name { get; }
    public AttributeType Type { get; }

    // Numeric stats (filled in DistanceContext)
    public double Min { get; set; }
    public double Max { get; set; }
    public double Range { get; set; }

    // Nominal domain (filled by loader)
    public HashSet<string> NominalValues { get; } = new(StringComparer.Ordinal);

    public AttributeInfo(string name, AttributeType type)
    {
        Name = name;
        Type = type;
    }
}
