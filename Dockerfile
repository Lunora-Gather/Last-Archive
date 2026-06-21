# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# 拷贝项目文件并恢复依赖
COPY ["LastArchive.csproj", "./"]
RUN dotnet restore "LastArchive.csproj"

# 拷贝其余源文件并编译发布为 Linux net8.0 目标
COPY . .
RUN dotnet publish "LastArchive.csproj" -c Release -f net8.0 -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# 暴露出默认端口
EXPOSE 8080

# 运行 Web 服务
ENTRYPOINT ["dotnet", "LastArchive.dll", "--web"]
