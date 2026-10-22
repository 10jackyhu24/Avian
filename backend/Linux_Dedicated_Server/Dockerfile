FROM ubuntu:22.04

WORKDIR /app

RUN apt-get update && \
    apt-get install -y --no-install-recommends \
        ca-certificates \
        libglib2.0-0 \
        libnss3 \
        libx11-6 \
        libxcursor1 \
        libxrandr2 \
        libxi6 \
        libxrender1 \
        libasound2 \
        libpulse0 && \
    rm -rf /var/lib/apt/lists/*

COPY . /app

RUN chmod +x /app/AvianServer.x86_64

EXPOSE 7777/tcp 7777/udp

ENTRYPOINT ["/app/AvianServer.x86_64"]
