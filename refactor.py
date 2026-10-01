import os
import re

def process_file(filepath):
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

    if not re.search(r'\bText\b', content):
        return False
        
    original = content
    
    content = re.sub(r'AddComponent<Text>', 'AddComponent<TextMeshProUGUI>', content)
    content = re.sub(r'GetComponent<Text>', 'GetComponent<TMP_Text>', content)
    content = re.sub(r'GetComponentInChildren<Text>', 'GetComponentInChildren<TMP_Text>', content)
    
    content = re.sub(r'\bText(\s+\w+;)', r'TMP_Text\1', content)
    content = re.sub(r'\bText(\s+_[a-zA-Z0-9_]+)', r'TMP_Text\1', content)
    content = re.sub(r'\bText(\s+[a-zA-Z0-9_]+\s*=)', r'TMP_Text\1', content)
    content = re.sub(r'public\s+Text\b', 'public TMP_Text', content)
    content = re.sub(r'private\s+Text\b', 'private TMP_Text', content)
    content = re.sub(r'protected\s+Text\b', 'protected TMP_Text', content)
    content = re.sub(r'Text\[\]', 'TMP_Text[]', content)
    
    if original != content:
        if 'using TMPro;' not in content:
            if 'using UnityEngine.UI;' in content:
                content = content.replace('using UnityEngine.UI;', 'using UnityEngine.UI;\nusing TMPro;')
            elif 'using UnityEngine;' in content:
                content = content.replace('using UnityEngine;', 'using UnityEngine;\nusing TMPro;')
            else:
                content = 'using TMPro;\n' + content
                
        # preserve the same encoding it was read as. Just write as utf-8-sig which is standard for unity.
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
            if process_file(filepath):
                modified_files.append(filepath)

print('Modified:')
for m in modified_files:
    print(m)
