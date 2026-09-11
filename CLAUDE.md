# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## 이 저장소는 무엇인가

영수증 이미지 → JSON 추출 **프롬프트를 점수로 다루는 실험대**다. 제품 코드가 아니라
프롬프트를 고칠 때 나아졌는지 나빠졌는지 판단하기 위한 도구다. 코드는 거의 바뀌지 않고,
실제 작업 대상은 `LLMTests/<벤더>/SystemPrompt.txt`다.
(Starbucks 36KB·492줄, Megacoffee·Baemin 8KB·102줄)

주석·테스트 이름·프롬프트·문서가 전부 한국어다. 새로 쓰는 것도 한국어로 맞춘다.

## 명령

`dotnet test`는 이 저장소에서 **동작하지 않는다**. xunit.v3 + .NET 10 SDK 조합이라
VSTest 경로가 막혀 있고, 테스트 프로젝트를 실행 파일로 직접 돌려야 한다.

```bash
dotnet build
```

```bash
dotnet run --project LLMTests -- -class- "LLMTests.ReceiptTests"
```
API를 부르지 않는 테스트 전부(68개, 0.1초). 평소에는 이것만 돌린다.

```bash
dotnet run --project LLMTests -- -class "LLMTests.ReceiptDiffTests"
```
```bash
dotnet run --project LLMTests -- -method "LLMTests.ReceiptCheckTests.정답은_전부_산수가_맞는다"
```
클래스 하나 / 메서드 하나. 이름은 정규화된 전체 이름이고 `*` 와일드카드를 쓸 수 있다.
`-list tests`로 목록을 볼 수 있다.

`LLMTests.ReceiptTests`는 케이스 하나마다 실제 Gemini를 호출한다(지금 37건).
느리고 돈이 든다. **영수증 하나만 보고 싶으면 테스트가 아니라 Runner의 `--cases`를 쓴다.**

```bash
dotnet run --project Runner -- run --vendor Starbucks --repeat 2
```
```bash
dotnet run --project Runner -- optimize --vendor Starbucks --repeat 2 --iterations 5
```
키는 `Common/ApiKey.cs`에 박혀 있다. 인자 없이 `dotnet run --project Runner`를 치면
전체 옵션이 나온다. 자세한 설명은 [Runner/README.md](Runner/README.md)에 있다.

## 구조

- **Common** — 라이브러리. 호출(`ReceiptExtractor`), 채점(`ReceiptDiff`), 검산(`ReceiptCheck`),
  프롬프트 절 편집(`PromptDocument`), 벤더 폴더 규약(`Vendor`).
- **LLMTests** — xUnit. 벤더 데이터(프롬프트·정답·영수증 이미지)도 여기 들어 있다.
- **Runner** — `runner` CLI. 점수를 내고(run), 프롬프트를 자동으로 고친다(optimize).

### 벤더 폴더 규약

```
LLMTests/<벤더>/
  SystemPrompt.txt       규칙서
  UserPrompt.txt         JSON 형식
  <id>/answer.json       정답. 이 파일이 있는 폴더만 채점 대상이다
  <id>/*.jpg             한 폴더의 이미지 전부가 한 요청으로 간다
```

케이스 목록은 `Vendor.Cases()`가 벤더 폴더의 하위 폴더에서 읽는다. `answer.json`을 하나 넣으면
xUnit 케이스와 Runner 케이스가 동시에 하나 늘어난다. `[InlineData]`를 적을 곳은 없다.
벤더를 추가할 때는 클래스가 아니라 **`ReceiptTests`에 메서드를 하나 더 단다.**
벤더 이름은 문자열로 `[MemberData]`와 `RunCase`에 넘긴다. 두 줄을 복사해 폴더 이름만
바꾸면 되고, 하는 일은 `RunCase` 하나에 모여 있다.

지금 있는 벤더는 셋이다. 벤더는 계속 늘어난다.

| 벤더 | 케이스 | 대상 | `■` 절 |
|---|---|---|---|
| Starbucks | 22 | 종이·전자 영수증 | 14 |
| Megacoffee | 10 | 종이·전자 영수증 | 없음 |
| Baemin | 5 | 배달의민족 앱 '주문상세' 화면 캡처 | 없음 |

