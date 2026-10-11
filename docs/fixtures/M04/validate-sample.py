import json,hashlib,re
from pathlib import Path
root=Path(r"C:\dev\GeetaStudio"); out=root/"docs/fixtures/M04"
counts=[47,72,43,42,29,47,30,28,34,42,55,20,34,27,20,24,28,78]
assert sum(counts)==700
inventory=[]
for chapter,count in enumerate(counts,1):
    for baseline in range(1,count+1):
        inventory.append(dict(chapter=chapter,verse=baseline+1 if chapter==13 else baseline,variantKey="",baselineVerse=baseline,
            note="Besant chapter 13 numbering is offset by its opening Arjuna question." if chapter==13 else ""))
inventory.append(dict(chapter=13,verse=1,variantKey="opening-arjuna-question",baselineVerse=None,
    note="Besant 4th edition includes the opening Arjuna question, producing 35 chapter-13 records. Baseline 13.1 maps to this edition's 13.2; cross-edition readings are not harmonized."))
manifest=dict(key="besant-1922-wikisource-v1",version=1,edition="Annie Besant, Bhagavad-Gita, fourth edition, G. A. Natesan & Co., 1922; Wikisource transcription",baselineTarget=700,inventory=inventory)
(out/"manifest.json").write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding="utf-8")
data=json.loads((out/"sample.json").read_text(encoding="utf-8"))
for r in data:
    digits=str.maketrans("०१२३४५६७८९","0123456789")
    labels=re.findall(r"॥\s*([०-९]+)\s*॥",r["sanskrit"])
    assert labels and int(labels[-1].translate(digits))==r["verse"],r["verse"]
    assert r["translation"].rstrip().endswith(f'({r["verse"]})')
    for field in ["sanskrit","translation"]:
        assert hashlib.sha256(r[field].encode()).hexdigest().upper()==r[field+"Hash"]
print("Validated 25 Sanskrit/English verse labels and exact UTF-8 hashes; inventory: 700 baseline + 1 explicit variant.")
