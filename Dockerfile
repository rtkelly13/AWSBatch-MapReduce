FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build-env
WORKDIR /app
   
    
# Copy everything and build
COPY ./src ./
RUN dotnet publish ./awsbatch-mapreduce/awsbatch-mapreduce.csproj \
    -c Release -o out
    
# Build runtime image
FROM mcr.microsoft.com/dotnet/runtime:6.0
WORKDIR /app
COPY --from=build-env /app/out .
ENTRYPOINT ["dotnet", "awsbatch-mapreduce.dll"]