**프롬프트는 벤더마다 따로 둔다.** 겹쳐 보이는 규칙이 있어도 공통 프롬프트로 빼지 않는다.
한 벤더 프롬프트를 고쳐도 다른 벤더 점수는 움직이지 않는다.

케이스 수를 **박아 둔 테스트가 둘 있다.** 정답이나 절을 늘리면 같이 고친다.
`VendorTests.실제_벤더의_프롬프트와_케이스를_찾는다`(Starbucks 22건),
`PromptDocumentTests.실제_프롬프트의_절을_전부_찾는다`(Starbucks 절 14개).

프롬프트·정답·이미지는 bin으로 복사하지 않는다. `TestBase.ProjectDir()`와
`Options.RepoDir()`이 `[CallerFilePath]`로 **소스 폴더**를 잡는다. 빌드 출력의 사본을
읽으면 프롬프트를 고쳐도 반영이 안 돼 헛돌게 되므로 이 방식을 유지한다.

## 바꾸면 안 되는 것들

**호출 설정은 고정이다.** `ReceiptExtractor`의 모델(`gemini-3.5-flash-lite`),
`ThinkingLevel.Minimal`, `ResponseMimeType`은 손대지 않는다. 테스트와 Runner가 같은
클래스를 거치는 이유도 두 점수가 같은 뜻이어야 하기 때문이다. **정확도는 프롬프트로만 올린다.**
(`Proposer`는 예외다. 수정안을 내는 쪽이라 thinking을 막지 않고 모델도 `--proposer`로 바꾼다.)

**optimize는 원본 프롬프트를 덮어쓰지 않는다.** 결과는 `Runs/<벤더>/<시각>/SystemPrompt.txt`에만
쓴다. 확인하고 직접 복사한다.

## 점수와 노이즈

합/불이 아니라 잎(스칼라 필드) 단위 정확도로 본다. "17개 필드 틀림"과 "1개 필드 틀림"이
빨간 줄 하나로 같아 보이면 프롬프트를 고칠 수 없기 때문이다.

- `score` — 케이스별 점수의 평균. **후보 비교의 기준값**
- `fieldScore` — 필드 수 가중 평균

**temperature가 고정이 아니라 같은 프롬프트로 두 번 돌리면 점수가 다르다.** 그래서:
`--repeat 2` 이상으로 재고, 결과 JSON의 `maxSpread`보다 작은 점수 차이는 읽지 않는다.
케이스 하나가 깨졌다고 바로 고치지 말고 그 케이스만 `--repeat 3`으로 확인한다.

## 채점기 둘의 역할이 다르다

- `ReceiptDiff` — 정답과 비교한다. 금액·수량은 정확히 일치해야 하고, 이름 계열 필드
  (`storeName`, `storeSubName`, `productName`, `optionName`, `discountName`,
  `productDiscountName`, `paymentMethod`, `paymentName`)는 편집거리로 OCR 오독을 통과시킨다.
  2자 이하 이름('쿠폰', '할인')은 규칙이 만들어낸 값이므로 예외 없이 정확히 일치.
  이름 계열은 공백·대소문자 차이도 무시한다('starbucks' = 'STARBUCKS').
  정답 문자열에 `|`를 쓰면 그중 하나만 맞아도 통과한다(`"STARBUCKS|스타벅스"`).
  같은 것을 여러 표기로 부르는 필드에 쓴다. 브랜드 표기가 그렇다.
  키 없음과 `null`은 같은 뜻. 배열은 순서만 다르면 통과.
  최상위 `couponCalc`는 채점하지 않는다(아래 '근거 필드').
