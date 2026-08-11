FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ShaunERP.sln ./
COPY src/ShaunERP.Core/ShaunERP.Core.csproj src/ShaunERP.Core/
COPY src/ShaunERP.Infrastructure/ShaunERP.Infrastructure.csproj src/ShaunERP.Infrastructure/
COPY src/ShaunERP.Api/ShaunERP.Api.csproj src/ShaunERP.Api/
RUN dotnet restore src/ShaunERP.Api/ShaunERP.Api.csproj
COPY src/ src/
RUN dotnet publish src/ShaunERP.Api/ShaunERP.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ShaunERP.Api.dll"]
