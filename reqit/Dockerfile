FROM microsoft/dotnet:2.2-aspnetcore-runtime AS base
WORKDIR /app

FROM microsoft/dotnet:2.2-sdk AS build
WORKDIR /src
COPY ["reqit.csproj", "reqit/"]
RUN dotnet restore "reqit/reqit.csproj"
COPY . reqit/
WORKDIR "/src/reqit"
RUN dotnet build "reqit.csproj" -c Release -o /app

FROM build AS publish
RUN dotnet publish "reqit.csproj" -c Release -o /app

FROM base AS final
WORKDIR /app
COPY --from=publish /app .
ENTRYPOINT ["dotnet", "reqit.dll"]