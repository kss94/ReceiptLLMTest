using Common;

namespace LLMTests;

/// ReceiptDiff의 판정 기준을 고정한다. API를 호출하지 않으므로 오프라인에서 돌아간다.
public class ReceiptDiffTests
{
    const string Expected = """
        {
          "totalOrderPrice": 6600,
          "productItems": [
            {
              "productName": "I-G)카페 라떼",
              "productPrice": 6600,
              "productUnit": 1,
              "productOptionItems": [ { "optionName": "쿠폰", "optionPrice": -2540 } ]
            }
          ],
          "address": null,
          "review": "NONEED"
        }
        """;

    static ReceiptDiff.Report Compare(string actual) => ReceiptDiff.Compare(Expected, actual);

    [Fact]
    public void 이름_오독은_통과시킨다()
    {
        var report = Compare(Expected.Replace("카페 라떼", "카페 리떼"));

        Assert.True(report.Ok, report.ToString());
        Assert.Single(report.Fuzzy);
    }

    [Fact]
    public void 이름의_공백_차이는_차이로_보지_않는다()
    {
        var report = Compare(Expected.Replace("카페 라떼", "카페라떼"));

        Assert.True(report.Ok, report.ToString());
        Assert.Empty(report.Fuzzy);
    }

    [Fact]
    public void 두자_이하_이름은_정확히_일치해야_한다()
    {
        var report = Compare(Expected.Replace("쿠폰", "옵션"));

        Assert.False(report.Ok);
    }

    [Fact]
    public void 완전히_다른_이름은_실패시킨다()
    {
        var report = Compare(Expected.Replace("I-G)카페 라떼", "T)카페 라떼"));

        Assert.False(report.Ok);
    }

    [Fact]
    public void 금액이_1원_다르면_실패시킨다()
    {
        var report = Compare(Expected.Replace("\"productPrice\": 6600", "\"productPrice\": 6599"));

        Assert.False(report.Ok);
    }

    [Fact]
    public void 부호가_다르면_실패시킨다()
    {
        var report = Compare(Expected.Replace("-2540", "2540"));

        Assert.False(report.Ok);
    }

    [Fact]
    public void 키_순서와_들여쓰기는_무시한다()
    {
        string actual = """
            {"review":"NONEED","address":null,
             "productItems":[{"productOptionItems":[{"optionPrice":-2540,"optionName":"쿠폰"}],
             "productUnit":1,"productPrice":6600,"productName":"I-G)카페 라떼"}],
             "totalOrderPrice":6600}
            """;

        var report = Compare(actual);

        Assert.True(report.Ok, report.ToString());
    }

    [Fact]
    public void 쉼표가_붙은_문자열_금액도_값으로_비교한다()
    {
        var report = Compare(Expected.Replace("\"totalOrderPrice\": 6600", "\"totalOrderPrice\": \"6,600\""));

        Assert.True(report.Ok, report.ToString());
    }

    [Fact]
    public void null과_0은_다르다()
    {
        var report = Compare(Expected.Replace("\"address\": null", "\"address\": 0"));

        Assert.False(report.Ok);
    }

    [Fact]
    public void null인_필드는_키가_없어도_통과시킨다()
    {
        string noAddress = Expected.Replace("\"address\": null,", "");

        var missing = Compare(noAddress);
        var extra = ReceiptDiff.Compare(noAddress, Expected);

        Assert.True(missing.Ok, missing.ToString());
        Assert.True(extra.Ok, extra.ToString());
    }

    [Fact]
    public void 항목_순서만_다르면_통과시킨다()
    {
        const string Two = """
            {
              "productItems": [
                { "productName": "옵션", "productPrice": 800 },
                { "productName": "쿠폰", "productPrice": -800 }
              ]
            }
            """;
        string swapped = """
            {
              "productItems": [
                { "productName": "쿠폰", "productPrice": -800 },
                { "productName": "옵션", "productPrice": 800 }
              ]
            }
            """;

        var report = ReceiptDiff.Compare(Two, swapped);

        Assert.True(report.Ok, report.ToString());
        Assert.Contains(report.Fuzzy, f => f.Contains("순서만 다름"));
    }

    [Fact]
    public void 순서가_같아도_값이_다르면_실패시킨다()
    {
        const string Two = """
            {
              "productItems": [
                { "productName": "옵션", "productPrice": 800 },
                { "productName": "쿠폰", "productPrice": -800 }
              ]
            }
            """;
        string wrongAmount = Two.Replace("-800", "-900");

        var report = ReceiptDiff.Compare(Two, wrongAmount);

        Assert.False(report.Ok);
    }

