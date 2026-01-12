using System.Collections.Generic;

namespace MED.Core;

public sealed class Dataset
{
    public IReadOnlyList<AttributeInfo> Attributes { get; }
    public IReadOnlyList<Record> Records { get; }

    public int FeatureCount => Attributes.Count;
    public int Count => Records.Count;

    public Dataset(IReadOnlyList<AttributeInfo> attributes, IReadOnlyList<Record> records)
    {
        Attributes = attributes;
        Records = records;
    }
}
