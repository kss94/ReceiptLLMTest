using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Common;

/// <summary>
/// 영수증 JSON 비교기. 키 순서와 공백은 무시하고 차이를 필드 단위로 모아서 보고한다.
/// 금액·수량은 값이 정확히 같아야 하고, 이름 계열 필드는 OCR 오독을 통과시킨다.
/// (예: '카페 라떼' / '카페 리떼', '햄루꼴라SW' / '햄루폴라SW')
/// 값이 null인 필드는 응답에서 키가 빠져도 같은 뜻으로 본다(그 반대도).
/// </summary>
public static class ReceiptDiff
{
    /// 오독을 허용하는 필드. 나머지 문자열(receiptDate, receiptTime, review 등)은 정확히 일치해야 한다.
    static readonly HashSet<string> FuzzyFields =
    [
        "storeSubName", "productName", "optionName", "discountName",
        "productDiscountName", "paymentMethod", "paymentName",
    ];

    /// 정답에 없어도 실패로 보지 않는 최상위 필드. 모델이 계산 과정을 적는 자리이므로 채점하지 않는다.
    /// 값은 {name}.actual.json에 그대로 남으니 실패를 들여다볼 때 거기서 확인한다.
    static readonly HashSet<string> IgnoredRootFields = ["couponCalc"];

    /// 길이별 허용 편집거리. 2자 이하('쿠폰', '옵션', '할인' 등 규칙이 만들어내는 이름)는 정확히 일치해야 한다.
    static int Allowed(int length) => length <= 2 ? 0 : Math.Max(1, length / 6);

    public sealed class Report
    {
        /// 실패 사유.
        public List<string> Diffs { get; } = [];

        /// 오독으로 보고 통과시킨 이름. 실패는 아니지만 눈으로 확인할 값.
        public List<string> Fuzzy { get; } = [];

        public bool Ok => Diffs.Count == 0;

        public override string ToString() => string.Join(Environment.NewLine, Diffs.Concat(Fuzzy));
    }

    public static Report Compare(string expectedJson, string actualJson)
    {
        var report = new Report();
        JsonNode? expected, actual;

        try { expected = JsonNode.Parse(expectedJson); }
        catch (JsonException ex) { report.Diffs.Add($"(정답 파일): JSON 파싱 실패 - {ex.Message}"); return report; }

        try { actual = JsonNode.Parse(actualJson); }
        catch (JsonException ex) { report.Diffs.Add($"(응답): JSON 파싱 실패 - {ex.Message}"); return report; }

        Walk("", expected, actual, report);
        return report;
    }

    static void Walk(string path, JsonNode? expected, JsonNode? actual, Report report)
    {
        if (expected is JsonObject eo)
        {
            if (actual is not JsonObject ao)
            {
                report.Diffs.Add($"{Label(path)}: 객체가 아님 (actual={Text(actual)})");
                return;
            }

            foreach ((string key, JsonNode? value) in eo)
            {
                string p = Join(path, key);
                if (ao.ContainsKey(key)) Walk(p, value, ao[key], report);
                else if (value is not null) report.Diffs.Add($"{p}: 응답에 없음 (expected={Text(value)})");
            }

            foreach ((string key, JsonNode? value) in ao)
            {
                if (eo.ContainsKey(key)) continue;
                if (path.Length == 0 && IgnoredRootFields.Contains(key)) continue;
                if (value is null) continue;
                report.Diffs.Add($"{Join(path, key)}: 정답에 없는 필드 (actual={Text(value)})");
            }

            return;
        }

        if (expected is JsonArray ea)
        {
            if (actual is not JsonArray aa)
            {
                report.Diffs.Add($"{Label(path)}: 배열이 아님 (actual={Text(actual)})");
                return;
            }

            if (ea.Count != aa.Count)
            {
                report.Diffs.Add($"{Label(path)}: 개수 다름 (expected={ea.Count}, actual={aa.Count})");
                for (int i = 0; i < Math.Min(ea.Count, aa.Count); i++)
                    Walk($"{path}[{i}]", ea[i], aa[i], report);
                return;
            }

            var inOrder = new Report();
            for (int i = 0; i < ea.Count; i++)
                Walk($"{path}[{i}]", ea[i], aa[i], inOrder);

            // 항목이 같고 순서만 다르면 금액이 달라지지 않으므로 실패로 보지 않는다.
            if (!inOrder.Ok && TryMatchAnyOrder(path, ea, aa, out Report reordered))
            {
                report.Fuzzy.Add($"~ {Label(path)}: 순서만 다름 " +
                                 $"(expected: {Names(ea)} / actual: {Names(aa)})");
                report.Fuzzy.AddRange(reordered.Fuzzy);
                return;
            }

            report.Diffs.AddRange(inOrder.Diffs);
            report.Fuzzy.AddRange(inOrder.Fuzzy);
            return;
        }

        if (expected is null || actual is null)
        {
            if (expected is not null || actual is not null)
                report.Diffs.Add($"{Label(path)}: expected={Text(expected)}, actual={Text(actual)}");
            return;
        }

        string ev = Text(expected), av = Text(actual);

        // 이름: 공백 차이는 무시하고, 남은 편집거리가 허용치 안이면 통과.
        if (FuzzyFields.Contains(Field(path)))
        {
            string e = Squeeze(ev), a = Squeeze(av);
            if (e == a) return;

            int distance = Levenshtein(e, a);
            string line = $"{Label(path)}: expected=\"{ev}\", actual=\"{av}\" (편집거리 {distance}, 허용 {Allowed(e.Length)})";

            if (distance <= Allowed(e.Length)) report.Fuzzy.Add($"~ {line}");
            else report.Diffs.Add(line);
            return;
        }

        // 금액·수량: 표기(쉼표, 문자열/숫자)는 무시하고 값만 본다.
        if (TryNumber(ev, out decimal en) && TryNumber(av, out decimal an))
        {
            if (en != an) report.Diffs.Add($"{Label(path)}: expected={ev}, actual={av}");
            return;
        }

        if (ev.Trim() != av.Trim())
            report.Diffs.Add($"{Label(path)}: expected=\"{ev}\", actual=\"{av}\"");
    }

