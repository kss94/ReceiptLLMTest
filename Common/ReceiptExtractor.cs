using Google.GenAI;
using Google.GenAI.Types;

// Google.GenAI.Types에도 File·Environment가 있어 이름이 겹친다.
using File = System.IO.File;

namespace Common;

/// <summary>
/// 영수증 이미지 + 프롬프트 → 모델 응답(JSON 문자열).
/// 테스트와 러너가 <b>같은</b> 호출 설정을 쓰도록 둘 다 이 클래스만 거친다.
/// 모델·ThinkingLevel·ResponseMimeType은 고정이다. 정확도는 프롬프트로만 조정한다.
/// </summary>
public sealed class ReceiptExtractor
{
    public const string Model = "gemini-3.5-flash-lite";

    static readonly string[] Extensions = [".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp"];

    readonly Client _client;

    /// 키는 호출자가 넘긴다. 이 파일에 키를 두지 않는다.
    public ReceiptExtractor(string apiKey)
    {
        _client = new Client(
            apiKey: apiKey,
            httpOptions: new HttpOptions { RetryOptions = new HttpRetryOptions() });
    }

    /// receiptDir 안의 이미지를 전부 한 요청에 붙여 한 번 호출한다.
    public async Task<string> ExtractAsync(
        string receiptDir, string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        var response = await _client.Models.GenerateContentAsync(
            Model, CreateContent(receiptDir, userPrompt), CreateConfig(systemPrompt), ct);

        return response.Text
            ?? throw new InvalidOperationException($"{Path.GetFileName(receiptDir)}: 응답이 비어 있습니다.");
    }

    public static Content CreateContent(string receiptDir, string userPrompt)
    {
        // 이미지 순서를 이름순으로 고정한다. 점수를 비교하려면 같은 입력이 같은 순서로 가야 한다.
        var images = Directory.EnumerateFiles(receiptDir)
            .Where(f => Extensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .OrderBy(f => f, StringComparer.Ordinal);

        List<Part> parts = [Part.FromText(userPrompt)];
        foreach (string path in images)
        {
            byte[] bytes = File.ReadAllBytes(path);
            string mimeType = MimeTypes.GetMimeType(Path.GetFileName(path));
            parts.Add(Part.FromBytes(bytes, mimeType));
        }
        return new Content { Parts = parts };
    }

    public static GenerateContentConfig CreateConfig(string systemPrompt)
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
}
