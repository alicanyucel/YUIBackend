FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["src/YUIBackend.Domain/YUIBackend.Domain.csproj", "src/YUIBackend.Domain/"]
COPY ["src/YUIBackend.Application/YUIBackend.Application.csproj", "src/YUIBackend.Application/"]
COPY ["src/YUIBackend.Infrastructure/YUIBackend.Infrastructure.csproj", "src/YUIBackend.Infrastructure/"]
COPY ["src/YUIBackend.WebAPI/YUIBackend.WebAPI.csproj", "src/YUIBackend.WebAPI/"]
RUN dotnet restore "src/YUIBackend.WebAPI/YUIBackend.WebAPI.csproj"

COPY . .
RUN dotnet publish "src/YUIBackend.WebAPI/YUIBackend.WebAPI.csproj" \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "YUIBackend.WebAPI.dll"]
