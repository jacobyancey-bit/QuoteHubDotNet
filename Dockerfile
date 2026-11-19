# Multi-stage .NET 10 Dockerfile for QuoteHub.Api
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY QuoteHub.Api/QuoteHub.Api.csproj QuoteHub.Api/
RUN dotnet restore QuoteHub.Api/QuoteHub.Api.csproj

COPY QuoteHub.Api/ QuoteHub.Api/
RUN dotnet publish QuoteHub.Api/QuoteHub.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

ENTRYPOINT ["dotnet", "QuoteHub.Api.dll"]
