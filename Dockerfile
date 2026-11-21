# Багатоетапна збірка: SDK лише для компіляції, у фінальному образі тільки ASP.NET runtime.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Build.props global.json .editorconfig ./
COPY src/Greenhouse.Domain/Greenhouse.Domain.csproj src/Greenhouse.Domain/
COPY src/Greenhouse.Api/Greenhouse.Api.csproj src/Greenhouse.Api/
COPY src/Greenhouse.Simulator/Greenhouse.Simulator.csproj src/Greenhouse.Simulator/
RUN dotnet restore src/Greenhouse.Api/Greenhouse.Api.csproj \
 && dotnet restore src/Greenhouse.Simulator/Greenhouse.Simulator.csproj
COPY src/ src/
RUN dotnet publish src/Greenhouse.Api -c Release --no-restore -o /out/api \
 && dotnet publish src/Greenhouse.Simulator -c Release --no-restore -o /out/simulator

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS api
WORKDIR /app
COPY --from=build /out/api .
# БД і логи — у томі /data, щоб переживали перезапуск контейнера.
ENV ASPNETCORE_URLS=http://+:8080 \
    ConnectionStrings__Greenhouse="Data Source=/data/greenhouse.db" \
    Logging__FilePath=/data/logs/greenhouse-.log
USER root
RUN mkdir -p /data/logs && chown -R app:app /data
USER app
VOLUME /data
EXPOSE 8080
ENTRYPOINT ["dotnet", "Greenhouse.Api.dll"]

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS simulator
WORKDIR /app
COPY --from=build /out/simulator .
USER app
ENTRYPOINT ["dotnet", "Greenhouse.Simulator.dll"]
