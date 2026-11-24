using Microsoft.VisualStudio.TestTools.UnitTesting;
using PartFinder.Converters;

namespace PartFinder.Tests.Converters;

[TestClass]
public sealed class DictionaryValueConverterTests
{
    private readonly DictionaryValueConverter _converter = new();

    private static readonly Dictionary<string, object?> Values = new()
    {
        ["part_no"] = "PN-1",
        ["price"] = 9.5m,
        ["empty"] = null,
    };

    [TestMethod]
    public void ExistingKey_ReturnsValue()
    {
        Assert.AreEqual("PN-1", _converter.Convert(Values, typeof(object), "part_no", "en"));
        Assert.AreEqual(9.5m, _converter.Convert(Values, typeof(object), "price", "en"));
    }

    [TestMethod]
    public void ReadOnlyDictionary_IsSupported()
    {
        IReadOnlyDictionary<string, object?> ro = new Dictionary<string, object?> { ["k"] = 1 };

        Assert.AreEqual(1, _converter.Convert(ro, typeof(object), "k", "en"));
    }

    [TestMethod]
    public void MissingKeyOrNullValue_ReturnsNull()
    {
        Assert.IsNull(_converter.Convert(Values, typeof(object), "missing", "en"));
        Assert.IsNull(_converter.Convert(Values, typeof(object), "empty", "en"));
    }

    [TestMethod]
    public void NonStringParameterOrNonDictionary_ReturnsNull()
    {
        Assert.IsNull(_converter.Convert(Values, typeof(object), 5, "en"));
        Assert.IsNull(_converter.Convert("not a dictionary", typeof(object), "part_no", "en"));
    }

    [TestMethod]
    public void ConvertBack_ReturnsValueUnchanged()
    {
        Assert.AreEqual("x", _converter.ConvertBack("x", typeof(object), "part_no", "en"));
    }
}
