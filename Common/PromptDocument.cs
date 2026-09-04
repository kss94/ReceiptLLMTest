namespace Common;

/// <summary>
/// '■'로 시작하는 줄을 절 제목으로 보는 프롬프트 문서.
/// <para>
/// 옵티마이저가 32KB 문서를 통째로 다시 쓰게 하면 고치라고 하지 않은 450줄까지 흔들린다.
/// 그래서 모델에게는 <b>절 하나</b>만 돌려받고, 갈아끼우는 일은 이 클래스가 한다.
/// </para>
/// </summary>
public sealed class PromptDocument
{
    readonly string[] _lines;

    public PromptDocument(string text) => _lines = Lines(text);

    public string Text => string.Join(System.Environment.NewLine, _lines);

    /// 절 제목들. 옵티마이저가 이 중 하나를 골라 고친다.
    public IReadOnlyList<string> Headers => [.. _lines.Where(IsHeader).Select(l => l.Trim())];

    /// header로 시작하는 절 전체(제목 줄 포함)를 body로 갈아끼운다.
    /// 없는 절이면 false를 주고 문서는 그대로 둔다.
    /// <para>
    /// 모델은 제목의 '■'를 자주 빠뜨린다. 제목을 찾을 때도, body 첫 줄을 볼 때도
    /// '■'는 없는 셈 치고 맞춘다. 그것 때문에 멀쩡한 수정안을 버리면 시도 한 번이 날아간다.
    /// </para>
    public bool TryReplace(string header, string body, out PromptDocument result)
    {
        result = this;

        int start = Find(header);
        if (start < 0) return false;

        int end = Array.FindIndex(_lines, start + 1, IsHeader);
        if (end < 0) end = _lines.Length;

        // body를 다듬지 않는다. 꺼낸 절을 그대로 다시 넣으면 원문과 한 글자도 달라지지 않아야 한다.
        string[] lines = Lines(body);

        // 제목 줄이 빠졌으면 원래 제목을 얹는다. 그대로 넣으면 절이 하나 사라진다.
        int first = Array.FindIndex(lines, l => l.Trim().Length > 0);
        if (first < 0 || !IsHeader(lines[first])) lines = [_lines[start], .. lines];

        string[] rebuilt =
        [
            .. _lines[..start],
            .. lines,
            .. _lines[end..],
        ];

        result = new PromptDocument(string.Join(System.Environment.NewLine, rebuilt));
        return true;
    }

    /// header 절의 본문. 모델에게 "이 절을 고쳐라"라고 보여줄 때 쓴다.
    public string? Section(string header)
    {
        int start = Find(header);
        if (start < 0) return null;

        int end = Array.FindIndex(_lines, start + 1, IsHeader);
        if (end < 0) end = _lines.Length;

        return string.Join(System.Environment.NewLine, _lines[start..end]);
    }

    /// 제목 줄의 위치. 없으면 -1.
    int Find(string header)
    {
        string target = Key(header);
        return target.Length == 0 ? -1 : Array.FindIndex(_lines, l => IsHeader(l) && Key(l) == target);
    }

    static bool IsHeader(string line) => line.StartsWith('■');

    /// 제목을 맞출 때 쓰는 키. '■'와 앞뒤 공백은 무시한다.
    static string Key(string line) => line.Trim().TrimStart('■').Trim();

    static string[] Lines(string text) => text.ReplaceLineEndings("\n").Split('\n');
}
