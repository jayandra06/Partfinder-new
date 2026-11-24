using Microsoft.VisualStudio.TestTools.UnitTesting;
using PartFinder.Services;

namespace PartFinder.Tests.Services;

[TestClass]
public sealed class WorksheetRelationBidirectionalTests
{
    private static RelationMatchPairDto Pair(string source, string target) =>
        new() { SourceColumn = source, TargetColumn = target };

    private static WorksheetRelationDto Relation(
        string id,
        string primary,
        string lookup,
        string trigger,
        params RelationMatchPairDto[] keys) => new()
    {
        Id = id,
        PrimaryTemplateId = primary,
        LookupTemplateId = lookup,
        Name = id,
        TriggerColumn = trigger,
        MatchKeys = keys.ToList(),
    };

    [TestMethod]
    public void BuildReverseRequest_SwapsTemplatesAndMirrorsKeys()
    {
        var forward = new SaveWorksheetRelationRequest
        {
            PrimaryTemplateId = "parts",
            LookupTemplateId = "vendors",
            TriggerColumn = "Vendor",
            MatchKeys = [Pair("Vendor", "Name"), Pair("vendor", "Name"), Pair("Region", "Region")],
            DisplayColumns = ["Phone"],
        };

        var reverse = WorksheetRelationBidirectional.BuildReverseRequest(forward, "Parts", "Vendors");

        Assert.AreEqual("vendors", reverse.PrimaryTemplateId);
        Assert.AreEqual("parts", reverse.LookupTemplateId);
        Assert.AreEqual("Vendors to Parts", reverse.Name);
        Assert.AreEqual("Parts info", reverse.MenuLabel);
        Assert.AreEqual("Vendor", reverse.TriggerColumn);
        CollectionAssert.AreEqual(new[] { "Vendor", "Region" }, reverse.MatchKeys.Select(k => k.SourceColumn).ToArray());
        Assert.IsTrue(reverse.MatchKeys.All(k => k.SourceColumn == k.TargetColumn));
        CollectionAssert.AreEqual(new[] { "Vendor", "Region" }, reverse.DisplayColumns);
    }

    [TestMethod]
    public void BuildReverseRequest_NoMatchKeys_FallsBackToForwardTriggerAndDisplay()
    {
        var forward = new SaveWorksheetRelationRequest
        {
            PrimaryTemplateId = "a",
            LookupTemplateId = "b",
            TriggerColumn = "SKU",
            DisplayColumns = ["Price", "Stock"],
        };

        var reverse = WorksheetRelationBidirectional.BuildReverseRequest(forward, "A", "B");

        Assert.AreEqual("SKU", reverse.TriggerColumn);
        Assert.AreEqual(0, reverse.MatchKeys.Count);
        CollectionAssert.AreEqual(new[] { "Price", "Stock" }, reverse.DisplayColumns);
        Assert.AreNotSame(forward.DisplayColumns, reverse.DisplayColumns);
    }

    [TestMethod]
    public void EnsureMatchKeysFromTrigger_EmptyKeys_CreatesTrimmedTriggerPair()
    {
        var relation = Relation("r", "a", "b", "  Part No ");

        WorksheetRelationBidirectional.EnsureMatchKeysFromTrigger(relation);

        Assert.AreEqual(1, relation.MatchKeys.Count);
        Assert.AreEqual("Part No", relation.MatchKeys[0].SourceColumn);
        Assert.AreEqual("Part No", relation.MatchKeys[0].TargetColumn);
    }

    [TestMethod]
    public void EnsureMatchKeysFromTrigger_AppendsTriggerWhenMissing()
    {
        var relation = Relation("r", "a", "b", "SKU", Pair("Brand", "Brand"));

        WorksheetRelationBidirectional.EnsureMatchKeysFromTrigger(relation);

        CollectionAssert.AreEqual(new[] { "Brand", "SKU" }, relation.MatchKeys.Select(k => k.SourceColumn).ToArray());
    }

    [TestMethod]
    public void EnsureMatchKeysFromTrigger_TriggerAlreadyPresent_CaseInsensitive_NoChange()
    {
        var relation = Relation("r", "a", "b", "sku", Pair("SKU", "Code"));

        WorksheetRelationBidirectional.EnsureMatchKeysFromTrigger(relation);

        Assert.AreEqual(1, relation.MatchKeys.Count);
    }

    [TestMethod]
    public void EnsureMatchKeysFromTrigger_BlankTrigger_NoChange()
    {
        var relation = Relation("r", "a", "b", " ");

        WorksheetRelationBidirectional.EnsureMatchKeysFromTrigger(relation);

        Assert.AreEqual(0, relation.MatchKeys.Count);
    }

