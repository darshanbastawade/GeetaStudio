# M04 selected sample and reuse evidence

The 25 paired records are exact visible text nodes from the selected Wikisource
transcription of Annie Besant's 1922 fourth edition, Discourse 1, verses 1–25.
See source-evidence.json for the pinned revision URL and fetched HTML checksum.
discourse1-source.html and edition-source.html retain the fetched source/evidence;
CC-BY-SA-4.0.html retains the official license text fetched on 10 October 2026.

Source material/transcription reproduced in this folder is distributed under
Creative Commons Attribution-ShareAlike 4.0:
https://creativecommons.org/licenses/by-sa/4.0/
Credit: Annie Besant (English translator); G. A. Natesan & Co. (1922 edition);
Wikisource contributors (transcription). Retain attribution, source/revision links,
license notice, and ShareAlike obligations for adaptations. This notice applies
to source material, not to unrelated application code or new ownership claims.

The edition page separately states Sanskrit's public-domain status and translation
term/territory considerations. This sample conservatively retains Wikisource's
CC BY-SA notice rather than treating the original's status as automatic translation
permission. Application grants are limited to staff text READ in IN; commercial,
adaptation, translation and AI processing are disabled for this selected profile.

Extraction removes HTML markup/styles only. It preserves decoded text nodes,
source whitespace/Unicode, speaker labels, verse numbering and footnote markers.
Source hashes are UTF-8 SHA-256 of these exact strings; full HTML bytes have a
separate checksum. Search NFC/whitespace normalization never changes exact text.
No OCR, LLM reconstruction, spelling correction or modernized translation was used.
Verse 11's doubled danda and historical spellings are intentionally retained.
Footnote markers remain; this reader does not claim full footnote coverage.

validate-sample.py actually checks all 25 Sanskrit and English verse labels and
hashes. This is a transcription/source-locator review, not a new scholarly
manuscript, theological or human interpretation review. The demo records that
specific review evidence; every runtime source and corpus row starts Pending
until the explicit setup helper submits its review commands. No other license or
corpus is approved by those commands.

Manifest target is 700 baseline verses plus one explicit edition variant:
Besant's chapter 13 opening Arjuna question. Edition source number 13.2 maps to
baseline 13.1 through 13.35/baseline 13.34. These are explicit numbering references,
not fabricated missing readings. Only 1.1–1.25 are imported. Remaining 676 edition
locators are missing (675 baseline + 1 variant). Transliteration, word meanings,
approved concepts and explanations are absent and reported as such.

Expand with additional independently reviewed exact source snapshots and batches
through the same importer; do not fill gaps. A new text/translation/correction
requires a new manifest/source edition version, preserving cited evidence.

Preparation scripts are maintenance-only standard-library Python utilities, not
runtime adapters, crawler endpoints or configured code execution. Runtime and CI
need only the committed local files and .NET/SQL. Review/prepare only the explicitly
selected source; the product never fetches the URLs.

Review correction: manifest.json is the inventory template. The setup helper registers version 2 with immutable snapshot/hash/chapter/verse/component bindings assembled from this independently checked sample. Unbound version-1 records are preserved and withheld from text reads; the helper does not bless bindings copied from stored imports. Source coordinates use selected-edition numbering; BaselineVerse carries the explicit chapter-13 alternate numbering.
