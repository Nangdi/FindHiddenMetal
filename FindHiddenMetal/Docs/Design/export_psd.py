# 디자이너 PSD(02-숨은 금속 찾기-*.psd)에서 Unity용 PNG를 뽑는다.
#  - Assets/4.Sprite/Static/Static_<패널>.png : 화면별 정적 레이어 (1200x1920, 동적 요소 레이어는 숨기고 합성)
#  - Assets/4.Sprite/Parts/<부품>.png          : 버튼, 마크 등 동적 요소 (레이어만 합성 후 투명 영역 잘라냄)
#  - Docs/Design/psd_layout.json               : 부품별 캔버스 중심 기준 좌표/크기 (프리팹 배치 참고용)
# 같은 파일명으로 덮어쓰므로 Unity 연결(GUID)은 유지된다.
#
# 사용: pip install psd-tools pillow
#       python Docs/Design/export_psd.py "<PSD 경로>"
import json
import os
import sys
import unicodedata

from PIL import Image, ImageDraw, ImageFont
from psd_tools import PSDImage

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
STATIC_DIR = os.path.join(ROOT, "Assets", "4.Sprite", "Static")
PARTS_DIR = os.path.join(ROOT, "Assets", "4.Sprite", "Parts")
LAYOUT_PATH = os.path.join(os.path.dirname(__file__), "psd_layout.json")

# 화면 그룹 이름 → 패널 이름, 정적 레이어에서 뺄(동적) 레이어 이름
STATIC = {
    "Idle": ("01 대기화면", ["버튼"]),
    "Guide": ("02 체험 방법", ["체험시작 버튼"]),
    "Countdown": ("03 카운트다운", ["카운트다운 숫자 - 3"]),
    "Play": ("04 탐색/위치 입력 화면", ["시간 숫자 - 43", "43", "정답 확인 버튼 비활성",
                                    "정답 확인 버튼 (활성)", "홈 버튼", "선택된 수 표기 숫자 - 1", "선택아이콘"]),
    # 결과 화면은 박스/범례/격자 틀만 남기고 나머지(숫자, 랭킹 글자, 나의 랭킹 줄, 마크, 버튼)는 동적
    "Result": ("05 결과 화면", None),
}
RESULT_KEEP = {"배경", "결과 화면", "타이틀", "범례", "결과 남색 박스", "랭킹 그룹 박스"}

# 부품 이름 → (화면 그룹, 레이어 이름)
PARTS = {
    "Button_Start": ("01 대기화면", "버튼"),
    "Button_Confirm": ("02 체험 방법", "체험시작 버튼"),
    "Button_Submit": ("04 탐색/위치 입력 화면", "정답 확인 버튼 (활성)"),
    "Button_Submit_Off": ("04 탐색/위치 입력 화면", "정답 확인 버튼 비활성"),
    "Button_Home": ("04 탐색/위치 입력 화면", "홈 버튼"),
    "Cell_Select": ("04 탐색/위치 입력 화면", "선택아이콘"),
    "Mark_Match": ("05 결과 화면", "결과 화면 표기 - 정답 일치"),
    "Mark_Correct": ("05 결과 화면", "결과 화면 표기 - 정답"),
    "Mark_Wrong": ("05 결과 화면", "결과 화면 표기 - 오답"),
    "RankRow_Mine": ("05 결과 화면", "나의 랭킹", "pixel"),
    "Badge_MyRank": ("05 결과 화면", "나의 랭킹", "smartobject"),
}

# 크기를 맞출 부품 (투명 여백으로 채움)
PAD_TO = {"Button_Submit_Off": (380, 170)}

READY_TEXT = ("다음 체험을 준비하고 있어요", "숨은 금속의 위치가 바뀌는 중입니다.\n잠시만 기다려 주세요.")
NAVY = (52, 55, 90)
FONT_DIRS = [r"C:\Windows\Fonts", os.path.expandvars(r"%LOCALAPPDATA%\Microsoft\Windows\Fonts")]


# Mac에서 만든 PSD는 레이어 이름이 자모 분리(NFD)로 저장돼 있을 수 있어 NFC로 맞춰 비교한다.
def nm(layer):
    return unicodedata.normalize("NFC", layer.name)


def groups(psd):
    return {nm(g): g for g in psd if g.is_group()}


