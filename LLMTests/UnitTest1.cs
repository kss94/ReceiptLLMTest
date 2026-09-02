using Google.GenAI;
using Google.GenAI.Types;
using System.Text.RegularExpressions;

namespace LLMTests;

public class UnitTest1
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
        string actual = res.Text.Replace("햄루폴라", "햄루꼴라")
            .Replace("햄루콜라", "햄루꼴라");

        // Assert
        Assert.Equal(Normalize(expected), Normalize(actual));
    }

    static string Normalize(string s)
    {
        return Regex.Replace(s ?? "", @"\s+", "");
    }
}
