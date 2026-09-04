using Common;

namespace Runner;

/// 케이스 하나의 채점 결과. Diffs가 프롬프트를 고칠 근거다.
public sealed record CaseResult
{
    public required string Id { get; init; }

    /// Repeat회 평균 점수(0~1).
    public required double Score { get; init; }

    /// 모든 시도가 차이 없이 통과했는가.
    public required bool Ok { get; init; }

    public int Matched { get; init; }
    public int Total { get; init; }
    public int Attempts { get; init; }

    /// 시도들 사이의 점수 폭. 이 값보다 작은 점수 차이는 노이즈로 봐야 한다.
    public double Spread { get; init; }

    public string[] Diffs { get; init; } = [];
    public string[] Fuzzy { get; init; } = [];
    public string? Error { get; init; }

    public static CaseResult Failed(string id, string error) => new()
    {
        Id = id,
        Score = 0,
        Ok = false,
        Error = error,
    };

    public string Line()
    {
        string mark = Ok ? "OK  " : "FAIL";
        string detail = Error is not null ? $"  !! {Error}"
            : Diffs.Length > 0 ? $"  {Diffs.Length}건 불일치"
            : "";
        return $"  {mark} {Score:F3}  {Id}{detail}";
    }
}

/// 프롬프트 한 벌에 대한 전체 결과. 옵티마이저는 Score만 보고 후보를 고른다.
public sealed record RunResult
{
    public required string Vendor { get; init; }
    public required string Model { get; init; }
    public required string SystemPrompt { get; init; }
    public required string UserPrompt { get; init; }
    public required string OutDir { get; init; }

    /// 케이스별 점수의 평균. 영수증 하나를 한 문제로 본다. 후보 비교의 기준값.
    public required double Score { get; init; }

    /// 필드 수로 가중한 평균. 필드가 많은 영수증이 더 큰 비중을 갖는다.
    public required double FieldScore { get; init; }

    public required int Passed { get; init; }
    public required int Cases { get; init; }
    public required int Matched { get; init; }
    public required int Total { get; init; }

    /// 케이스별 편차의 최댓값. Score 차이가 이보다 작으면 개선인지 노이즈인지 알 수 없다.
    public required double MaxSpread { get; init; }

    public required double Seconds { get; init; }
    public required CaseResult[] Results { get; init; }

    public static RunResult From(
        string vendorName, string systemPrompt, string userPrompt, string outDir,
        CaseResult[] results, TimeSpan elapsed)
    {
        int matched = results.Sum(r => r.Matched);
        int total = results.Sum(r => r.Total);

        return new RunResult
        {
            Vendor = vendorName,
            Model = ReceiptExtractor.Model,
            SystemPrompt = systemPrompt,
            UserPrompt = userPrompt,
            OutDir = outDir,
            Score = results.Length == 0 ? 0 : results.Average(r => r.Score),
            FieldScore = total == 0 ? 0 : (double)matched / total,
            Passed = results.Count(r => r.Ok),
            Cases = results.Length,
            Matched = matched,
            Total = total,
            MaxSpread = results.Length == 0 ? 0 : results.Max(r => r.Spread),
            Seconds = elapsed.TotalSeconds,
            Results = [.. results.OrderBy(r => r.Score)],   // 나쁜 것부터. 고칠 것이 위로 온다.
        };
    }
}
