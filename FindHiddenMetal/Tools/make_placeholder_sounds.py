# -*- coding: utf-8 -*-
"""
임시 소리 만들기. 실제 소리 파일을 받기 전에 쓰는 합성음(wav)을 코드로 만든다 (외부 음원 없음, 저작권 문제 없음).
콘셉트: 숨은 금속 찾기 - 원판 아래 숨은 금속을 탐지기로 찾는 과학 탐구 체험.
  → 마림바·벨 같은 맑은 탐험 BGM + 탐지기 삑 소리, 금속이 '팅' 울리는 효과음.

  python Tools/make_placeholder_sounds.py          없는 파일만 만든다 (받아 넣은 실제 소리는 그대로 둔다)
  python Tools/make_placeholder_sounds.py --force  모두 다시 만든다

출력: Assets/StreamingAssets/Audio/*.wav (44.1kHz 16bit 스테레오), sound.json (없을 때만, 있으면 없는 키만 추가)
필요: pip install numpy
"""
import json
import os
import sys
import wave

import numpy as np

PROJECT = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), ".."))
OUT = os.path.join(PROJECT, "Assets", "StreamingAssets", "Audio")
RATE = 44100
rng = np.random.default_rng(7)


# ── 기본 소리 ─────────────────────────────────────────

def t_axis(seconds):
    return np.arange(int(RATE * seconds)) / RATE


def hz(n):  # 미디 번호 → Hz
    return 440.0 * 2 ** ((n - 69) / 12)


def osc(freq, seconds, shape="sine", phase=0.0):
    """freq 는 숫자 또는 시간별 배열(음높이가 변하는 소리)"""
    n = int(RATE * seconds)
    f = np.full(n, float(freq)) if np.isscalar(freq) else np.asarray(freq, float)[:n]
    ph = phase + np.cumsum(f) / RATE
    if shape == "saw":
        return 2 * (ph - np.floor(ph + 0.5))
    if shape == "square":
        return np.sign(np.sin(2 * np.pi * ph))
    if shape == "tri":
        return 2 * np.abs(2 * (ph - np.floor(ph + 0.5))) - 1
    return np.sin(2 * np.pi * ph)


def adsr(n, a=0.01, d=0.1, s=0.7, r=0.1):
    a, d, r = int(RATE * a), int(RATE * d), int(RATE * r)
    env = np.full(n, s)
    a = min(a, n)
    env[:a] = np.linspace(0, 1, a, endpoint=False) if a else env[:a]
    d = min(d, n - a)
    if d > 0: env[a:a + d] = np.linspace(1, s, d)
    r = min(r, n)
    if r > 0: env[-r:] *= np.linspace(1, 0, r)
    return env


def lowpass(x, cutoff):
    """한 극 저역 통과. cutoff 는 숫자 또는 시간별 배열(Hz)"""
    if np.isscalar(cutoff):
        # 고정 차단 주파수: 같은 응답의 지수 커널 합성곱 (반복문보다 훨씬 빠름)
        alpha = 1 - np.exp(-2 * np.pi * float(cutoff) / RATE)
        k = int(np.ceil(np.log(1e-5) / np.log(1 - alpha))) + 1
        kernel = alpha * (1 - alpha) ** np.arange(k)
        return np.convolve(x, kernel)[:len(x)]
    c = np.asarray(cutoff, float)[:len(x)]
    alpha = 1 - np.exp(-2 * np.pi * c / RATE)
    y = np.empty_like(x)
    acc = 0.0
    for i in range(len(x)):
        acc += alpha[i] * (x[i] - acc)
        y[i] = acc
    return y


def lp(x, cutoff, poles=2):
    for _ in range(poles):
        x = lowpass(x, cutoff)
    return x


def highpass(x, cutoff):
    return x - lowpass(x, cutoff)


def noise(seconds):
    return rng.uniform(-1, 1, int(RATE * seconds))


def supersaw(freq, seconds, voices=5, detune=0.12):
    """살짝 어긋나게 겹친 톱니파 (신스웨이브 패드·리드)"""
    out = np.zeros(int(RATE * seconds))
    for v in range(voices):
        cents = (v - (voices - 1) / 2) * detune * 100 / max(1, (voices - 1) / 2)
        out += osc(freq * 2 ** (cents / 1200), seconds, "saw", rng.random())
    return out / voices


