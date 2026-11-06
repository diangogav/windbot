ARG GIT_IMAGE=alpine/git:v2.52.0
ARG ALPINE_IMAGE=alpine:3.22
ARG NO_RESOURCE=0
ARG USE_APK_CHINA_MIRROR=0
ARG YGOPRO_DATABASE_REPO=https://code.moenext.com/nanahira/ygopro-database
ARG YGOPRO_DATABASE_BRANCH=master
ARG TARGET_FRAMEWORK_VERSION=v4.8

FROM ${ALPINE_IMAGE} AS mono-alpine
ARG USE_APK_CHINA_MIRROR
RUN if [ "${USE_APK_CHINA_MIRROR}" = "1" ]; then \
      sed -i 's/dl-cdn.alpinelinux.org/mirrors.aliyun.com/g' /etc/apk/repositories; \
    fi && \
    apk add --no-cache mono

FROM ${GIT_IMAGE} AS resource-loader
ARG NO_RESOURCE
ARG YGOPRO_DATABASE_REPO
ARG YGOPRO_DATABASE_BRANCH
# Evolution-only passcodes (pre-errata and alternate ids) that the upstream
# database lacks; WindBot drops deck cards it cannot find in cards.cdb.
COPY cdb/evolution-alternates.cdb /evolution-alternates.cdb
RUN mkdir -p /resources/windbot && \
  if [ "$NO_RESOURCE" != "1" ]; then \
    apk add --no-cache sqlite && \
    git clone --branch="${YGOPRO_DATABASE_BRANCH}" --depth=1 "${YGOPRO_DATABASE_REPO}" /ygopro-database && \
    cp -f /ygopro-database/locales/zh-CN/cards.cdb /resources/windbot/ && \
    sqlite3 /resources/windbot/cards.cdb \
      "ATTACH '/evolution-alternates.cdb' AS evo; \
       INSERT OR IGNORE INTO datas SELECT * FROM evo.datas; \
       INSERT OR IGNORE INTO texts SELECT * FROM evo.texts;" && \
    rm -rf /ygopro-database; \
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
