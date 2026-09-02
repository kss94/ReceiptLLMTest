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
    public void JSON이_깨지면_파싱_실패를_보고한다()
    {
        var report = Compare("{ \"totalOrderPrice\": ");

        Assert.False(report.Ok);
        Assert.Contains(report.Diffs, d => d.Contains("파싱 실패"));
    }
}
