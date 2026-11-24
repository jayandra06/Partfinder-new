using System.ComponentModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PartFinder.Models;

namespace PartFinder.Tests.Models;

[TestClass]
public sealed class CellConnectionTests
{
    [TestMethod]
    public void NewConnection_HasDefaultLabelAndUniqueId()
    {
        var a = new CellConnection();
        var b = new CellConnection();

        Assert.AreEqual("Group", a.Label);
        Assert.AreEqual(32, a.Id.Length);
        Assert.IsTrue(a.Id.All(Uri.IsHexDigit));
        Assert.AreNotEqual(a.Id, b.Id);
    }

    [TestMethod]
    public void SettingLabel_RaisesPropertyChanged()
    {
        var connection = new CellConnection();
        var changed = new List<string?>();
        ((INotifyPropertyChanged)connection).PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        connection.Label = "Brakes";

        Assert.AreEqual("Brakes", connection.Label);
        CollectionAssert.AreEqual(new[] { nameof(CellConnection.Label) }, changed);
    }

    [TestMethod]
    public void SettingSameLabel_DoesNotRaisePropertyChanged()
    {
        var connection = new CellConnection { Label = "Same" };
        var raised = 0;
        connection.PropertyChanged += (_, _) => raised++;

        connection.Label = "Same";

        Assert.AreEqual(0, raised);
    }

    [TestMethod]
    public void PortsAndKeys_RoundTrip()
    {
        var connection = new CellConnection
        {
            SourceCellIndex = 1,
            TargetCellIndex = 4,
            SourceSide = ConnectionPortSide.Right,
            TargetSide = ConnectionPortSide.Left,
            SourceCellKey = "brand",
            TargetCellKey = "model",
        };

        Assert.AreEqual(1, connection.SourceCellIndex);
        Assert.AreEqual(4, connection.TargetCellIndex);
        Assert.AreEqual(ConnectionPortSide.Right, connection.SourceSide);
        Assert.AreEqual(ConnectionPortSide.Left, connection.TargetSide);
        Assert.AreEqual("brand", connection.SourceCellKey);
        Assert.AreEqual("model", connection.TargetCellKey);
    }
}
