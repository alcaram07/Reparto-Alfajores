FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["RepartoAlfajores/RepartoAlfajores.csproj", "."]
RUN dotnet restore RepartoAlfajores.csproj
COPY RepartoAlfajores/ .
# Se nombra el .csproj explícitamente: el directorio también trae la .sln, y esa referencia
# al proyecto de tests, que no se copia a la imagen.
RUN dotnet publish RepartoAlfajores.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:10000
EXPOSE 10000
ENTRYPOINT ["dotnet", "RepartoAlfajores.dll"]