def place(out, start, s, gain=1.0):
    i = int(RATE * start)
    if i >= len(out): return
    n = min(len(s), len(out) - i)
    out[i:i + n] += s[:n] * gain


def place_loop(out, start, s, gain=1.0):
    """루프 끝을 넘는 부분은 앞으로 감아 붙인다 (이음매 없이 반복되게)"""
    i = int(RATE * start) % len(out)
    for k in range(0, len(s), len(out)):
        part = s[k:k + len(out)]
        end = i + len(part)
        if end <= len(out):
            out[i:end] += part * gain
        else:
            cut = len(out) - i
            out[i:] += part[:cut] * gain
            out[:end - len(out)] += part[cut:] * gain


def echo_loop(x, delay, feedback=0.35, mix=0.3, taps=4):
    """루프용 메아리 (꼬리를 앞으로 감는다)"""
    out = x.copy()
    d = int(RATE * delay)
    for k in range(1, taps + 1):
        out += np.roll(x, d * k) * (feedback ** k) * mix
    return out


def echo(x, delay, feedback=0.35, mix=0.3, taps=4):
    d = int(RATE * delay)
    out = np.concatenate([x, np.zeros(d * taps)])
    for k in range(1, taps + 1):
        out[d * k:d * k + len(x)] += x * (feedback ** k) * mix
    return out


def stereo(left, right=None, width=0.0):
    right = left if right is None else right
    return np.stack([left * (1 - width * 0.5) + right * width * 0.5, right * (1 - width * 0.5) + left * width * 0.5], 1)


def normalize(x, peak=0.85):
    m = np.abs(x).max()
    return x / m * peak if m > 0 else x


# ── 드럼 머신 ─────────────────────────────────────────

def kick(seconds=0.35):
    t = t_axis(seconds)
    f = 45 + 120 * np.exp(-t * 35)
    return osc(f, seconds) * np.exp(-t * 9) + noise(seconds) * np.exp(-t * 200) * 0.15


def snare(seconds=0.25):
    t = t_axis(seconds)
    body = osc(190, seconds) * np.exp(-t * 25) * 0.5
    return body + highpass(noise(seconds), 1500) * np.exp(-t * 18) * 0.8


def hat(seconds=0.05, open_=False):
    t = t_axis(seconds)
    return highpass(noise(seconds), 7000) * np.exp(-t * (12 if open_ else 70)) * 0.5



# ── 악기 (탐험·금속 콘셉트) ──────────────────────────────

def mallet(freq, seconds=0.6, bright=0.3):
    """마림바 같은 나무 타악기 (기음 + 4배음, 짧게 사라짐)"""
    t = t_axis(seconds)
    s = osc(freq, seconds) * np.exp(-t * 7) + osc(freq * 4, seconds) * np.exp(-t * 22) * bright
    s += osc(freq * 9.2, seconds) * np.exp(-t * 60) * bright * 0.3
    return s * adsr(len(t), 0.002, 0.0, 1.0, 0.03)


BELL_PARTIALS = [(1.0, 1.0, 2.2), (2.76, 0.55, 3.5), (5.40, 0.35, 5.5), (8.93, 0.2, 8.0), (13.34, 0.1, 12.0)]


def bell(freq, seconds=1.2, decay=1.0):
    """금속 종 (어긋난 배음 = 금속 특유의 '팅')"""
    t = t_axis(seconds)
    s = np.zeros(len(t))
    for ratio, amp, d in BELL_PARTIALS:
        if freq * ratio > RATE * 0.45: continue  # 재생 가능한 주파수를 넘는 배음은 뺀다 (접힘 잡음 방지)
        s += osc(freq * ratio, seconds, phase=rng.random()) * amp * np.exp(-t * d * decay)
    return s * adsr(len(t), 0.001, 0.0, 1.0, 0.05)


def blip(freq, seconds=0.08, shape="sine"):
    """탐지기 삑"""
    s = osc(freq, seconds, shape)
    return s * adsr(len(s), 0.003, 0.02, 0.7, 0.03)


