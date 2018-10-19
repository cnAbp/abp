FROM microsoft/dotnet:2.1-sdk

EXPOSE 80

WORKDIR /build

COPY ["framework", "framework"]
COPY ["modules", "modules"]
COPY ["abp_io", "abp_io"]
COPY ["common.props", "common.props"]

RUN dotnet build "abp_io/src/Volo.AbpWebSite.Web/Volo.AbpWebSite.Web.csproj" -c Release
RUN dotnet publish "abp_io/src/Volo.AbpWebSite.Web/Volo.AbpWebSite.Web.csproj" -c Release -o /prod

WORKDIR /prod
COPY ["entrypoint.sh", "entrypoint.sh"]
RUN chmod +x ./entrypoint.sh
CMD /bin/bash ./entrypoint.sh