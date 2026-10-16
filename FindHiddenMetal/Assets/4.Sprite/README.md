# 숨은 금속 찾기 UI 이미지 교체 가이드

디자인 원본: `02-숨은 금속 찾기-*.psd` (세로 1200 x 1920)

같은 경로에 **같은 파일명으로 덮어쓰면** 씬/프리팹 수정 없이 바로 반영됩니다.
(파일을 지웠다가 새로 넣으면 연결이 끊어지니 반드시 덮어쓰기)

## PSD에서 다시 뽑기

PSD가 바뀌면 아래 명령 한 번으로 Static/Parts PNG를 모두 덮어씁니다.

```
pip install psd-tools pillow
python Docs/Design/export_psd.py "<PSD 경로>"
```

- 레이어 이름으로 찾으므로 PSD의 그룹/레이어 이름(예: `04 탐색/위치 입력 화면`, `정답 확인 버튼 (활성)`)을 바꾸면 스크립트도 같이 고칩니다.
- 부품 위치가 바뀌면 `Docs/Design/psd_layout.json`에 새 좌표가 나옵니다. 프리팹 배치(`MetalPrefabBuilder`)를 그 값으로 맞춥니다.
- 회전 대기 화면(Static_Ready)은 시안이 없어 카운트다운 화면 배경에 문구를 더해 만듭니다 (`READY_TEXT`).

## Static/ — 화면별 정적 레이어 (1200 x 1920)
| 파일 | 화면 | PSD 그룹 |
|---|---|---|
| Static_Idle.png | 대기 | 01 대기화면 (버튼 제외) |
| Static_Guide.png | 체험 방법 | 02 체험 방법 (체험시작 버튼 제외) |
| Static_Countdown.png | 카운트다운 | 03 카운트다운 (숫자 제외) |
| Static_Play.png | 탐색/위치 입력 | 04 (격자+칸 번호, 시간 원, "/ 8" 박스 포함) |
| Static_Result.png | 결과 | 05 (빈 격자, 범례, 남색 박스 라벨, 랭킹 박스+아이콘) |
| Static_Ready.png | 다음 체험 준비 중 (원판 회전 대기) | 03 배경 + 문구 |

**_Guide/Guide_xxx.png** 의 분홍 박스 자리는 동적 요소(버튼, 숫자, 격자 칸 마크, 랭킹 글자)가 올라가는 곳이니 비워 두세요.

**숫자가 들어간 고정 그림 주의** — 아래 값을 Setting.json에서 바꾸면 PNG도 같이 고쳐야 합니다.
- Static_Play: 격자 12x12와 칸 번호 1~144 (`gridSize`), 선택 수 박스의 "/ 8" (`baseTargets` 개수)
- Static_Result: 12x12 격자 (`gridSize`), 랭킹 박스 5줄 아이콘 (`rankingShowCount`)
- Static_Guide: STEP03 "제한시간 60초", "N개의 숨은 금속" (`timeLimitSeconds`, `baseTargets` 개수)
- 격자 칸 위치는 격자 바깥 선 기준 영역을 칸 수로 나눠 정합니다 (진행 1120x1082, 결과 800x784, 선 두께 2px).

## Parts/ — 동적 요소 부품
| 파일 | 용도 | 크기 / 비고 |
|---|---|---|
| Button_Start.png | 대기 "체험 시작하기" | 1030 x 230 (그림자 포함) |
| Button_Confirm.png | 체험 방법 "체험시작" | 1080 x 190 |
| Button_Submit.png / Button_Submit_Off.png | 진행 "정답 확인" 활성 / 비활성(칸을 고르기 전) | 380 x 170 (두 장 크기 같게) |
| Button_Home.png | 진행·결과 "HOME" | 380 x 170 |
| Cell_Select.png | 진행 화면 칸 선택 표시 | 92 x 88 (칸 크기에 맞춰 늘어남) |
| Mark_Correct.png | 결과: 고르지 않은 정답 위치 ("정답") | 65 x 63 |
| Mark_Match.png | 결과: 고른 칸 중 정답 ("정답 일치") | 65 x 63 |
| Mark_Wrong.png | 결과: 고른 칸 중 오답 | 65 x 63 |
| RankRow_Mine.png | 결과 "나의 랭킹" 주황 줄 (아이콘 포함) | 1050 x 90 |
| Badge_MyRank.png | "나의 랭킹" 배지 | 130 x 50 |
| Dot.png | 개발자용 정답 위치 표시(F2) | 64 x 64, **흰색** (색은 프리팹이 칠함) |

PNG가 없으면 프리팹 생성 때 분홍색 임시 이미지가 만들어지니 바로 눈에 띕니다.

## 글꼴
숫자 등 동적 글자는 `Assets/10.Font/Paperlogy`(PSD 글꼴)의 Dynamic TMP 폰트를 씁니다.
PSD의 숫자 글꼴(HG꼬딕씨)은 설치돼 있지 않아 Paperlogy Bold/ExtraBold로 대신합니다.

## Unity 메뉴 (Tools > 숨은 금속 찾기)
1. **프리팹 생성 및 씬 적용** — 없는 프리팹만 만들고 씬에 배치 (기존 것은 덮어쓰지 않음)
2. **가이드 PNG 다시 만들기** — 레이아웃을 바꾼 뒤 _Guide 이미지 갱신
4. **연결 검사** — 빠진 이미지, 비어 있는 인스펙터 연결 확인 (Tooltip에 "비워두면"이 있는 연결은 제외)
