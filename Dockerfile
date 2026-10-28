FROM debian:trixie-slim as resource-loader
ARG NO_RESOURCE=0
# Evolution-only passcodes (pre-errata and alternate ids) that the upstream
# database lacks; WindBot drops deck cards it cannot find in cards.cdb.
COPY cdb/evolution-alternates.cdb /evolution-alternates.cdb
RUN mkdir -p /resources/windbot && \
  if [ "$NO_RESOURCE" != "1" ]; then \
    apt update && apt -y install git sqlite3 && \
    git clone --depth=1 https://github.com/purerosefallen/ygopro-database /ygopro-database && \
    cp -f /ygopro-database/locales/zh-CN/cards.cdb /resources/windbot/ && \
    sqlite3 /resources/windbot/cards.cdb \
      "ATTACH '/evolution-alternates.cdb' AS evo; \
       INSERT OR IGNORE INTO datas SELECT * FROM evo.datas; \
       INSERT OR IGNORE INTO texts SELECT * FROM evo.texts;" && \
    rm -rf /var/lib/apt/lists/* /ygopro-database; \
  fi

FROM mono as builder

COPY . /windbot-source
WORKDIR /windbot-source
RUN msbuild WindBot.sln /p:Configuration=Release /p:TargetFrameworkVersion=v4.8 /p:OutDir=/windbot/

FROM mono
COPY --from=builder /windbot /windbot
COPY --from=resource-loader /resources/windbot /windbot

WORKDIR /windbot

EXPOSE 2399
ENTRYPOINT ["mono", "./WindBot.exe"]
CMD [ "ServerMode=true", "ServerPort=2399" ]
