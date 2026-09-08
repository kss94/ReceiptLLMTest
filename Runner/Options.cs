using System.Runtime.CompilerServices;

namespace Runner;

public enum Command { Run, Optimize }

/// 명령줄 인자. 값이 없는 것은 벤더 폴더의 기본 파일을 쓴다.
public sealed record Options
{
    public required Command Command { get; init; }
    public required string Vendor { get; init; }
    public required string SystemPrompt { get; init; }
    public required string UserPrompt { get; init; }
    public required string OutDir { get; init; }
    public string[]? Cases { get; init; }
    public int Parallel { get; init; } = 4;
    public int Repeat { get; init; } = 1;

    // optimize 전용
    public int Iterations { get; init; } = 5;
    public double Threshold { get; init; } = 0.002;
    public int ShowCases { get; init; } = 3;
    public string ProposerModel { get; init; } = Common.ReceiptExtractor.Model;

    /// 벤더 폴더들이 있는 곳. 러너를 어디서 실행하든 소스 폴더를 가리킨다.
    public static string DefaultRoot => Path.Combine(RepoDir(), "LLMTests");

    public const string Usage = """
        사용법:
          runner run      --vendor <이름> [옵션]     프롬프트 하나를 채점한다
          runner optimize --vendor <이름> [옵션]     실패를 보고 프롬프트를 고쳐가며 점수를 올린다

        공통
          --vendor <이름>     벤더 폴더 이름 (예: Starbucks). 필수
          --system <경로>     시스템 프롬프트. 기본값 <벤더>/SystemPrompt.txt
          --user <경로>       사용자 프롬프트. 기본값 <벤더>/UserPrompt.txt
          --cases a,b,c       채점할 영수증 id. 기본값은 정답이 있는 전체
          --out <폴더>        결과 저장 위치. 기본값 Runs/<벤더>/<시각>
          --parallel <N>      동시 호출 수. 기본값 4
          --repeat <N>        같은 케이스를 N번 호출해 평균. 기본값 1
                              온도가 고정이 아니라 응답에 편차가 있다. optimize에는 2 이상을 권한다.
          --root <폴더>       벤더 폴더들의 상위. 기본값은 소스의 LLMTests/

        optimize 전용
          --iterations <N>    시도 횟수. 기본값 5
          --threshold <x>     이만큼 올라야 채택한다. 기본값 0.002
          --show <N>          모델에게 보여줄 실패 케이스 수. 기본값 3
          --proposer <모델>   수정안을 내는 모델. 기본값은 뽑는 모델과 같다.
                              뽑는 모델보다 좋은 것을 쓰는 편이 낫다.

        run은 표준출력으로 결과 JSON을 낸다.  runner run --vendor Starbucks > result.json
        optimize는 원본 프롬프트를 덮어쓰지 않는다. 결과는 <out>/SystemPrompt.txt에 쓴다.
        """;

    public static Options Parse(string[] args)
    {
        var command = Command.Run;
        int start = 0;
        if (args.Length > 0 && !args[0].StartsWith("--"))
        {
            command = args[0].ToLowerInvariant() switch
            {
                "run" => Command.Run,
                "optimize" => Command.Optimize,
                _ => throw new ArgumentException($"알 수 없는 명령: {args[0]}"),
            };
            start = 1;
        }

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = start; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--")) throw new ArgumentException($"알 수 없는 인자: {args[i]}");
            if (i + 1 >= args.Length) throw new ArgumentException($"{args[i]}에 값이 없습니다.");
            map[args[i][2..]] = args[++i];
        }

        if (!map.TryGetValue("vendor", out string? vendor))
            throw new ArgumentException("--vendor가 필요합니다.");

        string root = map.GetValueOrDefault("root") ?? DefaultRoot;
        string dir = Path.Combine(root, vendor);
        if (!Directory.Exists(dir)) throw new ArgumentException($"벤더 폴더가 없습니다: {dir}");

        var v = new Common.Vendor(dir);

        return new Options
        {
            Command = command,
            Vendor = dir,
            SystemPrompt = map.GetValueOrDefault("system") ?? v.SystemPromptPath,
            UserPrompt = map.GetValueOrDefault("user") ?? v.UserPromptPath,
            Cases = map.TryGetValue("cases", out string? c)
                ? c.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                : null,
            OutDir = map.GetValueOrDefault("out")
                ?? Path.Combine(RepoDir(), "Runs", v.Name, DateTime.Now.ToString("yyyyMMdd-HHmmss")),
            Parallel = Int(map, "parallel", 4),
            Repeat = Int(map, "repeat", 1),
            Iterations = Int(map, "iterations", 5),
            Threshold = double.TryParse(map.GetValueOrDefault("threshold"), out double t) ? t : 0.002,
            ShowCases = Int(map, "show", 3),
            ProposerModel = map.GetValueOrDefault("proposer") ?? Common.ReceiptExtractor.Model,
        };
    }

    static int Int(Dictionary<string, string> map, string key, int fallback) =>
        int.TryParse(map.GetValueOrDefault(key), out int n) ? Math.Max(1, n) : fallback;

    /// 빌드 출력이 아니라 소스 위치에서 저장소 루트를 잡는다. (Runner/Options.cs -> Runner/ -> 루트)
    static string RepoDir([CallerFilePath] string path = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, ".."));
}
