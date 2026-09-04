using Common;

namespace LLMTests;

/// 절 갈아끼우기를 고정한다. 여기가 틀리면 옵티마이저가 프롬프트를 조용히 망가뜨린다.
public class PromptDocumentTests
{
    const string Doc = """
        머리말입니다.

        ■ 0. 공통 원칙

        - 첫째
        - 둘째

        ■ 7. 쿠폰 처리

        - 쿠폰 규칙

        ■ 11. review

        - 마지막
        """;

    static PromptDocument Parse() => new(Doc);

    [Fact]
    public void 절_제목을_모두_찾는다()
    {
        Assert.Equal(
            ["■ 0. 공통 원칙", "■ 7. 쿠폰 처리", "■ 11. review"],
            Parse().Headers);
    }

    [Fact]
    public void 가운데_절만_갈아끼운다()
    {
        bool ok = Parse().TryReplace("■ 7. 쿠폰 처리", "■ 7. 쿠폰 처리\n\n- 새 규칙", out var result);

        Assert.True(ok);
        Assert.Contains("- 새 규칙", result.Text);
        Assert.DoesNotContain("- 쿠폰 규칙", result.Text);

        // 나머지 절은 손대지 않는다.
        Assert.Contains("머리말입니다.", result.Text);
        Assert.Contains("- 첫째", result.Text);
        Assert.Contains("- 마지막", result.Text);
        Assert.Equal(3, result.Headers.Count);
    }

    [Fact]
    public void 마지막_절도_갈아끼운다()
    {
        bool ok = Parse().TryReplace("■ 11. review", "■ 11. review\n\n- 바뀐 마지막", out var result);

        Assert.True(ok);
        Assert.Contains("- 바뀐 마지막", result.Text);
        Assert.DoesNotContain("- 마지막", result.Text);
        Assert.Contains("- 쿠폰 규칙", result.Text);
    }

    [Fact]
    public void 첫_절을_갈아끼워도_머리말은_남는다()
    {
        bool ok = Parse().TryReplace("■ 0. 공통 원칙", "■ 0. 공통 원칙\n\n- 바뀐 첫째", out var result);

        Assert.True(ok);
        Assert.StartsWith("머리말입니다.", result.Text);
        Assert.DoesNotContain("- 첫째", result.Text);
    }

    [Fact]
    public void 없는_절이면_문서를_건드리지_않는다()
    {
        var doc = Parse();
        bool ok = doc.TryReplace("■ 99. 없는 절", "■ 99. 없는 절\n\n- 무시", out var result);

        Assert.False(ok);
        Assert.Equal(doc.Text, result.Text);
    }

    [Fact]
    public void 절_본문을_제목까지_함께_꺼낸다()
    {
        string? section = Parse().Section("■ 7. 쿠폰 처리");

        Assert.NotNull(section);
        Assert.StartsWith("■ 7. 쿠폰 처리", section);
        Assert.Contains("- 쿠폰 규칙", section);
        Assert.DoesNotContain("- 마지막", section);
        Assert.DoesNotContain("- 첫째", section);
    }

    [Fact]
    public void 갈아끼운_결과를_다시_읽어도_구조가_같다()
    {
        Parse().TryReplace("■ 7. 쿠폰 처리", "■ 7. 쿠폰 처리\n\n- 새 규칙", out var once);
        bool ok = once.TryReplace("■ 7. 쿠폰 처리", "■ 7. 쿠폰 처리\n\n- 더 새 규칙", out var twice);

        Assert.True(ok);
        Assert.Contains("- 더 새 규칙", twice.Text);
        Assert.DoesNotContain("- 새 규칙", twice.Text);
        Assert.Equal(3, twice.Headers.Count);
    }

    [Fact]
    public void 실제_프롬프트의_절을_전부_찾는다()
    {
        var doc = new PromptDocument(File.ReadAllText(TestBase.Of("Starbucks").SystemPromptPath));

        // 절이 사라지면 옵티마이저가 고칠 자리를 잃는다.
        Assert.Equal(14, doc.Headers.Count);

        // 들여쓴 '■'는 본문이지 제목이 아니다. (419번째 줄)
        Assert.DoesNotContain(doc.Headers, h => h.StartsWith('('));
        Assert.Contains("■ 7. 쿠폰 처리 ★ 가장 중요", doc.Headers);

        // 모든 절을 자기 자신으로 갈아끼우면 원문 그대로여야 한다.
        var rebuilt = doc;
        foreach (string header in doc.Headers)
        {
            Assert.True(rebuilt.TryReplace(header, doc.Section(header)!, out rebuilt));
        }
        Assert.Equal(doc.Text, rebuilt.Text);
    }

    // ── 모델이 흔히 저지르는 실수 ────────────────────────────────────────
    // 실측: 5회 시도 중 3회가 제목의 '■'를 빠뜨려 통째로 버려졌다.

    [Fact]
    public void 제목의_기호가_빠져도_절을_찾는다()
    {
        bool ok = Parse().TryReplace("7. 쿠폰 처리", "■ 7. 쿠폰 처리\n\n- 새 규칙", out var result);

        Assert.True(ok);
        Assert.Contains("- 새 규칙", result.Text);
        Assert.Equal(3, result.Headers.Count);
    }

    [Fact]
    public void 본문에_제목_줄이_없으면_원래_제목을_얹는다()
    {
        bool ok = Parse().TryReplace("■ 7. 쿠폰 처리", "- 제목 없는 본문", out var result);

        Assert.True(ok);
        Assert.Contains("- 제목 없는 본문", result.Text);
        Assert.DoesNotContain("- 쿠폰 규칙", result.Text);

        // 제목이 사라지면 절이 앞 절에 흡수된다. 그러면 안 된다.
        Assert.Equal(3, result.Headers.Count);
        Assert.Contains("■ 7. 쿠폰 처리", result.Headers);
    }

    [Fact]
    public void 기호도_제목_줄도_없는_최악의_경우를_모두_받아낸다()
    {
        bool ok = Parse().TryReplace("  7. 쿠폰 처리  ", "- 본문만", out var result);

        Assert.True(ok);
        Assert.Equal(3, result.Headers.Count);
        Assert.Contains("- 본문만", result.Text);
        Assert.Contains("- 첫째", result.Text);
        Assert.Contains("- 마지막", result.Text);
    }

    [Fact]
    public void 빈_제목은_아무_절에도_걸리지_않는다()
    {
        Assert.False(Parse().TryReplace("", "- 무시", out _));
        Assert.False(Parse().TryReplace("  ■  ", "- 무시", out _));
        Assert.Null(Parse().Section(""));
    }
}
