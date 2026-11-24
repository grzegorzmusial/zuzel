FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY src/Zuzel.Network/ src/Zuzel.Network/
COPY src/Zuzel.Server/ src/Zuzel.Server/
COPY src/Zuzel/Physics.cs src/Zuzel/Tournament.cs src/Zuzel/
RUN dotnet publish src/Zuzel.Server/Zuzel.Server.csproj -c Release -o /app --no-self-contained
FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
COPY --from=build /app .
USER $APP_UID
ENV PORT=9050 MAX_ROOMS=16
EXPOSE 9050/udp
ENTRYPOINT ["dotnet","Zuzel.Server.dll"]
