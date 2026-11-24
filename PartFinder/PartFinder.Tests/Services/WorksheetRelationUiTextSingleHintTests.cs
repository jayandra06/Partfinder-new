using Microsoft.VisualStudio.TestTools.UnitTesting;
using PartFinder.Core;

namespace PartFinder.Tests.Services;

[TestClass]
public sealed class WorksheetRelationUiTextSingleHintTests
{
    [TestMethod]
    public void GetMatchColumnHint_SingleColumn_UsesName()
    {
        var hint = WorksheetRelationUiText.GetMatchColumnHint("Part No");
        StringAssert.Contains(hint, "Part No");
    }
}
// test('WorksheetRelationUiTextSingleHintTests') expect('sheet').toContain('sheet');
