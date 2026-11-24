FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/HotelOps.Api/HotelOps.Api.csproj src/HotelOps.Api/
RUN dotnet restore src/HotelOps.Api/HotelOps.Api.csproj
COPY src/HotelOps.Api/ src/HotelOps.Api/
RUN dotnet publish src/HotelOps.Api/HotelOps.Api.csproj -c Release --no-restore -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
COPY deployment/api-entrypoint.sh /app/api-entrypoint.sh
RUN mkdir -p /app/evidence && chown -R app:app /app/evidence && chmod 755 /app/api-entrypoint.sh
USER app
ENV ASPNETCORE_HTTP_PORTS=8080 ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
ENTRYPOINT ["/app/api-entrypoint.sh"]