    /// 기대 항목 하나하나를 아직 쓰지 않은 실제 항목과 짝지어 본다.
    /// 전부 짝이 맞으면 순서만 다른 것이다.
    static bool TryMatchAnyOrder(string path, JsonArray expected, JsonArray actual, out Report matched)
    {
        matched = new Report();
        bool[] used = new bool[actual.Count];

        for (int i = 0; i < expected.Count; i++)
        {
            int found = -1;
            for (int j = 0; j < actual.Count && found < 0; j++)
            {
                if (used[j]) continue;
                var trial = new Report();
                Walk($"{path}[{i}]", expected[i], actual[j], trial);
                if (trial.Ok) { found = j; matched.Fuzzy.AddRange(trial.Fuzzy); }
            }
            if (found < 0) return false;
            used[found] = true;
        }

        return true;
    }

    /// 순서 차이를 눈으로 확인할 수 있게 항목 이름을 뽑는다.
    static string Names(JsonArray array)
    {
        string[] keys = ["productName", "optionName", "discountName", "paymentMethod"];
        var names = array.Select((node, i) =>
        {
            if (node is JsonObject o)
                foreach (string key in keys)
                    if (o.TryGetPropertyValue(key, out JsonNode? v) && v is not null) return Text(v);
            return $"[{i}]";
        });
        return string.Join(", ", names);
    }

    static bool TryNumber(string s, out decimal value)
    {
        string t = Regex.Replace(s, @"[,\s원₩]", "");
        return decimal.TryParse(t, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    static int Levenshtein(string a, string b)
    {
        if (a.Length == 0) return b.Length;
        if (b.Length == 0) return a.Length;

        int[] prev = new int[b.Length + 1];
        int[] cur = new int[b.Length + 1];
        for (int j = 0; j <= b.Length; j++) prev[j] = j;

        for (int i = 1; i <= a.Length; i++)
        {
            cur[0] = i;
            for (int j = 1; j <= b.Length; j++)
            {
                int sub = prev[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1);
                cur[j] = Math.Min(Math.Min(prev[j] + 1, cur[j - 1] + 1), sub);
            }
            (prev, cur) = (cur, prev);
        }

        return prev[b.Length];
    }

    static string Squeeze(string s) => Regex.Replace(s, @"\s+", "");

    static string Join(string path, string key) => path.Length == 0 ? key : $"{path}.{key}";

    static string Label(string path) => path.Length == 0 ? "(root)" : path;

    /// "productItems[0].productOptionItems[1].optionName" -> "optionName"
    static string Field(string path)
    {
        int dot = path.LastIndexOf('.');
        string last = dot < 0 ? path : path[(dot + 1)..];
        int bracket = last.IndexOf('[');
        return bracket < 0 ? last : last[..bracket];
    }

    static string Text(JsonNode? node) => node switch
    {
        null => "null",
        JsonValue value => value.ToString(),
        _ => node.ToJsonString(),
    };
}
