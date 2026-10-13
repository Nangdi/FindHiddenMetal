# 숨은 금속 찾기 UI 이미지 교체 가이드

같은 경로에 **같은 파일명으로 덮어쓰면** 씬/프리팹 수정 없이 바로 반영됩니다.
(파일을 지웠다가 새로 넣으면 연결이 끊어지니 반드시 덮어쓰기)

## Static/ — 화면별 정적 레이어 (1920 x 1200)
| 파일 | 화면 |
|---|---|
| Static_Idle.png | 대기 |
| Static_Guide.png | 체험 방법 |
| Static_Play.png | 체험 진행 (격자 틀, 열 A~P / 행 1~16 표기, 정보 칸, 범례) |
| Static_Result.png | 결과 (기록 라벨, 랭킹 박스, 안내 문구) |
| Static_Ready.png | 다음 체험 준비 중 (원판 회전 대기, 화면 전체) |

배경, 제목, 설명글, 고정 안내 문구 등 변하지 않는 것은 모두 이 한 장에 그립니다.
**_Guide/Guide_xxx.png** 의 분홍 박스 자리는 동적 요소(버튼, 격자 칸, 숫자, 랭킹 줄)가 올라가는 곳이니 비워 두세요.

**숫자가 들어간 고정 문구 주의** — 아래 값을 Setting.json에서 바꾸면 PNG도 같이 고쳐야 합니다.
- Static_Idle / Static_Guide / Static_Play: "금속 8개", "최대 8개", "틀리면 기록에 5초" (`baseTargets` 개수, `wrongPenaltySeconds`)
- Static_Play: 격자 16x16과 열/행 표기 (`gridSize`). 칸 크기 62px, 간격 2px, 격자 중심 (-350, -40)

## Parts/ — 동적 요소 부품
| 파일 | 용도 | 크기 / 비고 |
|---|---|---|
| Button_Start.png | 대기 화면 "체험 시작" | 520 x 130 |
| Button_Confirm.png | 체험 방법 "시작하기" | 520 x 130 |
| Button_Submit.png | 진행 화면 "위치 확인" | 520 x 130 (칸을 고르기 전에는 흐리게 표시됨) |
| Button_Again.png / Button_Home.png | 결과 화면 "다시 체험" / "처음으로" | 360 x 110 |
| Result_Title_Success.png / Result_Title_Fail.png | 결과 제목 "탐색 완료!" / "시간 종료" | 900 x 140 |
| Mark_Wrong.png | 오답 X 표시 (설정창 keepWrongMark: 다음 위치 확인까지 남김 / 잠깐 보였다 사라짐) | 64 x 64, 투명 배경 |
| Dot.png | 찾은 금속(초록) / 못 찾은 금속(노랑) 표시 | 64 x 64, **흰색**으로 (색은 프리팹이 칠함) |
| Cell_Bg.png | 격자 한 칸, 선택 표시 | 9-slice, **흰색**으로 (색은 프리팹이 칠함) |
| RankRow_Bg.png | 랭킹 한 줄 배경 | 9-slice, **흰색**으로 (색은 프리팹이 칠함) |
| Card_Bg.png | 박스 배경 (예비) | 9-slice |

9-slice 이미지는 모서리 크기가 바뀌면 Sprite Editor에서 Border 값을 맞춰 주세요.

## Unity 메뉴 (Tools > 숨은 금속 찾기)
1. **프리팹 생성 및 씬 적용** — 없는 이미지/프리팹만 만들고 씬에 배치 (기존 것은 덮어쓰지 않음)
2. **가이드 PNG 다시 만들기** — 레이아웃을 바꾼 뒤 _Guide 이미지 갱신
3. **임시 스프라이트 다시 내보내기** — Static/Parts를 임시 디자인으로 모두 덮어씀 (주의)
4. **연결 검사** — 빠진 이미지, 비어 있는 인스펙터 연결 확인