    [Fact]
    public void 항목_개수가_다르면_실패시킨다()
    {
        var report = Compare(Expected.Replace("\"productOptionItems\": [ { \"optionName\": \"쿠폰\", \"optionPrice\": -2540 } ]", "\"productOptionItems\": []"));

        Assert.False(report.Ok);
        Assert.Contains(report.Diffs, d => d.Contains("개수 다름"));
    }

    [Fact]
    public void 필드가_빠지거나_늘어나면_실패시킨다()
    {
        var missing = Compare(Expected.Replace("\"productUnit\": 1,", ""));
        var extra = Compare(Expected.Replace("\"review\": \"NONEED\"", "\"review\": \"NONEED\", \"tax\": 494"));

        Assert.Contains(missing.Diffs, d => d.Contains("응답에 없음"));
        Assert.Contains(extra.Diffs, d => d.Contains("정답에 없는 필드"));
    }

    [Fact]
    public void couponCalc는_채점하지_않는다()
    {
        string actual = Expected.Replace(
            "\"totalOrderPrice\": 6600",
            "\"couponCalc\": { \"couponTotal\": 2540, \"productRowCount\": 1, \"share\": 2540 }, \"totalOrderPrice\": 6600");

        var report = Compare(actual);

        Assert.True(report.Ok, report.ToString());
    }

    [Fact]
    public void JSON이_깨지면_파싱_실패를_보고한다()
    {
        var report = Compare("{ \"totalOrderPrice\": ");

        Assert.False(report.Ok);
        Assert.Contains(report.Diffs, d => d.Contains("파싱 실패"));
    }

    // ── 점수 ──────────────────────────────────────────────────────────────
    // Ok는 합/불만 주므로 프롬프트가 나아졌는지를 판단하지 못한다.
    // Score는 잎(스칼라 필드) 단위 정확도라 "17개 틀림"과 "1개 틀림"을 구분한다.

    [Fact]
    public void 완전히_같으면_만점이다()
    {
        var report = Compare(Expected);

        Assert.Equal(1.0, report.Score);
        Assert.Equal(8, report.Total);   // 이 정답이 가진 잎의 수
    }

    [Fact]
    public void 한_필드만_틀리면_그_잎_하나만_깎인다()
    {
        var report = Compare(Expected.Replace("\"productPrice\": 6600", "\"productPrice\": 6599"));

        Assert.False(report.Ok);
        Assert.Equal(8, report.Total);
        Assert.Equal(7, report.Matched);
    }

    [Fact]
    public void 오독으로_통과시킨_이름은_맞힌_것으로_센다()
    {
        var report = Compare(Expected.Replace("카페 라떼", "카페 리떼"));

        Assert.Equal(1.0, report.Score);
    }

    [Fact]
    public void 더_많이_틀릴수록_점수가_낮다()
    {
        var one = Compare(Expected.Replace("\"productPrice\": 6600", "\"productPrice\": 6599"));
        var two = Compare(Expected
            .Replace("\"productPrice\": 6600", "\"productPrice\": 6599")
            .Replace("\"totalOrderPrice\": 6600", "\"totalOrderPrice\": 1"));

        Assert.True(two.Score < one.Score, $"{two.Score} < {one.Score}");
    }

    [Fact]
    public void 비교하지_못한_잎도_오답으로_센다()
    {
        // 배열이 통째로 비면 그 안의 잎 2개(optionName, optionPrice)를 비교조차 못 한다.
        var report = Compare(Expected.Replace(
            "\"productOptionItems\": [ { \"optionName\": \"쿠폰\", \"optionPrice\": -2540 } ]",
            "\"productOptionItems\": []"));

        Assert.Equal(8, report.Total);
        Assert.Equal(6, report.Matched);
    }

    [Fact]
    public void 정답에_없는_필드는_페널티로_붙는다()
    {
        var report = Compare(Expected.Replace("\"review\": \"NONEED\"", "\"review\": \"NONEED\", \"tax\": 494"));

        Assert.Equal(9, report.Total);
        Assert.Equal(8, report.Matched);
    }

    [Fact]
    public void 파싱_실패는_0점이다()
    {
        var report = Compare("{ \"totalOrderPrice\": ");

        Assert.Equal(0.0, report.Score);
    }

    [Fact]
    public void 순서만_다르면_만점이다()
    {
        const string Two = """
            {
              "productItems": [
                { "productName": "옵션", "productPrice": 800 },
                { "productName": "쿠폰", "productPrice": -800 }
              ]
            }
            """;
        string swapped = """
            {
              "productItems": [
                { "productName": "쿠폰", "productPrice": -800 },
                { "productName": "옵션", "productPrice": 800 }
              ]
            }
            """;

        var report = ReceiptDiff.Compare(Two, swapped);

        Assert.Equal(1.0, report.Score);
        Assert.Equal(4, report.Total);
    }
}
