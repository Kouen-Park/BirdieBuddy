FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["BirdieBuddy.csproj", "./"]
RUN dotnet restore "BirdieBuddy.csproj"

COPY . .
RUN dotnet publish "BirdieBuddy.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:10000

EXPOSE 10000

ENTRYPOINT ["dotnet", "BirdieBuddy.dll"]
