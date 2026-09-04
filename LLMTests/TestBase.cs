using Common;
using Google.GenAI;
using Google.GenAI.Types;
using System.Runtime.CompilerServices;

namespace LLMTests;

public abstract class TestBase
{
    protected const string APIKEY = "AQ.Ab8RN6Lr0pUVMpIW9HlA-q25iJbKWg07SExdnrziPrj1FURubQ";
    protected const string AIMODEL = "gemini-3.5-flash-lite";
    protected readonly ITestOutputHelper _output;
    protected readonly string[] _exts = [".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp"];
    protected readonly Client _client = new(apiKey: APIKEY, httpOptions: new HttpOptions
    {
        RetryOptions = new HttpRetryOptions()
    });

    public TestBase(ITestOutputHelper output)
    {
        _output = output;
    }

    /// 벤더 폴더 이름. 하위 클래스가 지정한다. ("Starbucks" -> LLMTests/Starbucks/)
    protected abstract string Vendor { get; }

    /// 그 벤더의 프롬프트·정답·영수증 이미지가 전부 이 폴더 아래에 있다.
    protected string VendorDir => Path.Combine(ProjectDir(), Vendor);

    protected Content CreateContent(string userPrompt, string receiptId)
    {
        var images = Directory.EnumerateFiles(Path.Combine(VendorDir, "Receipts", receiptId))
            .Where(f => _exts.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .ToDictionary(f => Path.GetFileName(f), System.IO.File.ReadAllBytes);

        List<Part> parts = [Part.FromText(userPrompt)];
        foreach ((string fileName, byte[] buffer) in images)
        {
            parts.Add(Part.FromBytes(buffer, MimeTypes.GetMimeType(fileName)));
        }
        return new Content { Parts = parts };
    }

    protected static GenerateContentConfig CreateConfig(string systemPrompt)
    {
        return new GenerateContentConfig
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
    }

    /// 응답 원본과 차이 목록을 출력 폴더에 남긴다. 프롬프트를 고칠 때 이 파일들만 보면 된다.
    protected static string Save(string receiptId, string actual, ReceiptDiff.Report report)
    {
        string dir = Path.Combine(AppContext.BaseDirectory, "Diffs");
        Directory.CreateDirectory(dir);

        System.IO.File.WriteAllText(Path.Combine(dir, $"{receiptId}.actual.json"), actual);

        string diffPath = Path.Combine(dir, $"{receiptId}.diff.txt");
        string body = report.ToString();
        if (body.Length == 0) System.IO.File.Delete(diffPath);
        else System.IO.File.WriteAllText(diffPath, body);

        return dir;
    }

    /// 프롬프트·정답·이미지를 모두 출력 폴더 복사본이 아니라 소스 폴더에서 읽는다.
    private static string ProjectDir([CallerFilePath] string path = "") => Path.GetDirectoryName(path)!;
}
