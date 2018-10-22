#!/bin/bash

set -e
run_cmd='dotnet Volo.AbpWebSite.Web.dll'

cd /build
export ASPNETCORE_ENVIRONMENT=Docker
until dotnet ef database update -s 'abp_io/src/Volo.AbpWebSite.Web/Volo.AbpWebSite.Web.csproj' -p 'abp_io/src/Volo.AbpWebSite.EntityFrameworkCore/Volo.AbpWebSite.EntityFrameworkCore.csproj' ; do
sleep 1
done

cd /prod
exec $run_cmd
