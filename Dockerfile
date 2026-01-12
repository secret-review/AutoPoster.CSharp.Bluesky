FROM mcr.microsoft.com/dotnet/runtime:8.0

ENV TZ=Asia/Tokyo
RUN apt-get update && apt-get install -y tzdata

WORKDIR /app

# 必要なファイルをすべてコピー
COPY AutoPoster .
COPY appsettings.json .

ENTRYPOINT ["./AutoPoster"]