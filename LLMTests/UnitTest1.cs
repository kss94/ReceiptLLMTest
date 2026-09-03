using Google.GenAI;
using Google.GenAI.Types;

namespace LLMTests;

public class UnitTest1(ITestOutputHelper output)
{
    const string APIKEY = "AQ.Ab8RN6Lr0pUVMpIW9HlA-q25iJbKWg07SExdnrziPrj1FURubQ";
    const string AIMODEL = "gemini-3.5-flash-lite";
    private readonly string[] _exts = [".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp"];
    private readonly Client _client = new(apiKey: APIKEY, httpOptions: new HttpOptions
    {
        RetryOptions = new HttpRetryOptions()
    });

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
    [InlineData("22784546")]
    [InlineData("260503-235859769-I0972880")]
    [InlineData("260510-131001182-K0122596")]
    [InlineData("260517-164718136-P0042412")]
    [InlineData("260517-170519787-P0346872")]
    [InlineData("260517-224843885-H0120915")]
    [InlineData("260524-202859302-K1029150")]
    [InlineData("260529-170927864-I0038317")]
    public async Task Starbucks(string name)
    {
        string systemPrompt = System.IO.File.ReadAllText("StarbucksSystemPrompt.txt");
        string userPrompt = System.IO.File.ReadAllText("StarbucksUserPrompt.txt");

        string expected = System.IO.File.ReadAllText($"Answers\\{name}.json");

        var s = Directory.EnumerateFiles($"스타벅스\\{name}")
            .Where(f => _exts.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .ToDictionary(f => Path.GetFileName(f), System.IO.File.ReadAllBytes);

        List<Part> parts = [Part.FromText(userPrompt)];
        foreach ((string name2, byte[] buffer) in s)
        {
            parts.Add(Part.FromBytes(buffer, MimeTypes.GetMimeType(name2)));
        }
        var content = new Content { Parts = parts };

        var config = new GenerateContentConfig
        {
            SystemInstruction = new Content
            {
                Parts = [new Part { Text = systemPrompt }]
            },
            ResponseMimeType = "application/json",
            ThinkingConfig = new ThinkingConfig
            {
                ThinkingLevel = ThinkingLevel.Minimal
            }
        };

        // Act
        var res = await _client.Models.GenerateContentAsync(AIMODEL, content, config, TestContext.Current.CancellationToken);
        Assert.NotNull(res.Text);
        string actual = res.Text;

        // Assert
        var report = ReceiptDiff.Compare(expected, actual);
        output.WriteLine($"리포트: {Save(name, actual, report)}");
        if (report.Fuzzy.Count > 0) output.WriteLine(string.Join(System.Environment.NewLine, report.Fuzzy));
        Assert.True(report.Ok, $"{name}: {report.Diffs.Count}건 불일치{System.Environment.NewLine}{string.Join(System.Environment.NewLine, report.Diffs)}");
    }

    /// 응답 원본과 차이 목록을 출력 폴더에 남긴다. 프롬프트를 고칠 때 이 파일들만 보면 된다.
    static string Save(string name, string actual, ReceiptDiff.Report report)
    {
        string dir = Path.Combine(AppContext.BaseDirectory, "Diffs");
        Directory.CreateDirectory(dir);

        System.IO.File.WriteAllText(Path.Combine(dir, $"{name}.actual.json"), actual);

        string diffPath = Path.Combine(dir, $"{name}.diff.txt");
        string body = report.ToString();
        if (body.Length == 0) System.IO.File.Delete(diffPath);
        else System.IO.File.WriteAllText(diffPath, body);

        return dir;
    }
}
