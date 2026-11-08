FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY src/PcAssembly.Domain src/PcAssembly.Domain
COPY src/PcAssembly.Api src/PcAssembly.Api
RUN dotnet publish src/PcAssembly.Api -c Release -o /app
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
USER app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "PcAssembly.Api.dll"]
