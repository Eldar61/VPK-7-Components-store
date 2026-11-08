FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY src/PcShop.Domain src/PcShop.Domain
COPY src/PcShop.Api src/PcShop.Api
COPY data/catalog.json data/catalog.json
RUN dotnet publish src/PcShop.Api -c Release -o /app
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
USER app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "PcShop.Api.dll"]

