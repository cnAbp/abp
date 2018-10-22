FROM microsoft/dotnet:2.1-sdk

EXPOSE 80

WORKDIR /build

# install System.Drawing native dependencies
RUN apt-get update \
    && apt-get install -y --allow-unauthenticated \
        libc6-dev \
        libgdiplus \
        libx11-dev \
     && rm -rf /var/lib/apt/lists/*
     
COPY ["framework", "framework"]
COPY ["modules", "modules"]
COPY ["abp_io", "abp_io"]
COPY ["common.props", "common.props"]

RUN dotnet build "abp_io/src/Volo.AbpWebSite.Web/Volo.AbpWebSite.Web.csproj" -c Release
RUN dotnet publish "abp_io/src/Volo.AbpWebSite.Web/Volo.AbpWebSite.Web.csproj" -c Release -o /prod

WORKDIR /prod
RUN mkdir TemplateFiles
COPY ["entrypoint.sh", "entrypoint.sh"]
RUN chmod +x ./entrypoint.sh
CMD /bin/bash ./entrypoint.sh