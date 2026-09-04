using System.Text;
using System.Text.Json;
using Common;
using Google.GenAI;
using Google.GenAI.Types;

using File = System.IO.File;

namespace Runner;

/// 모델이 내놓은 수정안. 절 하나를 통째로 갈아끼운다.
public sealed record Proposal
{
    public string Section { get; init; } = "";
    public string Replacement { get; init; } = "";
    public string Reason { get; init; } = "";
}

/// <summary>
/// 실패한 케이스를 보여주고 프롬프트의 절 하나를 고쳐 달라고 한다.
/// <para>
/// 추출 호출과 달리 이쪽은 설정이 고정이 아니다. thinking을 막지 않고,
/// 모델도 <c>--proposer</c>로 바꿀 수 있다. 뽑는 모델보다 좋은 걸 쓰는 편이 낫다.
/// </para>
/// </summary>
public sealed class Proposer
{
    /// 케이스 하나에서 보여줄 JSON 최대 길이. 넘으면 자른다.
    const int MaxJsonChars = 4000;

    const string Instruction = """
        당신은 영수증 이미지를 JSON으로 뽑는 프롬프트를 고치는 사람입니다.

        아래 [현재 프롬프트]로 뽑았더니 [실패한 케이스]처럼 틀렸습니다.
        프롬프트의 '절' 하나만 골라 다시 써서 이 실패를 고치세요.

        ■ 지켜야 할 것
        1. 절은 하나만 고칩니다. section에는 [고칠 수 있는 절] 목록의 제목을 한 글자도 바꾸지 말고
           그대로 옮겨 적습니다.
        2. replacement에는 그 절 전체를 제목 줄부터 끝까지 씁니다. 다른 절은 쓰지 않습니다.
        3. ★ 지금 맞고 있는 케이스를 깨뜨리지 마세요. 규칙을 지우기보다 적용 조건을 좁히는 쪽을
           택합니다. 이미 있는 규칙과 충돌하는 문장을 새로 넣지 마세요.
        4. ★ 특정 영수증의 값(금액·상품명·날짜·주문번호)을 프롬프트에 적지 마세요.
           그 영수증 하나만 맞고 나머지는 그대로입니다. 왜 틀렸는지를 일반 규칙으로 적으세요.
        5. 한국어로 쓰고, 기존 절의 문체·들여쓰기·번호 체계를 그대로 따릅니다.

        ■ 실패를 읽는 법
        - expected가 정답, actual이 모델이 뽑은 값입니다.
        - '개수 다름'은 행을 잘못 나눴거나 합쳤다는 뜻입니다. 대개 상품 행 판별(3번)이나
          옵션 판별(5번)의 문제입니다.
        - '~'로 시작하는 줄은 통과시킨 것이니 고칠 대상이 아닙니다.

        ■ 응답 형식 (JSON)
        {
          "section": "고칠 절의 제목",
          "reason": "무엇을 왜 고쳤는지 한두 문장",
          "replacement": "■ 로 시작하는 절 전체"
        }
        """;

    readonly Client _client;
    readonly string _model;

    public Proposer(string apiKey, string model)
    {
        _client = new Client(apiKey: apiKey, httpOptions: new HttpOptions { RetryOptions = new HttpRetryOptions() });
        _model = model;
    }

    public async Task<Proposal?> ProposeAsync(
        PromptDocument document, RunResult run, string outDir, int showCases, CancellationToken ct = default)
    {
        string request = BuildRequest(document, run, outDir, showCases);

        var config = new GenerateContentConfig
        {
            SystemInstruction = new Content { Parts = [new Part { Text = Instruction }] },
            ResponseMimeType = "application/json",
        };

        var response = await _client.Models.GenerateContentAsync(
            _model, new Content { Parts = [Part.FromText(request)] }, config, ct);

        if (response.Text is null) return null;

        try
        {
            return JsonSerializer.Deserialize<Proposal>(response.Text, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });
        }
        catch (JsonException)
        {
            return null;
        }
    }

    static string BuildRequest(PromptDocument document, RunResult run, string outDir, int showCases)
    {
        var sb = new StringBuilder();

        sb.AppendLine("[고칠 수 있는 절]");
        foreach (string header in document.Headers) sb.AppendLine($"  {header}");
        sb.AppendLine();

        var failed = run.Results.Where(r => !r.Ok).Take(showCases).ToArray();
        int passing = run.Passed;

        sb.AppendLine($"[현재 점수] {run.Score:F4}  (통과 {passing}/{run.Cases})");
        sb.AppendLine($"통과한 {passing}개는 지금 규칙으로 맞고 있습니다. 이걸 깨뜨리면 점수가 내려갑니다.");
        sb.AppendLine();

        sb.AppendLine("[실패한 케이스]");
        foreach (var c in failed)
        {
            sb.AppendLine($"--- {c.Id} (점수 {c.Score:F3}) ---");
            sb.AppendLine("차이:");
            foreach (string d in c.Diffs) sb.AppendLine($"  {d}");

            string? actual = ReadIfExists(Path.Combine(outDir, $"{c.Id}.actual.json"));
            if (actual is not null)
            {
                sb.AppendLine("모델이 뽑은 JSON:");
                sb.AppendLine(Clip(actual));
            }
            sb.AppendLine();
        }

        sb.AppendLine("[현재 프롬프트]");
        sb.AppendLine(document.Text);

        return sb.ToString();
    }

    static string? ReadIfExists(string path) => File.Exists(path) ? File.ReadAllText(path) : null;

    static string Clip(string text) =>
        text.Length <= MaxJsonChars ? text : text[..MaxJsonChars] + "\n… (생략)";
}
