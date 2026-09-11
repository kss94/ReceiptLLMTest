using Common;

namespace LLMTests;

public class ReceiptTests : TestBase
{
    public ReceiptTests(ITestOutputHelper output) : base(output)
    {
    }

    public static TheoryData<string> Cases(string vendorName) => [.. Of(vendorName).Cases()];

    [Theory]
    [MemberData(nameof(Cases), "Starbucks")]
    public Task Starbucks(string receiptId)
    {
        return RunCase("Starbucks", receiptId);
    }

    [Theory]
    [MemberData(nameof(Cases), "Megacoffee")]
    public Task Megacoffee(string receiptId)
    {
        return RunCase("Megacoffee", receiptId);
    }

    [Theory]
    [MemberData(nameof(Cases), "Baemin")]
    public Task Baemin(string receiptId)
    {
        return RunCase("Baemin", receiptId);
    }

    private async Task RunCase(string vendorName, string receiptId)
    {
        Vendor data = Of(vendorName);
        string systemPrompt = File.ReadAllText(data.SystemPromptPath);
        string userPrompt = File.ReadAllText(data.UserPromptPath);
        string expected = File.ReadAllText(data.AnswerPath(receiptId));

        // Act
        string actual = await _extractor.ExtractAsync(
            data.ReceiptDir(receiptId), systemPrompt, userPrompt, TestContext.Current.CancellationToken);

        // Assert
        var report = ReceiptDiff.Compare(expected, actual);
        _output.WriteLine($"점수 {report.Score:F3} ({report.Matched}/{report.Total})");
        _output.WriteLine($"리포트: {Save(vendorName, receiptId, actual, report)}");
        if (report.Fuzzy.Count > 0)
            _output.WriteLine(string.Join(Environment.NewLine, report.Fuzzy));

        Assert.True(report.Ok, $"{receiptId}: {report.Diffs.Count}건 불일치{Environment.NewLine}{string.Join(Environment.NewLine, report.Diffs)}");
    }
}
