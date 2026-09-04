using Common;

namespace LLMTests;

public class ReceiptTests : TestBase
{
    public ReceiptTests(ITestOutputHelper output) : base(output)
    {
    }

    protected override string VendorName => "Starbucks";

    /// Starbucks/Answers/*.json에서 읽는다. 정답을 추가하면 케이스가 저절로 늘어난다.
    public static TheoryData<string> Cases => [.. Of("Starbucks").Cases()];

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Starbucks(string receiptId)
    {
        Vendor data = Data;
        string systemPrompt = File.ReadAllText(data.SystemPromptPath);
        string userPrompt = File.ReadAllText(data.UserPromptPath);
        string expected = File.ReadAllText(data.AnswerPath(receiptId));

        // Act
        string actual = await _extractor.ExtractAsync(
            data.ReceiptDir(receiptId), systemPrompt, userPrompt, TestContext.Current.CancellationToken);

        // Assert
        var report = ReceiptDiff.Compare(expected, actual);
        _output.WriteLine($"점수 {report.Score:F3} ({report.Matched}/{report.Total})");
        _output.WriteLine($"리포트: {Save(receiptId, actual, report)}");
        if (report.Fuzzy.Count > 0)
            _output.WriteLine(string.Join(Environment.NewLine, report.Fuzzy));

        Assert.True(report.Ok, $"{receiptId}: {report.Diffs.Count}건 불일치{Environment.NewLine}{string.Join(Environment.NewLine, report.Diffs)}");
    }
}
