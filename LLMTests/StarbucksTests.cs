using Common;
using Google.GenAI.Types;

namespace LLMTests;

public class StarbucksTests : TestBase
{
    public StarbucksTests(ITestOutputHelper output) : base(output)
    {
    }

    protected override string Vendor => "Starbucks";

    [Theory]
    [InlineData("22195")]
    [InlineData("22026")]
    [InlineData("22052")]
    [InlineData("22279")]
    [InlineData("22286")]
    [InlineData("22309")]
    [InlineData("22315")]
    [InlineData("22350")]
    [InlineData("22369")]
    [InlineData("22390")]
    [InlineData("22435")]
    [InlineData("22738")]
    [InlineData("22876")]
    [InlineData("22784546")]
    [InlineData("260503-235859769-I0972880")]
    [InlineData("260510-131001182-K0122596")]
    [InlineData("260517-164718136-P0042412")]
    [InlineData("260517-170519787-P0346872")]
    [InlineData("260517-224843885-H0120915")]
    [InlineData("260524-202859302-K1029150")]
    [InlineData("260529-170927864-I0038317")]
    [InlineData("260903-123832335-P0234467")]
    public async Task Starbucks(string receiptId)
    {
        string systemPrompt = System.IO.File.ReadAllText(Path.Combine(VendorDir, "SystemPrompt.txt"));
        string userPrompt = System.IO.File.ReadAllText(Path.Combine(VendorDir, "UserPrompt.txt"));

        string expected = System.IO.File.ReadAllText(Path.Combine(VendorDir, "Answers", $"{receiptId}.json"));

        Content content = CreateContent(userPrompt, receiptId);
        GenerateContentConfig config = CreateConfig(systemPrompt);

        // Act
        var res = await _client.Models.GenerateContentAsync(AIMODEL, content, config, TestContext.Current.CancellationToken);
        Assert.NotNull(res.Text);
        string actual = res.Text;

        // Assert
        var report = ReceiptDiff.Compare(expected, actual);
        _output.WriteLine($"리포트: {Save(receiptId, actual, report)}");
        if (report.Fuzzy.Count > 0)
            _output.WriteLine(string.Join(System.Environment.NewLine, report.Fuzzy));

        Assert.True(report.Ok, $"{receiptId}: {report.Diffs.Count}건 불일치{System.Environment.NewLine}{string.Join(System.Environment.NewLine, report.Diffs)}");
    }
}
