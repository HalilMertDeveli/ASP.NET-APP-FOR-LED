# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY src/LedSupport.Web/LedSupport.Web.csproj src/LedSupport.Web/
RUN dotnet restore src/LedSupport.Web/LedSupport.Web.csproj
COPY src/LedSupport.Web/ src/LedSupport.Web/
RUN dotnet publish src/LedSupport.Web/LedSupport.Web.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "LedSupport.Web.dll"]
