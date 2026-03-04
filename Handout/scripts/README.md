# Scripts

This folder contains helper tooling that sits above feature-specific folders. The Markdown to PDF converter for the handout is defined by [Dockerfile](./Dockerfile), [render_pdf.py](./render_pdf.py), [puppeteer-config.json](./puppeteer-config.json), and [mermaid-config.json](./mermaid-config.json).

Build the converter image from the repository root:

```bash
docker build -t chess-handout-pdf ./scripts
```

Run it against the handout folder:

```bash
docker run --rm \
  -v "$(pwd)/handout:/workspace/handout" \
  chess-handout-pdf
```
