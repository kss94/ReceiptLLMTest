using System.Text.Json;
using Common;

namespace Runner;

/// 한 번의 시도 기록. 무엇을 고쳤고 점수가 어떻게 됐는지.
public sealed record Attempt
{
    public required int Iteration { get; init; }
    public required bool Accepted { get; init; }
    public string? Section { get; init; }
    public string? Reason { get; init; }
    public double Score { get; init; }
    public double Delta { get; init; }
    public int Passed { get; init; }
    public string? Rejected { get; init; }
}

/// <summary>
/// 실패를 모델에 보여주고 절 하나를 고친 뒤, 전체 케이스 점수가 올랐을 때만 채택한다.
/// <para>
/// 손으로 고칠 때와 다른 점은 <b>채택 조건</b>뿐이다. 한 케이스를 고치려다
/// 다른 케이스를 깨뜨리면 전체 점수가 내려가므로 자동으로 버려진다.
/// </para>
/// <para>
/// 원본 프롬프트는 절대 덮어쓰지 않는다. 결과는 출력 폴더에만 쓴다.
/// </para>
/// </summary>
public sealed class Optimizer
{
    /// 제안된 절이 원래 절보다 이만큼도 안 되면 잘려서 온 것으로 보고 버린다.
    const double MinSectionRatio = 0.3;

    readonly Evaluator _evaluator;
    readonly Proposer _proposer;
    readonly Action<string> _log;

    public Optimizer(Evaluator evaluator, Proposer proposer, Action<string> log)
    {
        _evaluator = evaluator;
        _proposer = proposer;
        _log = log;
    }

    public async Task<(RunResult Best, Attempt[] History)> RunAsync(
        string systemPromptPath, string userPromptPath, string outDir,
        int iterations, double threshold, int showCases, CancellationToken ct = default)
    {
        string userPrompt = File.ReadAllText(userPromptPath);
        var document = new PromptDocument(File.ReadAllText(systemPromptPath));
        var history = new List<Attempt>();

        _log($"기준선 채점 중… ({_evaluator.Cases.Length}개 케이스)");
        string bestDir = Path.Combine(outDir, "iter-00");
        var best = await _evaluator.RunAsync("기준선", document.Text, userPrompt, bestDir, ct);
        _log($"기준선 {best.Score:F4}  통과 {best.Passed}/{best.Cases}");

        for (int i = 1; i <= iterations; i++)
        {
            ct.ThrowIfCancellationRequested();

            if (best.Passed == best.Cases)
            {
                _log("전부 통과했다. 고칠 것이 없어 멈춘다.");
                break;
            }

            _log("");
            _log($"── {i}/{iterations} ──");

            var attempt = await Step(i, document, best, bestDir, userPrompt, outDir, threshold, showCases, ct);
            history.Add(attempt.Record);

            if (attempt.Record.Accepted)
            {
                document = attempt.Document!;
                best = attempt.Run!;
                bestDir = attempt.Dir!;
                File.WriteAllText(Path.Combine(outDir, "SystemPrompt.txt"), document.Text);
            }
        }

        File.WriteAllText(Path.Combine(outDir, "SystemPrompt.txt"), document.Text);
        File.WriteAllText(
            Path.Combine(outDir, "history.json"),
            JsonSerializer.Serialize(history, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            }));

        return (best, [.. history]);
    }

    async Task<(Attempt Record, PromptDocument? Document, RunResult? Run, string? Dir)> Step(
        int iteration, PromptDocument document, RunResult best, string bestDir, string userPrompt,
        string outDir, double threshold, int showCases, CancellationToken ct)
    {
        Attempt Reject(string why, string? section = null, string? reason = null)
        {
            _log($"  버림: {why}");
            return new Attempt
            {
                Iteration = iteration,
                Accepted = false,
                Section = section,
                Reason = reason,
                Score = best.Score,
                Passed = best.Passed,
                Rejected = why,
            };
        }

        Proposal? proposal;
        try
        {
            proposal = await _proposer.ProposeAsync(document, best, bestDir, showCases, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return (Reject($"제안 호출 실패 - {ex.Message}"), null, null, null);
        }

        if (proposal is null || string.IsNullOrWhiteSpace(proposal.Section))
            return (Reject("제안을 읽지 못했다"), null, null, null);

        _log($"  절: {proposal.Section}");
        _log($"  이유: {proposal.Reason}");

        string? original = document.Section(proposal.Section);
        if (original is null)
            return (Reject($"그런 절이 없다: {proposal.Section}", proposal.Section, proposal.Reason), null, null, null);

        // 절이 통째로 잘려 오면 규칙이 사라진다. 길이로 거른다.
        if (proposal.Replacement.Length < original.Length * MinSectionRatio)
            return (Reject($"제안이 너무 짧다 ({proposal.Replacement.Length}자 < 원래 {original.Length}자)",
                proposal.Section, proposal.Reason), null, null, null);

        if (!document.TryReplace(proposal.Section, proposal.Replacement, out var candidate))
            return (Reject("절 갈아끼우기 실패", proposal.Section, proposal.Reason), null, null, null);

        if (candidate.Headers.Count != document.Headers.Count)
            return (Reject($"절 개수가 달라졌다 ({document.Headers.Count} → {candidate.Headers.Count})",
                proposal.Section, proposal.Reason), null, null, null);

        string dir = Path.Combine(outDir, $"iter-{iteration:D2}");
        var run = await _evaluator.RunAsync($"후보 {iteration}", candidate.Text, userPrompt, dir, ct);

        double delta = run.Score - best.Score;
        _log($"  {run.Score:F4} (기준 {best.Score:F4}, {delta:+0.0000;-0.0000;0}) 통과 {run.Passed}/{run.Cases}");

        if (delta <= threshold)
            return (Reject($"점수가 오르지 않았다 ({delta:+0.0000;-0.0000;0} ≤ {threshold:F4})",
                proposal.Section, proposal.Reason), null, null, null);

        _log("  채택");
        File.WriteAllText(Path.Combine(dir, "SystemPrompt.txt"), candidate.Text);

        return (new Attempt
        {
            Iteration = iteration,
            Accepted = true,
            Section = proposal.Section,
            Reason = proposal.Reason,
            Score = run.Score,
            Delta = delta,
            Passed = run.Passed,
        }, candidate, run, dir);
    }
}
