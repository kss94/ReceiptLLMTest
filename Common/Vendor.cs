namespace Common;

/// <summary>
/// 한 벤더의 프롬프트와 케이스가 모여 있는 폴더.
/// <code>
/// LLMTests/&lt;Name&gt;/
///   SystemPrompt.txt
///   UserPrompt.txt
///   &lt;id&gt;/answer.json   정답. 이 파일이 있는 폴더만 채점 대상이다
///   &lt;id&gt;/*.jpg         한 폴더의 이미지 전부가 한 요청으로 간다
/// </code>
/// 벤더를 추가할 때 코드를 고치지 않도록 케이스 목록은 폴더에서 읽는다.
/// </summary>
public sealed class Vendor
{
    public const string AnswerFileName = "answer.json";

    public Vendor(string dir)
    {
        Dir = Path.GetFullPath(dir);
        Name = Path.GetFileName(Dir);
    }

    public string Name { get; }
    public string Dir { get; }

    public string SystemPromptPath => Path.Combine(Dir, "SystemPrompt.txt");
    public string UserPromptPath => Path.Combine(Dir, "UserPrompt.txt");

    /// 정답과 영수증 이미지가 같이 있는 케이스 폴더.
    public string ReceiptDir(string id) => Path.Combine(Dir, id);
    public string AnswerPath(string id) => Path.Combine(ReceiptDir(id), AnswerFileName);

    /// answer.json이 있는 폴더만 채점 대상이다.
    /// 정답 파일을 하나 추가하면 케이스가 저절로 하나 늘어난다.
    public IReadOnlyList<string> Cases()
    {
        if (!Directory.Exists(Dir)) return [];

        return Directory.EnumerateDirectories(Dir)
            .Select(Path.GetFileName)
            .OfType<string>()
            .Where(id => File.Exists(AnswerPath(id)))
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
    }
}
