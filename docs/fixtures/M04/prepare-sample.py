from html.parser import HTMLParser
from pathlib import Path
import re,json,hashlib
class Node:
    def __init__(self,tag="",attrs=None,parent=None):
        self.tag=tag; self.attrs=dict(attrs or []); self.parent=parent; self.children=[]
    def text(self):
        if self.tag in ("style","script"): return ""
        return "".join(c if isinstance(c,str) else c.text() for c in self.children)
    def all(self):
        yield self
        for c in self.children:
            if isinstance(c,Node): yield from c.all()
class Parser(HTMLParser):
    def __init__(self): super().__init__(convert_charrefs=True); self.root=Node(); self.current=self.root
    def handle_starttag(self,t,a):
        node=Node(t,a,self.current);self.current.children.append(node)
        if t not in ("br","hr","img","link","meta","input","source","wbr"):self.current=node
    def handle_endtag(self,t):
        n=self.current
        while n.parent and n.tag!=t:n=n.parent
        if n.parent:self.current=n.parent
    def handle_data(self,d): self.current.children.append(d)
root=Path(r"C:\dev\GeetaStudio"); raw=(root/".local/M04-discourse1.html").read_bytes()
parser=Parser();parser.feed(raw.decode("utf-8"))
nodes=list(parser.root.all())
body=next(n for n in nodes if "mw-parser-output" in n.attrs.get("class","").split())
parts=list(body.all()); rows=[]
for i,p in enumerate(parts):
    if p.tag!="p":continue
    labels=[n for n in p.all() if "wst-floatright" in n.attrs.get("class","").split()]
    if not labels:continue
    match=re.fullmatch(r"\((\d+)\)",labels[0].text().strip())
    if not match or int(match[1])>25:continue
    sa=next(n for n in reversed(parts[:i]) if n.tag=="div" and n.attrs.get("lang")=="sa")
    index=parts.index(sa)
    def in_sa(n):
        while n:
            if n.attrs.get("lang")=="sa":return True
            n=n.parent
        return False
    exact_sa=sa.text()
    exact_en="\n".join(n.text() for n in parts[index:i+1] if n.tag=="p" and not in_sa(n))
    verse=int(match[1]); assert re.search(r"[\u0900-\u097f]",exact_sa)
    rows.append(dict(chapter=1,verse=verse,variantKey="",sanskrit=exact_sa,translation=exact_en,
        sanskritHash=hashlib.sha256(exact_sa.encode()).hexdigest().upper(),translationHash=hashlib.sha256(exact_en.encode()).hexdigest().upper(),
        sourceLocator=f"Discourse 1, verse {verse}; Sanskrit DOM {sa.attrs.get('id')}; English DOM {p.attrs.get('id')}",
        transliteration=None,wordMeanings=[]))
assert len(rows)==25 and [r["verse"] for r in rows]==list(range(1,26))
a=next(n for n in nodes if n.tag=="li" and n.attrs.get("id")=="t-permalink")
permanent="https://en.wikisource.org"+next(n for n in a.all() if n.tag=="a").attrs["href"]
out=root/"docs/fixtures/M04"
for field,file in (("sanskrit","sanskrit-sample.txt"),("translation","translation-sample.txt")):
    (out/file).write_bytes("\n\n".join(r[field] for r in rows).encode())
(out/"sample.json").write_text(json.dumps(rows,ensure_ascii=False,indent=2),encoding="utf-8")
# Byte-for-byte fetched page retained for reproducible source verification.
(out/"discourse1-source.html").write_bytes(raw)
(out/"edition-source.html").write_bytes((root/".local/M04-edition.html").read_bytes())
evidence=dict(sourceUrl="https://en.wikisource.org/wiki/Bhagavad-Gita_(Besant_4th)/Discourse_1",permanentUrl=permanent,
    rawPageSha256=hashlib.sha256(raw).hexdigest().upper(),licenseUrl="https://creativecommons.org/licenses/by-sa/4.0/",
    extraction="M04 visible-paragraph text v1: HTML text nodes decoded, styles excluded, source whitespace/Unicode and note markers retained. No source-language reconstruction.",
    sampleCount=25,review="Source/transcription and locator checks only; no new scholarly or human manuscript review claimed.")
(out/"source-evidence.json").write_text(json.dumps(evidence,indent=2),encoding="utf-8")
print(json.dumps(dict(count=25,permanentUrl=permanent)))