def soft_pad(chord, seconds, cut=900):
    """부드러운 화음 바탕 (세모파 + 살짝 어긋난 톱니파를 낮게 거른다)"""
    out = np.zeros(int(RATE * seconds))
    for note in chord:
        out += osc(hz(note), seconds, "tri", rng.random()) * 0.6
        out += lp(supersaw(hz(note), seconds, 3, 0.08), cut) * 0.4
    return out / max(1, len(chord)) * adsr(len(out), 0.5, 0.3, 0.85, 0.6)


# ── 배경음악 ─────────────────────────────────────────

def explore(bpm, bars, chords, melody, *, drums="none", bass=True, sonar=False, pulse=False, pad_gain=0.5, mel_gain=0.5):
    """chords: 마디별 화음(미디 번호), melody: 마디별 8분음 8개(0 = 쉼)"""
    beat = 60 / bpm
    length = bars * 4 * beat
    n = int(RATE * length)
    pad_l, pad_r, low, mel_l, mel_r, fx, drum = (np.zeros(n) for _ in range(7))

    for bar in range(bars):
        chord = chords[bar % len(chords)]
        t0 = bar * 4 * beat
        p = soft_pad(chord, 4 * beat + 0.6)
        place_loop(pad_l, t0, p, pad_gain)
        place_loop(pad_r, t0, np.roll(p, int(RATE * 0.012)), pad_gain)

        # 베이스: 근음 2분음 (pulse 면 8분음으로 걸어간다)
        if bass:
            root = chord[0] - 12
            steps = 8 if pulse else 2
            dur = 4 * beat / steps
            for e in range(steps):
                note = root + (7 if pulse and e % 4 == 3 else 0)
                s = osc(hz(note), dur, "tri") * 0.7 + osc(hz(note), dur) * 0.5
                s = lp(s, 700) * adsr(len(s), 0.005, 0.1, 0.6, 0.05)
                place_loop(low, t0 + e * dur, s, 0.55)

        # 선율: 마림바
        for e, note in enumerate(melody[bar % len(melody)]):
            if not note: continue
            s = mallet(hz(note), 0.7)
            place_loop(mel_l if e % 2 == 0 else mel_r, t0 + e * beat / 2, s, mel_gain)
            place_loop(mel_r if e % 2 == 0 else mel_l, t0 + e * beat / 2, s, mel_gain * 0.45)

        # 탐지기 음파(소나): 두 마디마다 멀리서 울리는 핑
        if sonar and bar % 2 == 0:
            ping = osc(hz(chord[-1] + 24), 1.4) * np.exp(-t_axis(1.4) * 3.5) * 0.5
            place_loop(fx, t0 + 2 * beat, ping, 0.35)

        # 드럼 (가볍게)
        for b in range(4):
            tb = t0 + b * beat
            if drums in ("light", "full"):
                if b in (0, 2) or drums == "full":
                    place_loop(drum, tb, kick(0.3), 0.45 if drums == "light" else 0.55)
                place_loop(drum, tb + beat / 2, hat(), 0.18)
            if drums == "full":
                if b in (1, 3): place_loop(drum, tb, snare(0.18), 0.22)
                place_loop(drum, tb + beat / 4, hat(0.03), 0.08)
                place_loop(drum, tb + beat * 3 / 4, hat(0.03), 0.08)

    mel_l = echo_loop(mel_l, beat * 0.75, 0.3, 0.35, 3)
    mel_r = echo_loop(mel_r, beat * 0.5, 0.3, 0.35, 3)
    fx = echo_loop(fx, beat * 1.5, 0.45, 0.6, 4)

    left = pad_l + low + mel_l + fx + drum
    right = pad_r + low + mel_r + fx * 0.8 + drum
    # 모든 소리를 place_loop / echo_loop 로 앞으로 감아 붙였으므로 끝과 처음이 그대로 이어진다 (이음매 페이드 불필요)
    out = np.stack([left, right], 1)
    return normalize(out, 0.8)


# ── 효과음 ───────────────────────────────────────────

def sfx_button():
    # 부드러운 '톡' (짧은 마림바 + 아주 짧은 딸깍)
    out = np.zeros(int(RATE * 0.25))
    place(out, 0, mallet(hz(84), 0.25, 0.2), 0.8)
    place(out, 0, highpass(noise(0.01), 3000) * np.exp(-t_axis(0.01) * 400), 0.25)
    return out


