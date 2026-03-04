FROM python:3.12-slim

ENV PYTHONUNBUFFERED=1 \
    PUPPETEER_SKIP_DOWNLOAD=true \
    PUPPETEER_EXECUTABLE_PATH=/usr/bin/chromium

RUN apt-get update && apt-get install -y --no-install-recommends \
    chromium \
    fonts-dejavu \
    fonts-liberation \
    libcairo2 \
    libgdk-pixbuf-2.0-0 \
    libpango-1.0-0 \
    libpangocairo-1.0-0 \
    nodejs \
    npm \
    shared-mime-info \
    && rm -rf /var/lib/apt/lists/*

RUN npm install -g @mermaid-js/mermaid-cli

RUN pip install --no-cache-dir \
    markdown \
    pygments \
    weasyprint

WORKDIR /app

COPY render_pdf.py puppeteer-config.json mermaid-config.json /app/

ENTRYPOINT ["python", "/app/render_pdf.py"]
CMD ["/workspace/handout/handout.md", "/workspace/handout/handout.pdf"]
