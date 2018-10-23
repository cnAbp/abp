FROM microsoft/dotnet:2.1-aspnetcore-runtime AS base

# install System.Drawing native dependencies
RUN mv /etc/apt/sources.list /etc/apt/sources.list.bak && echo "deb http://mirrors.aliyun.com/debian/ stretch main non-free contrib\
deb-src http://mirrors.aliyun.com/debian/ stretch main non-free contrib\
deb http://mirrors.aliyun.com/debian-security stretch/updates main\
deb-src http://mirrors.aliyun.com/debian-security stretch/updates main\
deb http://mirrors.aliyun.com/debian/ stretch-updates main non-free contrib\
deb-src http://mirrors.aliyun.com/debian/ stretch-updates main non-free contrib\
deb http://mirrors.aliyun.com/debian/ stretch-backports main non-free contrib\
deb-src http://mirrors.aliyun.com/debian/ stretch-backports main non-free contrib" > /etc/apt/sources.list

RUN apt-get update \
    && apt-get install -y --allow-unauthenticated \
        libc6-dev \
        libgdiplus \
        libx11-dev \
     && rm -rf /var/lib/apt/lists/*

FROM microsoft/dotnet:2.1-sdk AS build
WORKDIR /build

COPY ["framework", "framework"]
COPY ["modules", "modules"]
COPY ["abp_io", "abp_io"]
COPY ["common.props", "common.props"]

RUN dotnet build "abp_io/src/Volo.AbpWebSite.Web/Volo.AbpWebSite.Web.csproj" -c Release
RUN dotnet publish "abp_io/src/Volo.AbpWebSite.Web/Volo.AbpWebSite.Web.csproj" -c Release -o /build/publish

FROM base AS final

EXPOSE 80

WORKDIR /app
COPY --from=build /build/publish .
RUN mkdir TemplateFiles
RUN mkdir -p wwwroot/files
ENTRYPOINT ["dotnet", "Volo.AbpWebSite.Web.dll"]