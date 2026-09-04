using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Common;

/// <summary>
/// 정답 없이 응답 하나만 보고 자기모순을 잡는다.
/// <para>
/// 영수증에 인쇄된 '합계'·'결제금액'은 모델이 그대로 옮겨 적는 값이고,
/// 항목들은 모델이 판단해서 만든 값이다. 둘이 안 맞으면 항목 쪽이 틀린 것이다.
/// </para>
/// <para>
/// 채점기(<see cref="ReceiptDiff"/>)와 달리 정답 파일이 필요 없다.
/// 정답을 만들지 않은 영수증에도 쓸 수 있다.
/// </para>
/// </summary>
public static class ReceiptCheck
{
    /// 규칙이 만들어내는 이름. 상품이 아니라 쿠폰 분할의 결과다.
    const string Coupon = "쿠폰";

    /// 어긋난 곳을 모두 모아 돌려준다. 비어 있으면 산수가 맞는 것이다.
    public static List<string> Verify(string json)
    {
        var problems = new List<string>();

        JsonNode? root;
        try { root = JsonNode.Parse(json); }
        catch (JsonException ex) { return [$"JSON 파싱 실패 - {ex.Message}"]; }

        if (root is not JsonObject o) return ["최상위가 객체가 아님"];

        var products = o["productItems"] as JsonArray ?? [];

        decimal items = 0, options = 0, coupons = 0;

        foreach (JsonNode? node in products)
        {
            if (node is not JsonObject p) continue;

            decimal price = Number(p["productPrice"]);
            if (Name(p["productName"]) == Coupon) coupons += price;
            else items += price;

            foreach (JsonNode? optionNode in p["productOptionItems"] as JsonArray ?? [])
            {
                if (optionNode is not JsonObject option) continue;

                decimal optionPrice = Number(option["optionPrice"]);
                if (Name(option["optionName"]) == Coupon) coupons += optionPrice;
                else options += optionPrice;
            }
        }

        decimal discounts = 0;
        foreach (JsonNode? node in o["discountItems"] as JsonArray ?? [])
            if (node is JsonObject d) discounts += Number(d["discountPrice"]);

        // ★ 바닥선 검산. 모든 항목을 더하면 결제금액이 나와야 한다.
        //   합계를 거치지 않으므로 할인이 합계 앞에 붙었는지 뒤에 붙었는지와 무관하다.
        decimal payment = Number(o["totalPaymentPrice"]);
        decimal sum = items + options + coupons + discounts;
        if (sum != payment)
            problems.Add($"결제금액이 항목과 안 맞음: totalPaymentPrice={payment}, " +
                         $"상품 {items} + 옵션 {options} + 쿠폰 {coupons} + 할인 {discounts} = {sum} " +
                         $"(차 {payment - sum})");

        // 합계 검산. 할인이 C 영역 'ㄴ' 행에서 왔으면 합계에 이미 반영돼 있고(8번),
        // D 영역 할인 행이면 합계 뒤에 차감된다. JSON만 봐서는 구분이 안 되므로 둘 다 인정한다.
        decimal order = Number(o["totalOrderPrice"]);
        if (items + options != order && items + options + discounts != order)
            problems.Add($"합계가 항목과 안 맞음: totalOrderPrice={order}, " +
                         $"상품 {items} + 옵션 {options} = {items + options} " +
                         $"(할인 {discounts}을 반영해도 {items + options + discounts})");

        // 결제수단의 합도 결제금액과 같아야 한다.
        var payments = o["paymentItems"] as JsonArray;
        if (payments is { Count: > 0 })
        {
            decimal paid = payments.OfType<JsonObject>().Sum(p => Number(p["paymentPrice"]));
            if (paid != payment)
                problems.Add($"결제수단 합이 안 맞음: totalPaymentPrice={payment}, 결제수단 합 {paid} " +
                             $"(차 {payment - paid})");
        }

        return problems;
    }

    static string Name(JsonNode? node) => node?.ToString().Trim() ?? "";

    static decimal Number(JsonNode? node)
    {
        if (node is null) return 0;
        string text = Regex.Replace(node.ToString(), @"[,\s원₩]", "");
        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal value)
            ? value
            : 0;
    }
}
