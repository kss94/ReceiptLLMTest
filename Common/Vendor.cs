namespace Common;

/// <summary>
/// 한 벤더의 프롬프트·정답·영수증이 모여 있는 폴더.
/// <code>
/// LLMTests/&lt;Name&gt;/
///   SystemPrompt.txt
///   UserPrompt.txt
///   Answers/&lt;id&gt;.json
///   Receipts/&lt;id&gt;/*.jpg
/// </code>
/// 벤더를 추가할 때 코드를 고치지 않도록 케이스 목록은 폴더에서 읽는다.
/// </summary>
public sealed class Vendor
{
    public Vendor(string dir)
    {
        Dir = Path.GetFullPath(dir);
        Name = Path.GetFileName(Dir);
    }

    public string Name { get; }
    public string Dir { get; }

    public string SystemPromptPath => Path.Combine(Dir, "SystemPrompt.txt");
    public string UserPromptPath => Path.Combine(Dir, "UserPrompt.txt");

    public string ReceiptDir(string id) => Path.Combine(Dir, "Receipts", id);
    public string AnswerPath(string id) => Path.Combine(Dir, "Answers", $"{id}.json");

    /// 정답이 있고 영수증 폴더도 있는 것만 채점 대상이다.
    /// 정답 파일을 하나 추가하면 케이스가 저절로 하나 늘어난다.
    public IReadOnlyList<string> Cases()
    {
        string answers = Path.Combine(Dir, "Answers");
        if (!Directory.Exists(answers)) return [];

        return Directory.EnumerateFiles(answers, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .OfType<string>()
            .Where(id => Directory.Exists(ReceiptDir(id)))
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
    }
}
