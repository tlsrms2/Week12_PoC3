import os

def convert_to_utf8_bom(file_path):
    try:
        with open(file_path, 'rb') as f:
            content = f.read()
        
        # Try to detect double encoding or already correct encoding
        # If it's already UTF-8 with BOM, content starts with b'\xef\xbb\xbf'
        if content.startswith(b'\xef\xbb\xbf'):
            return
        
        # Try reading as UTF-8
        try:
            text = content.decode('utf-8')
        except UnicodeDecodeError:
            # If fail, try reading as CP949 (Korean Windows default)
            try:
                text = content.decode('cp949')
            except UnicodeDecodeError:
                print(f"Skipping {file_path} - unknown encoding")
                return

        # Write back with UTF-8 BOM
        with open(file_path, 'w', encoding='utf-8-sig') as f:
            f.write(text)
        print(f"Converted {file_path}")
    except Exception as e:
        print(f"Error processing {file_path}: {e}")

scripts_dir = 'Assets/Scripts'
for root, dirs, files in os.walk(scripts_dir):
    for file in files:
        if file.endswith(('.cs', '.md', '.json')):
            convert_to_utf8_bom(os.path.join(root, file))
