# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# 拷贝项目文件并恢复依赖
COPY ["LastArchive.csproj", "./"]
RUN dotnet restore "LastArchive.csproj" -p:TargetFrameworks=net10.0

# 拷贝其余源文件并编译发布为 Linux net10.0 目标
COPY . .
RUN dotnet publish "LastArchive.csproj" -c Release -f net10.0 --no-restore -p:TargetFrameworks=net10.0 -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
RUN mkdir -p /data/last-archive && chown 1000:1000 /data/last-archive
USER 1000:1000

# 暴露出默认端口 (Hugging Face Spaces 默认使用 7860 端口)
ENV PORT=7860 LAST_ARCHIVE_SAVE_DIR=/data/last-archive
EXPOSE 7860

# 运行 Web 服务
ENTRYPOINT ["dotnet", "LastArchive.dll", "--web"]
