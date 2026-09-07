ARG DOCKER_REPO=hub.alibaba.ir/mcr
FROM $DOCKER_REPO/dotnet/sdk:10.0 AS sdk
WORKDIR /app
ENV ASPNETCORE_ENVIRONMENT=Production
ENV TZ=Asia/Tehran
COPY . ./
RUN dotnet restore AeroTech.Ordering.sln --configfile nuget-prod.config
RUN dotnet build AeroTech.Ordering.sln -c Release --no-restore
RUN dotnet publish src/AeroTech.Ordering.ServiceHost/AeroTech.Ordering.ServiceHost.csproj --no-build -c Release -o /app/out
FROM $DOCKER_REPO/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=sdk /app/out .
EXPOSE 80
ENTRYPOINT ["dotnet","AeroTech.Ordering.ServiceHost.dll"]