def sfx_start():
    # 탐지기 전원 켜짐: 위로 훑는 소나 + 두 음 차임
    t = t_axis(0.35)
    sweep = osc(400 * (6 ** (t / 0.35)), 0.35) * adsr(len(t), 0.01, 0.1, 0.7, 0.1) * 0.35
    out = np.zeros(int(RATE * 1.0))
    place(out, 0, sweep)
    place(out, 0.3, bell(hz(79), 0.7), 0.35)
    place(out, 0.42, bell(hz(86), 0.6), 0.35)
    return echo(out, 0.11, 0.3, 0.3, 3)


def sfx_submit():
    # 정답 확인: 탐지기 스캔 (빠른 삑삑삑 + 확정음)
    out = np.zeros(int(RATE * 0.9))
    for k in range(4):
        place(out, k * 0.07, blip(1400 + k * 200, 0.05), 0.4)
    place(out, 0.3, mallet(hz(79), 0.5), 0.6)
    place(out, 0.3, mallet(hz(86), 0.5), 0.5)
    return echo(out, 0.1, 0.3, 0.25, 3)


def sfx_count():
    # 카운트 삑 (탐지기 소리 높이)
    return blip(hz(81), 0.18) * 0.6 + blip(hz(93), 0.18) * 0.25


def sfx_go():
    # 탐색 시작: 높은 삑 + 올라가는 금속 화음
    out = np.zeros(int(RATE * 1.0))
    place(out, 0, blip(hz(93), 0.3), 0.5)
    for k, n in enumerate((81, 86, 88, 93)):
        place(out, 0.05 + k * 0.05, bell(hz(n), 0.8), 0.18)
    return echo(out, 0.12, 0.3, 0.25, 3)


def sfx_select():
    # 칸 고르기: 탐지기 '삡' (높게 올라가는 짧은 음)
    t = t_axis(0.09)
    s = osc(1200 * (1.4 ** (t / 0.09)), 0.09) * adsr(len(t), 0.002, 0.02, 0.7, 0.04)
    out = np.zeros(int(RATE * 0.2))
    place(out, 0, s, 0.6)
    place(out, 0, mallet(hz(88), 0.2, 0.1), 0.3)
    return out


def sfx_deselect():
    # 칸 풀기: 내려가는 짧은 음
    t = t_axis(0.09)
    s = osc(1300 * (0.65 ** (t / 0.09)), 0.09) * adsr(len(t), 0.002, 0.02, 0.6, 0.04)
    return np.concatenate([s, np.zeros(int(RATE * 0.05))]) * 0.5


def sfx_full():
    # 8칸을 다 골라 더 못 고름: 낮은 두 번 '뚜둑'
    out = np.zeros(int(RATE * 0.3))
    for k in range(2):
        s = lp(osc(hz(50), 0.08, "square"), 1200) * adsr(int(RATE * 0.08), 0.002, 0.02, 0.6, 0.03)
        place(out, k * 0.11, s, 0.5)
    return out


def sfx_tick():
    # 남은 시간 경고: 시계 '틱' + 높은 삑
    t = t_axis(0.12)
    click = highpass(noise(0.12), 2500) * np.exp(-t * 120) * 0.5
    ping = osc(hz(96), 0.12) * np.exp(-t * 35) * 0.5
    return click + ping


def sfx_timeup():
    # 시간 종료: 내려가는 세 음 + 짧은 버저
    out = np.zeros(int(RATE * 1.0))
    for k, n in enumerate((79, 75, 72)):
        s = lp(osc(hz(n), 0.18, "square"), 2500) * adsr(int(RATE * 0.18), 0.003, 0.05, 0.6, 0.06)
        place(out, k * 0.15, s, 0.4)
    buzzer = lp(osc(hz(48), 0.4, "saw") + osc(hz(48) * 1.01, 0.4, "square"), 1500) * adsr(int(RATE * 0.4), 0.01, 0.1, 0.6, 0.15)
    place(out, 0.45, buzzer, 0.35)
    return out


