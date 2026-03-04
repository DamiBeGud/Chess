from __future__ import annotations

import argparse
import html
import re
import subprocess
import tempfile
from pathlib import Path

import markdown
from pygments.formatters import HtmlFormatter
from weasyprint import HTML


MERMAID_BLOCK_PATTERN = re.compile(
    r"^```mermaid[ \t]*\n(.*?)^```[ \t]*$",
    re.MULTILINE | re.DOTALL,
)


BASE_CSS = """
@page {
  size: A4;
  margin: 1.7cm 1.5cm 1.8cm 1.5cm;
}

body {
  color: #1f2937;
  font-family: "DejaVu Sans", Arial, sans-serif;
  font-size: 11pt;
  line-height: 1.55;
}

h1, h2, h3, h4 {
  color: #111827;
  line-height: 1.2;
  margin-top: 1.2em;
  margin-bottom: 0.45em;
}

h1 {
  border-bottom: 2px solid #d1d5db;
  font-size: 24pt;
  margin-top: 0;
  padding-bottom: 0.2em;
}

h2 {
  border-bottom: 1px solid #e5e7eb;
  font-size: 17pt;
  padding-bottom: 0.12em;
}

h3 {
  font-size: 14pt;
}

p {
  margin: 0 0 0.8em 0;
  text-align: justify;
}

code {
  background: #f3f4f6;
  border-radius: 3px;
  font-family: "DejaVu Sans Mono", "Courier New", monospace;
  font-size: 0.9em;
  padding: 0.08em 0.3em;
}

pre {
  background: #111827;
  border-radius: 6px;
  color: #f9fafb;
  overflow-wrap: anywhere;
  padding: 0.85em 1em;
  white-space: pre-wrap;
}

pre code {
  background: transparent;
  color: inherit;
  padding: 0;
}

blockquote {
  background: #f9fafb;
  border-left: 4px solid #9ca3af;
  margin: 1em 0;
  padding: 0.2em 1em;
}

table {
  border-collapse: collapse;
  margin: 1em 0;
  width: 100%;
}

th, td {
  border: 1px solid #d1d5db;
  padding: 0.45em 0.6em;
  text-align: left;
  vertical-align: top;
}

th {
  background: #f3f4f6;
}

img, svg {
  max-width: 100%;
}

.mermaid-diagram {
  background: #ffffff;
  border: 1px solid #e5e7eb;
  border-radius: 8px;
  margin: 1.2em 0;
  padding: 0.8em;
  text-align: center;
}

.mermaid-diagram svg {
  height: auto;
  width: 100%;
}
"""


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Convert a Markdown handout into a PDF, including Mermaid diagrams."
    )
    parser.add_argument(
        "input",
        nargs="?",
        default="/workspace/handout/handout.md",
        help="Path to the input Markdown file.",
    )
    parser.add_argument(
        "output",
        nargs="?",
        default="/workspace/handout/handout.pdf",
        help="Path to the output PDF file.",
    )
    return parser.parse_args()


def render_mermaid_block(diagram_source: str, temp_dir: Path, index: int) -> str:
    source_path = temp_dir / f"diagram-{index}.mmd"
    svg_path = temp_dir / f"diagram-{index}.svg"
    source_path.write_text(diagram_source.strip() + "\n", encoding="utf-8")

    command = [
        "mmdc",
        "-i",
        str(source_path),
        "-o",
        str(svg_path),
        "-c",
        "/app/mermaid-config.json",
        "-p",
        "/app/puppeteer-config.json",
        "-t",
        "neutral",
        "-b",
        "transparent",
    ]

    try:
        subprocess.run(
            command,
            check=True,
            capture_output=True,
            text=True,
        )
    except subprocess.CalledProcessError as exc:
        stderr = exc.stderr.strip() or exc.stdout.strip() or "Unknown Mermaid rendering error."
        raise RuntimeError(f"Failed to render Mermaid diagram {index}: {stderr}") from exc

    svg_markup = svg_path.read_text(encoding="utf-8").strip()
    return f'\n<div class="mermaid-diagram">\n{svg_markup}\n</div>\n'


def replace_mermaid_blocks(markdown_text: str, temp_dir: Path) -> str:
    diagram_index = 0

    def replacer(match: re.Match[str]) -> str:
        nonlocal diagram_index
        diagram_index += 1
        return render_mermaid_block(match.group(1), temp_dir, diagram_index)

    return MERMAID_BLOCK_PATTERN.sub(replacer, markdown_text)


def extract_title(markdown_text: str, fallback: str) -> str:
    for line in markdown_text.splitlines():
        stripped = line.strip()
        if stripped.startswith("# "):
            return stripped[2:].strip()

    return fallback


def build_html_document(title: str, body_html: str) -> str:
    formatter = HtmlFormatter(style="default")
    syntax_css = formatter.get_style_defs(".codehilite")
    return f"""<!DOCTYPE html>
<html lang="en">
  <head>
    <meta charset="utf-8" />
    <title>{html.escape(title)}</title>
    <style>
{BASE_CSS}
{syntax_css}
    </style>
  </head>
  <body>
{body_html}
  </body>
</html>
"""


def convert_markdown_to_pdf(input_path: Path, output_path: Path) -> None:
    if not input_path.is_file():
        raise FileNotFoundError(f"Input Markdown file was not found: {input_path}")

    markdown_text = input_path.read_text(encoding="utf-8")
    title = extract_title(markdown_text, input_path.stem)

    with tempfile.TemporaryDirectory() as temp_directory:
        temp_dir = Path(temp_directory)
        markdown_with_diagrams = replace_mermaid_blocks(markdown_text, temp_dir)
        body_html = markdown.markdown(
            markdown_with_diagrams,
            extensions=[
                "fenced_code",
                "tables",
                "codehilite",
                "sane_lists",
            ],
            output_format="html5",
        )
        html_document = build_html_document(title, body_html)

    output_path.parent.mkdir(parents=True, exist_ok=True)
    HTML(string=html_document, base_url=str(input_path.parent.resolve())).write_pdf(output_path)


def main() -> int:
    args = parse_args()
    input_path = Path(args.input).resolve()
    output_path = Path(args.output).resolve()

    try:
        convert_markdown_to_pdf(input_path, output_path)
    except Exception as exc:
        print(f"PDF export failed: {exc}")
        return 1

    print(f"PDF written to {output_path}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
