using ClosedXML.Excel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PartFinder.Services;
using PartFinder.ViewModels;

namespace PartFinder.Tests.Services;

[TestClass]
public sealed class AiExportSaveServiceTests
{
    private static AiStructuredExportPayloadDto CreateSamplePayload(string format = "json_table") => new()
    {
        ExportType = "vendor_comparison",
        Format = format,
        FileNameSuggestion = format == "csv" ? "vendor-comparison.csv" : "vendor-comparison.xlsx",
        Columns =
        [
            new AiExportColumnDto { Key = "partIdentifier", Label = "Part Identifier" },
            new AiExportColumnDto { Key = "vendor", Label = "Vendor" },
            new AiExportColumnDto { Key = "price", Label = "Price" },
        ],
        Rows =
        [
            new Dictionary<string, string>
            {
                ["partIdentifier"] = "P1001",
                ["vendor"] = "Acme",
                ["price"] = "12.50",
            },
        ],
        RowCount = 1,
        Truncated = false,
    };

    [TestMethod]
    public async Task SaveExportAsync_WritesXlsxUsingClosedXmlExcelTemplateService()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"pf-ai-export-{Guid.NewGuid():N}.xlsx");
        try
        {
            var excelService = new ClosedXmlExcelTemplateService();
            var saveService = new AiExportSaveService(excelService);

            var result = await saveService.SaveExportAsync(
                CreateSamplePayload("json_table"),
                pickSavePathAsync: (_, _, _) => Task.FromResult<string?>(tempFile));

            Assert.AreEqual(AiExportSaveStatus.Saved, result.Status);
            Assert.AreEqual(tempFile, result.FilePath);
            Assert.AreEqual(1, result.RowCount);
            Assert.IsTrue(File.Exists(tempFile));

            using var workbook = new XLWorkbook(tempFile);
            var sheet = workbook.Worksheet("Data");
            Assert.AreEqual("Part Identifier", sheet.Cell(1, 1).GetString());
            Assert.AreEqual("P1001", sheet.Cell(2, 1).GetString());
            Assert.AreEqual("Acme", sheet.Cell(2, 2).GetString());
            Assert.AreEqual("12.50", sheet.Cell(2, 3).GetString());
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [TestMethod]
    public async Task SaveExportAsync_WritesFormulaSafeCsv_WhenCsvRequested()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"pf-ai-export-{Guid.NewGuid():N}.csv");
        try
        {
            var saveService = new AiExportSaveService(new ClosedXmlExcelTemplateService());
            var payload = CreateSamplePayload("csv");
            payload.Rows.Add(new Dictionary<string, string>
            {
                ["partIdentifier"] = "=CMD(\"calc\")",
                ["vendor"] = "+Vendor",
                ["price"] = "@SUM(A1)",
            });

            var result = await saveService.SaveExportAsync(
                payload,
                pickSavePathAsync: (_, _, _) => Task.FromResult<string?>(tempFile));

            Assert.AreEqual(AiExportSaveStatus.Saved, result.Status);
            var content = await File.ReadAllTextAsync(tempFile);
            StringAssert.Contains(content, "'=CMD(");
            StringAssert.Contains(content, "'+Vendor");
            StringAssert.Contains(content, "'@SUM(A1)");
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [TestMethod]
    public async Task SaveExportAsync_ReturnsCancelled_WhenUserCancelsSavePicker()
    {
        var saveService = new AiExportSaveService(new ClosedXmlExcelTemplateService());

        var result = await saveService.SaveExportAsync(
            CreateSamplePayload(),
            pickSavePathAsync: (_, _, _) => Task.FromResult<string?>(null));

        Assert.AreEqual(AiExportSaveStatus.Cancelled, result.Status);
        Assert.IsNull(result.FilePath);
    }

    [TestMethod]
    public async Task SaveExportAsync_ReturnsFailed_WhenPayloadHasNoColumns()
    {
        var saveService = new AiExportSaveService(new ClosedXmlExcelTemplateService());

        var result = await saveService.SaveExportAsync(new AiStructuredExportPayloadDto());

        Assert.AreEqual(AiExportSaveStatus.Failed, result.Status);
        Assert.IsFalse(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }

    [TestMethod]
    public async Task ChatbotViewModel_ExecuteExportActionsAsync_PostsSavedAndCancelledStatusMessages()
    {
        var state = new ChatbotStateService();
        var stubExport = new StubAiExportSaveService(
            new AiExportSaveResult(AiExportSaveStatus.Saved, @"C:\Exports\test.xlsx", 5),
            new AiExportSaveResult(AiExportSaveStatus.Cancelled));

        var vm = new ChatbotViewModel(
            state,
            new LocalMockChatbotResponder(),
            navigation: null,
            exportSaveService: stubExport);

        await vm.ExecuteExportActionsAsync([
            new AiUiActionDto
            {
                Type = "export",
                ExportPayload = CreateSamplePayload(),
            },
        ]);

        Assert.IsTrue(vm.Messages.Any(m => m.Text.Contains("Exported 5 row(s) to C:\\Exports\\test.xlsx.")));

        await vm.ExecuteExportActionsAsync([
            new AiUiActionDto
            {
                Type = "export",
                ExportPayload = CreateSamplePayload(),
            },
        ]);

        Assert.IsTrue(vm.Messages.Any(m => m.Text.Contains("Export cancelled. No file was saved.")));
    }

    private sealed class StubAiExportSaveService : IAiExportSaveService
    {
        private readonly Queue<AiExportSaveResult> _results;

        public StubAiExportSaveService(params AiExportSaveResult[] results)
        {
            _results = new Queue<AiExportSaveResult>(results);
        }

        public Task<AiExportSaveResult> SaveExportAsync(
            AiStructuredExportPayloadDto payload,
            Func<string, string, CancellationToken, Task<string?>>? pickSavePathAsync = null,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_results.Dequeue());
        }
    }
}
