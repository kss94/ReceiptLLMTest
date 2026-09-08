using System.Text.Json;
using System.Text.Json.Serialization;
using Common;

namespace Runner;

/// <summary>
/// run      — 프롬프트 하나를 벤더의 전체 케이스에 돌려 점수를 낸다.
/// optimize — 실패를 모델에 보여주고 절 하나씩 고쳐가며 점수를 올린다.
/// 표준출력은 결과 JSON, 표준오류는 사람이 읽는 진행 상황.
/// </summary>
public static class Program
{
    static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    static void Log(string line) => Console.Error.WriteLine(line);

    public static async Task<int> Main(string[] args)
    {
        // 콘솔 기본 코드페이지로는 한글 진단이 깨진다.
        try { Console.OutputEncoding = System.Text.Encoding.UTF8; } catch (IOException) { }

        Options options;
        // 키는 Common.ApiKey에 박혀 있다. 테스트와 같은 키를 쓴다.
        string apiKey = ApiKey.Gemini;
        try
        {
            options = Options.Parse(args);
        }
        catch (ArgumentException ex)
        {
            Log($"{ex.Message}{Environment.NewLine}{Environment.NewLine}{Options.Usage}");
            return 2;
        }

        var vendor = new Common.Vendor(options.Vendor);
        string[] cases = (options.Cases ?? vendor.Cases()).ToArray();
        if (cases.Length == 0)
        {
            Log($"{vendor.Name}: 채점할 케이스가 없습니다. (<id>/answer.json 확인)");
            return 2;
        }

        var evaluator = new Evaluator(new ReceiptExtractor(apiKey), vendor, cases, options.Parallel, options.Repeat, Log);

        Log($"{vendor.Name}: {cases.Length}개 케이스 × {options.Repeat}회, 동시 {options.Parallel}");
        Log($"  시스템 프롬프트: {options.SystemPrompt}");
        Log($"  출력: {options.OutDir}");
        Log("");

        return options.Command switch
        {
            Command.Optimize => await Optimize(options, evaluator, apiKey),
            _ => await Run(options, evaluator),
        };
    }

    static async Task<int> Run(Options options, Evaluator evaluator)
    {
        var run = await evaluator.RunAsync(
            options.SystemPrompt,
            File.ReadAllText(options.SystemPrompt),
            File.ReadAllText(options.UserPrompt),
            options.OutDir);

        File.WriteAllText(Path.Combine(options.OutDir, "result.json"), JsonSerializer.Serialize(run, Json));

        Log("");
        Log($"점수 {run.Score:F4}  통과 {run.Passed}/{run.Cases}  " +
            $"필드 {run.Matched}/{run.Total}  {run.Seconds:F1}s");
        if (run.MaxSpread > 0)
            Log($"편차 최대 {run.MaxSpread:F4} — 이보다 작은 점수 차이는 노이즈다.");

        Console.Out.WriteLine(JsonSerializer.Serialize(run, Json));
        return 0;
    }

    static async Task<int> Optimize(Options options, Evaluator evaluator, string apiKey)
    {
        if (options.Repeat == 1)
            Log("주의: --repeat 1이면 편차를 재지 못해 노이즈를 개선으로 착각할 수 있다. 2 이상을 권한다.");

        Log($"제안 모델: {options.ProposerModel}, 시도 {options.Iterations}회, 채택 기준 +{options.Threshold:F4}");
        Log("");

        var optimizer = new Optimizer(evaluator, new Proposer(apiKey, options.ProposerModel), Log);

        var (best, history) = await optimizer.RunAsync(
            options.SystemPrompt, options.UserPrompt, options.OutDir,
            options.Iterations, options.Threshold, options.ShowCases);

        int accepted = history.Count(h => h.Accepted);
        string outPrompt = Path.Combine(options.OutDir, "SystemPrompt.txt");

        Log("");
        Log($"시도 {history.Length}회 중 {accepted}회 채택.  최종 {best.Score:F4}  통과 {best.Passed}/{best.Cases}");
        Log($"고쳐진 프롬프트: {outPrompt}");
        Log($"원본은 건드리지 않았다. 확인한 뒤 직접 복사해라:");
        Log($"  cp \"{outPrompt}\" \"{options.SystemPrompt}\"");

        Console.Out.WriteLine(JsonSerializer.Serialize(new { best, history }, Json));
        return 0;
    }
}