def sfx_picks():
    # 내가 고른 칸 표시: 탐지기가 판 전체를 훑는 스캔 (삑 소리가 점점 빨라지며 올라감)
    out = np.zeros(int(RATE * 1.0))
    t, gap = 0.0, 0.11
    for k in range(8):
        place(out, t, blip(900 + k * 90, 0.05), 0.3 + k * 0.03)
        t += gap
        gap *= 0.88
    place(out, t, mallet(hz(86), 0.5, 0.2), 0.4)
    return echo(out, 0.09, 0.25, 0.2, 3)


def sfx_correct():
    # 정답 일치: 금속이 '팅' 울림 + 밝은 탐지 삑 (금속 찾았다!)
    out = np.zeros(int(RATE * 1.0))
    place(out, 0, blip(hz(88), 0.06), 0.35)
    place(out, 0.06, blip(hz(95), 0.09), 0.35)
    place(out, 0.06, bell(hz(84), 0.9), 0.45)
    return echo(out, 0.09, 0.25, 0.25, 3)


def sfx_wrong():
    # 오답: 낮고 둔한 '붕' (금속 없음)
    t = t_axis(0.35)
    f = hz(52) * (0.85 ** (t / 0.35))
    s = lp(osc(f, 0.35, "square") * 0.5 + osc(f, 0.35, "tri") * 0.5, 900) * adsr(len(t), 0.005, 0.1, 0.6, 0.15)
    thud = osc(90, 0.15) * np.exp(-t_axis(0.15) * 30)
    out = np.zeros(int(RATE * 0.4))
    place(out, 0, s, 0.7)
    place(out, 0, thud, 0.5)
    return out


def sfx_reveal():
    # 못 찾은 정답 공개: 금속 종이 흩어져 울림 (반짝)
    out = np.zeros(int(RATE * 1.4))
    for k, n in enumerate((76, 83, 79, 86, 81, 88)):
        place(out, k * 0.05, bell(hz(n), 0.9, 1.3), 0.2)
    return echo(out, 0.13, 0.35, 0.3, 3)


def sfx_success():
    # 모두 찾음: 올라가는 마림바 + 금속 종 화음 팡파르
    out = np.zeros(int(RATE * 2.2))
    for k, n in enumerate((72, 76, 79, 84)):
        place(out, k * 0.1, mallet(hz(n), 0.4), 0.5)
    for n in (84, 88, 91, 96):
        place(out, 0.42, bell(hz(n), 1.6, 0.7), 0.2)
    place(out, 0.42, soft_pad([60, 64, 67, 72], 1.6, 2000), 0.5)
    return echo(out, 0.14, 0.3, 0.3, 3)


def sfx_finish():
    # 결과 마무리(모두 찾지는 못함): 따뜻하게 내려앉는 두 화음
    out = np.zeros(int(RATE * 1.8))
    for k, n in enumerate((79, 76, 72)):
        place(out, k * 0.12, mallet(hz(n), 0.5), 0.45)
    place(out, 0.36, soft_pad([53, 60, 64, 69], 1.3, 1600), 0.6)
    place(out, 0.36, bell(hz(81), 1.2, 0.9), 0.15)
    return echo(out, 0.14, 0.3, 0.25, 3)


# ── 저장 ─────────────────────────────────────────────

def save(name, samples, force, peak=0.85):
    path = os.path.join(OUT, name)
    if os.path.exists(path) and not force:
        print("skip", name)
        return
    if samples.ndim == 1:
        samples = stereo(normalize(samples, peak))
    data = (np.clip(samples, -1, 1) * 32767).astype(np.int16)
    with wave.open(path, "wb") as f:
        f.setnchannels(2)
        f.setsampwidth(2)
        f.setframerate(RATE)
        f.writeframes(data.tobytes())
    print("write", name, f"{len(samples) / RATE:.1f}s")


