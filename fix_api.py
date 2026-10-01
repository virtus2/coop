import os
import re

mapping = {
    'TextAnchor.UpperLeft': 'TextAlignmentOptions.TopLeft',
    'TextAnchor.UpperCenter': 'TextAlignmentOptions.Top',
    'TextAnchor.UpperRight': 'TextAlignmentOptions.TopRight',
    'TextAnchor.MiddleLeft': 'TextAlignmentOptions.Left',
    'TextAnchor.MiddleCenter': 'TextAlignmentOptions.Center',
    'TextAnchor.MiddleRight': 'TextAlignmentOptions.Right',
    'TextAnchor.LowerLeft': 'TextAlignmentOptions.BottomLeft',
    'TextAnchor.LowerCenter': 'TextAlignmentOptions.Bottom',
    'TextAnchor.LowerRight': 'TextAlignmentOptions.BottomRight',
}

def fix_tmp_issues(filepath):
    try:
        with open(filepath, 'r', encoding='utf-8-sig') as f:
            content = f.read()
    except UnicodeDecodeError:
        try:
            with open(filepath, 'r', encoding='euc-kr') as f:
                content = f.read()
        except UnicodeDecodeError:
            with open(filepath, 'r', encoding='utf-8', errors='ignore') as f:
                content = f.read()

    original = content
    
    # Let's find all AddComponent<Text> again, since previous script missed some if they didn't have \bText\b alone
    content = re.sub(r'AddComponent<Text>\(\)', 'AddComponent<TextMeshProUGUI>()', content)
    content = re.sub(r'GetComponent<Text>\(\)', 'GetComponent<TextMeshProUGUI>()', content)
    
    # We want to replace alignment ONLY if it's not childAlignment
    # childAlignment is for layout groups.
    # So if we see .alignment = TextAnchor.XXX, we replace it.
    for k, v in mapping.items():
        # Match .alignment = TextAnchor.XXX
        content = re.sub(r'\.alignment\s*=\s*' + k, '.alignment = ' + v, content)
        
    # Remove Font assignments for TMP since they need TMP_FontAsset
    # e.g., text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
    content = re.sub(r'([a-zA-Z0-9_]+)\.font\s*=\s*Resources\.GetBuiltinResource<Font>\([^)]+\);', r'// \1.font = null; /* TMP font */', content)
    # Also if font = font; where font is Font
    # Just comment out .font assignments for now if they are text.font
    content = re.sub(r'([a-zA-Z0-9_]+Text)\.font\s*=\s*[^;]+;', r'// \g<0> /* TMP Font */', content)
    content = re.sub(r'([a-zA-Z0-9_]+Txt)\.font\s*=\s*[^;]+;', r'// \g<0> /* TMP Font */', content)
    content = re.sub(r'([a-zA-Z0-9_]+Comp)\.font\s*=\s*[^;]+;', r'// \g<0> /* TMP Font */', content)
    content = re.sub(r'_nameText\.font\s*=\s*[^;]+;', r'// \g<0> /* TMP Font */', content)
    content = re.sub(r'_hintText\.font\s*=\s*[^;]+;', r'// \g<0> /* TMP Font */', content)
    content = re.sub(r'_targetNameText\.font\s*=\s*[^;]+;', r'// \g<0> /* TMP Font */', content)
    
    # Fix overflow
    content = re.sub(r'([a-zA-Z0-9_]+)\.horizontalOverflow\s*=\s*HorizontalWrapMode\.Overflow;', r'\1.enableWordWrapping = false;', content)
    content = re.sub(r'([a-zA-Z0-9_]+)\.horizontalOverflow\s*=\s*HorizontalWrapMode\.Wrap;', r'\1.enableWordWrapping = true;', content)
    content = re.sub(r'([a-zA-Z0-9_]+)\.verticalOverflow\s*=\s*VerticalWrapMode\.Overflow;', r'\1.overflowMode = TextOverflowModes.Overflow;', content)
    content = re.sub(r'([a-zA-Z0-9_]+)\.verticalOverflow\s*=\s*VerticalWrapMode\.Truncate;', r'\1.overflowMode = TextOverflowModes.Truncate;', content)
    
    # Fix resizeTextForBestFit
    content = re.sub(r'([a-zA-Z0-9_]+)\.resizeTextForBestFit\s*=\s*true;', r'\1.enableAutoSizing = true;', content)
    content = re.sub(r'([a-zA-Z0-9_]+)\.resizeTextMinSize\s*=\s*([0-9]+);', r'\1.fontSizeMin = \2;', content)
    content = re.sub(r'([a-zA-Z0-9_]+)\.resizeTextMaxSize\s*=\s*([0-9]+);', r'\1.fontSizeMax = \2;', content)
    
    # Fix supportRichText
    content = re.sub(r'([a-zA-Z0-9_]+)\.supportRichText\s*=\s*([^;]+);', r'\1.richText = \2;', content)
    
    if original != content:
        if 'using TMPro;' not in content:
            content = 'using TMPro;\n' + content
        with open(filepath, 'w', encoding='utf-8-sig') as f:
            f.write(content)
        return True
    return False

modified_files = []
for root, dirs, files in os.walk('c:/unity-projects/coop/Assets'):
    if 'External' in root or 'JMO Assets' in root:
        continue
    for file in files:
        if file.endswith('.cs'):
            filepath = os.path.join(root, file)
            if fix_tmp_issues(filepath):
                modified_files.append(filepath)

print('Fixed TMP API:')
for m in modified_files:
    print(m)