def leaves(group):
    return [l for l in group.descendants() if not l.is_group()]


def find(group, name, kind=None):
    for l in leaves(group):
        if nm(l) == name and (kind is None or l.kind == kind):
            return l
    raise KeyError(f"{group.name}/{name}")


def solo(psd, group, visible_layers):
    """group 하나만 켜고, 그 안에서 visible_layers만 보이게 합성한다."""
    for g in psd.descendants():
        if g.is_group():
            g.visible = True
    for g in psd:
        g.visible = g is group
    for l in leaves(group):
        l.visible = l in visible_layers
    return psd.composite(force=True).convert("RGBA")


def export_static(psd, gs):
    for panel, (group_name, dynamic) in STATIC.items():
        group = gs[group_name]
        if dynamic is None:
            keep = [l for l in leaves(group) if nm(l) in RESULT_KEEP]
        else:
            keep = [l for l in leaves(group) if nm(l) not in dynamic]
        img = solo(psd, group, keep).convert("RGB")
        save(img, os.path.join(STATIC_DIR, f"Static_{panel}.png"))

    # 회전 대기 화면은 시안이 없어 카운트다운 화면의 배경/아이콘/제목에 문구를 더해 만든다.
    group = gs["03 카운트다운"]
    keep = [find(group, "배경"), find(group, "아이콘"), find(group, "숨은 금속 찾기")]
    img = solo(psd, group, keep)
    draw = ImageDraw.Draw(img)
    draw_center(draw, READY_TEXT[0], 640, font("Paperlogy-7Bold.ttf", 72), NAVY)
    draw_center(draw, READY_TEXT[1], 760, font("Paperlogy-4Regular.ttf", 52), NAVY, spacing=24)
    save(img.convert("RGB"), os.path.join(STATIC_DIR, "Static_Ready.png"))


def export_parts(psd, gs):
    layout = {}
    w, h = psd.width, psd.height
    for name, spec in PARTS.items():
        group = gs[spec[0]]
        layer = find(group, spec[1], spec[2] if len(spec) > 2 else None)
        layer_opacity = layer.opacity
        img = solo(psd, group, [layer])
        box = img.getbbox()
        part = img.crop(box)
        if name in PAD_TO:
            # 버튼 상태 스프라이트끼리 크기를 맞춘다 (Sprite Swap 때 늘어나지 않게)
            pw, ph = PAD_TO[name]
            padded = Image.new("RGBA", (pw, ph), (0, 0, 0, 0))
            padded.paste(part, ((pw - part.width) // 2, (ph - part.height) // 2))
            part = padded
            box = (box[0] - (pw - (box[2] - box[0])) // 2, box[1] - (ph - (box[3] - box[1])) // 2, 0, 0)
            box = (box[0], box[1], box[0] + pw, box[1] + ph)
        save(part, os.path.join(PARTS_DIR, f"{name}.png"))
        cx = (box[0] + box[2]) / 2 - w / 2
        cy = h / 2 - (box[1] + box[3]) / 2
        layout[name] = {"pos": [cx, cy], "size": [box[2] - box[0], box[3] - box[1]], "opacity": layer_opacity}
    with open(LAYOUT_PATH, "w", encoding="utf-8") as f:
        json.dump(layout, f, ensure_ascii=False, indent=2)
    print("layout →", LAYOUT_PATH)


def font(file, size):
    for d in FONT_DIRS:
        path = os.path.join(d, file)
        if os.path.exists(path):
            return ImageFont.truetype(path, size)
    print(f"글꼴 없음: {file} - 기본 글꼴 사용")
    return ImageFont.load_default()


def draw_center(draw, text, y, fnt, color, spacing=0):
    box = draw.multiline_textbbox((0, 0), text, font=fnt, spacing=spacing, align="center")
    x = (1200 - (box[2] - box[0])) / 2
    draw.multiline_text((x, y), text, font=fnt, fill=color, spacing=spacing, align="center")


def save(img, path):
    img.save(path)
    print(f"{os.path.relpath(path, ROOT)}  {img.size}")


def main():
    psd = PSDImage.open(sys.argv[1])
    gs = groups(psd)
    export_static(psd, gs)
    export_parts(psd, gs)


if __name__ == "__main__":
    main()