- `ReceiptCheck` — **정답 없이** 응답 하나만 보고 산수의 자기모순을 잡는다. 셋을 본다.
  항목(상품+옵션+쿠폰+할인) 합 = `totalPaymentPrice`,
  상품+옵션(할인 반영은 선택) = `totalOrderPrice`,
  결제수단 합 = `totalPaymentPrice`.
  정답을 만들지 않은 영수증에도 쓸 수 있어 운영 응답 검증용이다.
  `ReceiptCheckTests`가 **Starbucks 정답 22건**으로 이 검산이 성립함을 고정한다.

  ★ 이 검산은 모든 벤더에 성립하지 않는다. Baemin 정답은 배달팁이 항목이 아니라
  `paymentItems`에만 있어 세 식 중 둘이 깨진다. 정답이 틀린 게 아니다.
  검산을 벤더 전체로 넓히려면 배달팁을 어디로 셀지부터 정해야 한다.

## 프롬프트를 고칠 때

프롬프트는 벤더마다 따로 쓴다. 아래 절 번호와 규칙은 Starbucks 것이다.

Starbucks의 `SystemPrompt.txt`는 `■`로 시작하는 절 14개로 나뉜다
(`■ 0. 공통 원칙` … `■ 11. review`). optimize는 **절 하나만** 모델에게 돌려받고
갈아끼우기는 `PromptDocument`가 한다. 36KB를 통째로 다시 쓰게 하면 고치라고 하지 않은
490줄이 흔들리기 때문이다. 절 제목을 바꾸거나 절을 추가·삭제하면 optimize의
안전장치(절 개수 비교)가 무너진다.

절의 내용은 전부 스타벅스 영수증 전용 규칙이다(영역 A~F 구분, 쿠폰 분할, 상품에 딸린
'ㄴ' 옵션 행 …). 일반 영수증 규칙으로 읽지 말고, 고칠 일이 있으면 프롬프트를 직접 읽는다.

`■` 절은 벤더가 **쓸 수도 있고 안 쓸 수도 있다.** 지켜야 하는 규약이 아니라 optimize를
절 단위로 돌리기 위한 장치다. `PromptDocument`가 `■` 줄만 절 제목으로 보니, 쓰는
프롬프트는 절 하나만 갈아끼우고 안 쓰는 프롬프트는(지금 Megacoffee와 Baemin에 `■`가
하나도 없다) optimize가 고칠 절을 못 고른다. 그 프롬프트는 직접 고치면 된다.

### 벤더를 가로지르는 두 가지

- **브랜드 번호표.** 세 프롬프트 모두 맨 위 세 줄이 `1144: Starbucks` / `1142: 메가엠지씨커피`
  / `1033: 이마트`다. `storeName`은 폴더 이름이 아니라 **이미지에서 판별**하고,
  `receiptBrandId`는 판별한 이름을 이 표에서 찾은 정수다. 표에 없으면 `null`이다
  (배달의민족이 그렇다. 비슷한 브랜드 번호를 대신 넣으면 안 된다).
- **근거 필드.** Starbucks `UserPrompt.txt`의 `couponCalc`는 쿠폰 분할의 계산 과정을
  먼저 적게 하는 자리다. 규칙을 세게 쓰는 것보다 근거를 먼저 쓰게 하는 쪽이 오카운트를 줄였다.
  채점에서는 빠지지만(`ReceiptDiff.IgnoredRootFields`) 값은 `<id>.actual.json`에 그대로
  남으니 실패를 들여다볼 때 여기부터 본다.

코드와 얽힌 것은 부호 하나뿐이다. `discountPrice`는 이 저장소에서 **음수**이고
`ReceiptCheck`의 검산식이 그 부호에 의존한다. 실서비스는 양수 규약이니 옮길 때 확인한다.

## 실패를 들여다볼 때

Runner는 `<out>/<id>.actual.json`(모델 응답 원본)과 `<out>/<id>.diff.txt`(차이),
그리고 `<out>/result.json`(점수 전체)을 남긴다.
`--repeat`이 2 이상이면 **가장 나쁜 시도**를 남긴다. 볼 것은 잘 된 쪽이 아니라 깨진 쪽이다.
optimize는 시도마다 `iter-NN/` 폴더와 무엇을 왜 고쳤는지 적은 `history.json`을 남긴다.
`Runs/`는 git에 올라가지 않는다.

xUnit으로 돌렸을 때는 같은 두 파일이 `LLMTests/bin/<구성>/net10.0/Diffs/<벤더>/`에 쌓인다.