    [TestMethod]
    public void MirrorAsPrimary_ReversesPairsAndMarksId()
    {
        var relation = Relation("rel1", "parts", "vendors", "VendorCode", Pair("VendorCode", "Code"), Pair("Region", "Area"));
        relation.Name = "Parts to Vendors";

        var mirror = WorksheetRelationBidirectional.MirrorAsPrimary(relation, "vendors");

        Assert.AreEqual("rel1:mirror", mirror.Id);
        Assert.AreEqual("vendors", mirror.PrimaryTemplateId);
        Assert.AreEqual("parts", mirror.LookupTemplateId);
        Assert.AreEqual("Parts to Vendors (reverse)", mirror.Name);
        Assert.AreEqual(string.Empty, mirror.MenuLabel);
        CollectionAssert.AreEqual(new[] { "Code", "Area" }, mirror.MatchKeys.Select(k => k.SourceColumn).ToArray());
        CollectionAssert.AreEqual(new[] { "VendorCode", "Region" }, mirror.MatchKeys.Select(k => k.TargetColumn).ToArray());
        Assert.AreEqual("Code", mirror.TriggerColumn);
        CollectionAssert.AreEqual(new[] { "VendorCode", "Region" }, mirror.DisplayColumns);
    }

    [TestMethod]
    public void MirrorAsPrimary_NoKeys_UsesTriggerAndDisplayColumns()
    {
        var relation = Relation("rel2", "a", "b", "SKU");
        relation.DisplayColumns = ["Price"];

        var mirror = WorksheetRelationBidirectional.MirrorAsPrimary(relation, "b");

        Assert.AreEqual("SKU", mirror.TriggerColumn);
        Assert.AreEqual(1, mirror.MatchKeys.Count);
        CollectionAssert.AreEqual(new[] { "Price" }, mirror.DisplayColumns);
    }

    [TestMethod]
    public void GetOutboundRelations_BlankIdOrNoRelations_ReturnsEmpty()
    {
        Assert.AreEqual(0, WorksheetRelationBidirectional.GetOutboundRelations(" ", [Relation("r", "a", "b", "x")]).Count);
        Assert.AreEqual(0, WorksheetRelationBidirectional.GetOutboundRelations("a", []).Count);
    }

    [TestMethod]
    public void GetOutboundRelations_IncludesOwnedAndMirroredInbound()
    {
        var owned = Relation("r1", "T1", "T2", "Part No");
        var inbound = Relation("r2", "T3", "T1", "SKU", Pair("SKU", "Code"));

        var outbound = WorksheetRelationBidirectional.GetOutboundRelations("t1", [owned, inbound]);

        Assert.AreEqual(2, outbound.Count);
        Assert.AreEqual("r1", outbound[0].Id);
        Assert.AreEqual("r2:mirror", outbound[1].Id);
        Assert.AreEqual("T3", outbound[1].LookupTemplateId);
    }

    [TestMethod]
    public void GetOutboundRelations_ExplicitReverseIsMergedNotDuplicated()
    {
        var forward = Relation("r1", "T1", "T2", "Part No");
        var reverse = Relation("r2", "T2", "T1", "SKU", Pair("SKU", "SKU"));

        var outbound = WorksheetRelationBidirectional.GetOutboundRelations("T1", [forward, reverse]);

        Assert.AreEqual(1, outbound.Count);
        CollectionAssert.AreEqual(
            new[] { "Part No", "SKU" },
            outbound[0].MatchKeys.Select(k => k.SourceColumn).ToArray());
        Assert.AreEqual(0, forward.MatchKeys.Count, "input relation must not be mutated");
    }

    [TestMethod]
    public void GetMatchKeyColumnLabels_CollectsTriggerAndBothSidesOnce()
    {
        var relation = Relation("r", "a", "b", "Brand", Pair("brand", "Make"), Pair(" Model ", ""));

        var labels = WorksheetRelationBidirectional.GetMatchKeyColumnLabels(relation);

        CollectionAssert.AreEquivalent(new[] { "Brand", "Make", "Model" }, labels.ToArray());
    }

    [TestMethod]
    public void FindRelationBetween_IsDirectionalAndCaseInsensitive()
    {
        var relation = Relation("r", "Parts", "Vendors", "x");
        WorksheetRelationDto[] all = [relation];

        Assert.AreSame(relation, WorksheetRelationBidirectional.FindRelationBetween(all, "parts", "VENDORS"));
        Assert.IsNull(WorksheetRelationBidirectional.FindRelationBetween(all, "Vendors", "Parts"));
    }
}
