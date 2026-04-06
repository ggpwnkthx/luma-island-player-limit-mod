FROM mcr.microsoft.com/devcontainers/dotnet:2-8.0

USER root

RUN apt-get update && apt-get install -y --no-install-recommends \
    ca-certificates \
    wget \
    lib32gcc-s1 \
    libc6-i386 \
    lib32stdc++6 \
    && update-ca-certificates \
    && rm -rf /var/lib/apt/lists/* \
    && useradd -m -s /bin/bash steam \
    && usermod -a -G steam vscode \
    && mkdir -p /home/steam/steamcmd \
    && chown -R steam:steam /home/steam \
    && chmod -R g+rwX /home/steam \
    && find /home/steam -type d -exec chmod g+s {} \;

USER steam

RUN cd /home/steam/steamcmd \
    && wget -S -O steamcmd.tar.gz https://steamcdn-a.akamaihd.net/client/installer/steamcmd_linux.tar.gz \
    && tar -xzf steamcmd.tar.gz \
    && rm steamcmd.tar.gz

USER root

RUN chgrp -R steam /home/steam \
    && chmod -R g+rwX /home/steam \
    && chmod g+rx /home/steam/steamcmd/steamcmd.sh

USER vscode

ENV OPENCODE_CONFIG=.opencode/settings.json
RUN curl -fsSL https://opencode.ai/install | bash