SFX = [
    # (key, 함수, 최대 크기, sound.json 볼륨)
    ("button", sfx_button, 0.6, 0.8),
    ("start", sfx_start, 0.85, 1.0),
    ("submit", sfx_submit, 0.85, 1.0),
    ("count", sfx_count, 0.5, 1.0),
    ("go", sfx_go, 0.85, 1.0),
    ("select", sfx_select, 0.7, 0.8),
    ("deselect", sfx_deselect, 0.6, 0.6),
    ("full", sfx_full, 0.45, 0.6),
    ("tick", sfx_tick, 0.8, 0.9),
    ("timeup", sfx_timeup, 0.7, 0.8),
    ("picks", sfx_picks, 0.55, 0.9),
    ("correct", sfx_correct, 0.85, 1.0),
    ("wrong", sfx_wrong, 0.55, 1.0),
    ("reveal", sfx_reveal, 0.9, 1.0),
    ("success", sfx_success, 0.85, 1.0),
    ("finish", sfx_finish, 0.85, 1.0),
]

TABLE = {
    "bgm": [
        {"key": "idle", "file": "bgm_idle.wav", "volume": 0.9},
        {"key": "play", "file": "bgm_play.wav", "volume": 0.8},
        {"key": "result", "file": "bgm_result.wav", "volume": 0.9},
    ],
    "sfx": [{"key": k, "file": f"sfx_{k}.wav", "volume": v} for k, _, _, v in SFX],
}


def main():
    force = "--force" in sys.argv
    os.makedirs(OUT, exist_ok=True)

    D, Bm, G, A = [62, 66, 69], [59, 62, 66], [55, 59, 62, 66], [57, 61, 64]
    # 대기: 느긋한 탐험 (마림바 + 멀리서 울리는 소나 핑)
    save("bgm_idle.wav", explore(92, 8, [D, Bm, G, A],
                                 [[74, 0, 78, 81, 0, 78, 76, 0], [74, 0, 71, 0, 74, 76, 78, 0],
                                  [79, 0, 78, 76, 0, 74, 71, 0], [73, 0, 76, 0, 81, 0, 0, 0],
                                  [74, 0, 78, 81, 0, 83, 81, 0], [78, 0, 74, 0, 71, 74, 0, 0],
                                  [79, 78, 76, 0, 74, 0, 78, 0], [76, 0, 73, 0, 69, 0, 0, 0]],
                                 drums="none", sonar=True), force)
    Am, F, C, E = [57, 60, 64], [53, 57, 60], [48, 52, 55], [52, 56, 59]
    # 탐색: 긴장감 있는 단조, 걸어가는 베이스 + 가벼운 드럼
    save("bgm_play.wav", explore(118, 8, [Am, F, C, E],
                                 [[69, 72, 0, 76, 0, 72, 69, 0], [77, 0, 76, 72, 0, 69, 0, 0],
                                  [72, 76, 0, 79, 0, 76, 72, 0], [71, 0, 68, 0, 71, 0, 76, 0]],
                                 drums="full", pulse=True, sonar=True, pad_gain=0.35, mel_gain=0.4), force)
    # 결과: 밝은 장조 마무리
    save("bgm_result.wav", explore(100, 4, [[60, 64, 67], [53, 57, 60, 64], [55, 59, 62], [60, 64, 67, 71]],
                                   [[72, 0, 76, 79, 0, 76, 84, 0], [77, 0, 76, 0, 72, 74, 0, 0],
                                    [74, 0, 79, 0, 83, 0, 79, 0], [84, 0, 0, 79, 0, 76, 0, 0]],
                                   drums="light", sonar=False), force)

    for key, fn, peak, _ in SFX:
        save(f"sfx_{key}.wav", fn(), force, peak)

    # sound.json: 없으면 새로 쓰고, 있으면 없는 키만 덧붙인다 (현장에서 바꾼 파일 이름·볼륨은 지킨다)
    table_path = os.path.join(OUT, "sound.json")
    table = TABLE
    if os.path.exists(table_path) and not force:
        with open(table_path, encoding="utf-8") as f:
            table = json.load(f)
        added = 0
        for kind in ("bgm", "sfx"):
            keys = {e["key"] for e in table.get(kind, [])}
            for entry in TABLE[kind]:
                if entry["key"] not in keys:
                    table.setdefault(kind, []).append(entry)
                    added += 1
        if added == 0:
            return
    with open(table_path, "w", encoding="utf-8") as f:
        json.dump(table, f, ensure_ascii=False, indent=4)
    print("write sound.json")


if __name__ == "__main__":
    main()
