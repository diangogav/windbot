ARG GIT_IMAGE=alpine/git:v2.52.0
ARG ALPINE_IMAGE=alpine:3.22
ARG NO_RESOURCE=0
ARG USE_APK_CHINA_MIRROR=0
ARG CARD_DATABASE_URL=https://raw.githubusercontent.com/diangogav/evolution-assets/main/cdb
ARG TARGET_FRAMEWORK_VERSION=v4.8

FROM ${ALPINE_IMAGE} AS mono-alpine
ARG USE_APK_CHINA_MIRROR
RUN if [ "${USE_APK_CHINA_MIRROR}" = "1" ]; then \
      sed -i 's/dl-cdn.alpinelinux.org/mirrors.aliyun.com/g' /etc/apk/repositories; \
    fi && \
    apk add --no-cache mono

FROM ${GIT_IMAGE} AS resource-loader
ARG NO_RESOURCE
ARG CARD_DATABASE_URL
# The card pool comes from evolution-assets, the same source the duel server
# reads: base, prerelease and pre-errata cards. evolution-alternates.cdb adds
# the Evolution-only passcodes (alternate ids) those databases lack. WindBot
# drops deck cards it cannot find in cards.cdb.
COPY cdb/evolution-alternates.cdb /evolution-alternates.cdb
RUN mkdir -p /resources/windbot && \
  if [ "$NO_RESOURCE" != "1" ]; then \
    apk add --no-cache sqlite && \
    for name in base.en prerelease.en pre-errata.en; do \
      wget -q -O "/$name.cdb.gz" "${CARD_DATABASE_URL}/$name.cdb.gz" && \
      gunzip "/$name.cdb.gz" || exit 1; \
    done && \
    cp -f /base.en.cdb /resources/windbot/cards.cdb && \
    for source in /prerelease.en.cdb /pre-errata.en.cdb /evolution-alternates.cdb; do \
      sqlite3 /resources/windbot/cards.cdb \
        "ATTACH '$source' AS src; \
         INSERT OR IGNORE INTO datas SELECT * FROM src.datas; \
         INSERT OR IGNORE INTO texts SELECT * FROM src.texts;" || exit 1; \
    done && \
    rm -f /base.en.cdb /prerelease.en.cdb /pre-errata.en.cdb; \
  fi

FROM mono-alpine AS builder
ARG TARGET_FRAMEWORK_VERSION

COPY . /windbot-source
WORKDIR /windbot-source
RUN xbuild WindBot.sln /p:Configuration=Release /p:TargetFrameworkVersion="${TARGET_FRAMEWORK_VERSION}" /p:OutDir=/windbot/ && \
  for arch in x86 x64; do \
    source_path="/windbot/ThirdParty/sqlite/$arch/sqlite3.dll"; \
    target_dir="/windbot/$arch"; \
    if [ -f "$source_path" ]; then \
      mkdir -p "$target_dir"; \
      mv -f "$source_path" "$target_dir/sqlite3.dll"; \
    fi; \
  done

FROM mono-alpine
COPY --from=builder /windbot /windbot
COPY --from=resource-loader /resources/windbot /windbot

WORKDIR /windbot

EXPOSE 2399
ENTRYPOINT ["mono", "./WindBot.exe"]
CMD [ "ServerMode=true", "ServerPort=2399" ]
