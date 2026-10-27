# 소리 (배경음악·효과음)

실행할 때 이 폴더의 파일을 읽습니다. 빌드 후에도 `<앱>_Data/StreamingAssets/Audio/` 의 파일만 바꾸면 다시 빌드하지 않고 반영됩니다 (앱 재시작 필요).

- **지금 들어 있는 파일은 `Tools/make_placeholder_sounds.py` 가 코드로 직접 합성한 임시 소리입니다** (외부 음원을 쓰지 않아 저작권 문제 없음).
  콘셉트(원판 아래 숨은 금속을 찾는 과학 탐구)에 맞춰 마림바·금속 종 느낌의 탐험 배경음악과 탐지기 삑·금속 '팅' 효과음으로 만들었습니다.
  실제 소리를 받으면 같은 파일명으로 덮어쓰세요. (다른 이름이나 mp3/ogg 로 넣으려면 `sound.json` 의 `file` 을 바꿉니다)
- 파일 형식: wav, ogg, mp3
- 파일이 없거나 깨져 있으면 그 소리만 나지 않고 앱은 계속 동작합니다 (로그에 `[Sound]` 로 남음, 같은 경고는 한 번만).
- 볼륨: ESC 설정창의 조절바(전체·배경음악·효과음)와 음소거(배경음악·효과음), 또는 persistent `Setting.json` 의 `masterVolume` · `bgmVolume` · `sfxVolume`(0~1), `bgmMuted` · `sfxMuted`.
  음소거해도 볼륨 값은 그대로라 풀면 원래 크기로 돌아옵니다. 배경음악이 겹쳐 넘어가는 시간은 `bgmFadeSeconds`(설정창에도 있음).
  소리마다 따로 줄이려면 `sound.json` 의 `volume`(0~1)을 바꿉니다.
- 같은 효과음을 0.08초 안에 다시 부르면 무시합니다 (겹쳐서 커지지 않게).
- 임시 소리를 다시 만들려면: `python Tools/make_placeholder_sounds.py` (없는 파일만 만듦, `--force` 는 모두 다시. numpy 필요)

## sound.json

```json
{
    "bgm": [ { "key": "idle", "file": "bgm_idle.wav", "volume": 1.0 } ],
    "sfx": [ { "key": "count", "file": "sfx_count.wav", "volume": 1.0 } ]
}
```
`key` 는 프로그램이 부르는 이름이라 바꾸면 안 됩니다.

## 배경음악 (반복 재생, 바뀔 때 `bgmFadeSeconds` 동안 겹쳐 넘어감)

| key | 파일 | 나오는 때 |
|---|---|---|
| `idle` | bgm_idle.wav | 대기 화면, 체험 안내, 정답 편집(F3) |
| `play` | bgm_play.wav | 카운트다운, 탐색(칸 고르기) |
| `result` | bgm_result.wav | 결과 화면, 원판 회전 대기 |

## 효과음

| key | 파일 | 나오는 때 |
|---|---|---|
| `button` | sfx_button.wav | 버튼을 눌렀을 때 (체험 안내 확인, HOME, 편집 화면 버튼 등. 아래 start·submit 버튼 제외) |
| `start` | sfx_start.wav | 대기 화면 "체험 시작하기" |
| `submit` | sfx_submit.wav | 탐색 화면 "정답 확인" |
| `count` | sfx_count.wav | 카운트다운 3, 2, 1 |
| `go` | sfx_go.wav | 탐색 시작 (카운트다운이 끝났을 때) |
| `select` | sfx_select.wav | 칸을 골랐을 때 |
| `deselect` | sfx_deselect.wav | 고른 칸을 다시 눌러 풀었을 때 |
| `full` | sfx_full.wav | 8칸을 다 고른 뒤 다른 칸을 눌렀을 때 (더 고를 수 없음) |
| `tick` | sfx_tick.wav | 남은 시간 5초부터 1초마다 |
| `timeup` | sfx_timeup.wav | 제한 시간이 끝났을 때 |
| `picks` | sfx_picks.wav | 결과 화면 ① 내가 고른 칸을 모두 표시할 때 |
| `correct` | sfx_correct.wav | 결과 화면 ② 고른 칸을 한 칸씩 판정할 때 - 정답 일치 |
| `wrong` | sfx_wrong.wav | 결과 화면 ② 고른 칸을 한 칸씩 판정할 때 - 오답 |
| `reveal` | sfx_reveal.wav | 결과 화면 ③ 전체 정답 공개 (못 찾은 정답을 한꺼번에 보여 줄 때) |
| `success` | sfx_success.wav | 결과 공개가 끝나고 랭킹이 나타날 때 - 8개 모두 찾음 |
| `finish` | sfx_finish.wav | 결과 공개가 끝나고 랭킹이 나타날 때 - 모두 찾지는 못함 |

결과 화면 순서: ① 내가 고른 칸 전체 표시 → ② 고른 순서대로 한 칸씩 맞음/틀림 → ③ 전체 정답 공개 → 랭킹.
② 의 간격은 `Setting.json` 의 `revealStepSeconds`(기본 0.6초, ESC 설정창에도 있음)이고, 0이면 공개 연출 없이 한꺼번에 보여 줍니다.
