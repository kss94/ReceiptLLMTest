using System.Diagnostics;
using Common;

namespace Runner;

/// <summary>
/// 프롬프트 한 벌을 벤더의 케이스 전체에 돌려 점수를 낸다.
/// run은 한 번, optimize는 후보마다 한 번씩 부른다.
/// </summary>
public sealed class Evaluator
{
    readonly ReceiptExtractor _extractor;
    readonly Common.Vendor _vendor;
    readonly string[] _cases;
    readonly int _parallel;
    readonly int _repeat;
    readonly Action<string> _log;

    public Evaluator(
        ReceiptExtractor extractor, Common.Vendor vendor, string[] cases,
        int parallel, int repeat, Action<string> log)
    {
        _extractor = extractor;
        _vendor = vendor;
        _cases = cases;
        _parallel = parallel;
        _repeat = repeat;
        _log = log;
    }

    public string[] Cases => _cases;

    public async Task<RunResult> RunAsync(
        string label, string systemPrompt, string userPrompt, string outDir, CancellationToken ct = default)
    {
        Directory.CreateDirectory(outDir);

        var stopwatch = Stopwatch.StartNew();
        var results = new CaseResult[_cases.Length];

        await Parallel.ForAsync(0, _cases.Length,
            new ParallelOptions { MaxDegreeOfParallelism = _parallel, CancellationToken = ct },
            async (i, token) =>
            {
                results[i] = await RunCase(_cases[i], systemPrompt, userPrompt, outDir, token);
                _log(results[i].Line());
            });

        stopwatch.Stop();
        return RunResult.From(_vendor.Name, label, _vendor.UserPromptPath, outDir, results, stopwatch.Elapsed);
    }

    /// 한 케이스를 Repeat번 호출한다. 온도가 고정이라 응답에 편차가 있어 평균을 쓴다.
    async Task<CaseResult> RunCase(
        string id, string systemPrompt, string userPrompt, string outDir, CancellationToken ct)
    {
        string expected = File.ReadAllText(_vendor.AnswerPath(id));

        // 응답과 그 채점 결과를 짝지어 들고 있는다. 따로 두면 저장할 때 다른 시도끼리 섞인다.
        var attempts = new List<(ReceiptDiff.Report Report, string Actual)>();
        string? error = null;

        for (int attempt = 0; attempt < _repeat; attempt++)
        {
            try
            {
                string actual = await _extractor.ExtractAsync(_vendor.ReceiptDir(id), systemPrompt, userPrompt, ct);
                attempts.Add((ReceiptDiff.Compare(expected, actual), actual));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                error = ex.Message;
            }
        }

        if (attempts.Count == 0) return CaseResult.Failed(id, error ?? "호출 실패");

        // 가장 나쁜 시도를 대표로 남긴다. 프롬프트를 고칠 때 봐야 할 건 잘 된 쪽이 아니라 깨진 쪽이다.
        var worst = attempts.MinBy(a => a.Report.Score);
        Save(outDir, id, worst.Actual, worst.Report);

        return new CaseResult
        {
            Id = id,
            Score = attempts.Average(a => a.Report.Score),
            Ok = attempts.All(a => a.Report.Ok),
            Matched = worst.Report.Matched,
            Total = worst.Report.Total,
            Attempts = attempts.Count,
            Spread = attempts.Count > 1
                ? attempts.Max(a => a.Report.Score) - attempts.Min(a => a.Report.Score)
                : 0,
            Diffs = [.. worst.Report.Diffs],
            Fuzzy = [.. worst.Report.Fuzzy],
            Error = error,
        };
    }

    static void Save(string dir, string id, string actual, ReceiptDiff.Report report)
    {
        File.WriteAllText(Path.Combine(dir, $"{id}.actual.json"), actual);

        string diffPath = Path.Combine(dir, $"{id}.diff.txt");
        string body = report.ToString();
        if (body.Length == 0) File.Delete(diffPath);
        else File.WriteAllText(diffPath, body);
    }
}
