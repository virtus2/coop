import os
import re

def fix_types(filepath):
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
    
    # We want to replace Text -> TMP_Text where it's a type.
    content = re.sub(r'public\s+Text\b', 'public TMP_Text', content)
    content = re.sub(r'private\s+Text\b', 'private TMP_Text', content)
    content = re.sub(r'protected\s+Text\b', 'protected TMP_Text', content)
    content = re.sub(r'\bText\s+([a-zA-Z0-9_]+\s*;)', r'TMP_Text \1', content)
    content = re.sub(r'\bText\s+([a-zA-Z0-9_]+\s*=)', r'TMP_Text \1', content)
    content = re.sub(r'\bText\[\]\s+', 'TMP_Text[] ', content)
    
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
            if fix_types(filepath):
                modified_files.append(filepath)

print('Fixed Types:')
for m in modified_files:
    print(m)
