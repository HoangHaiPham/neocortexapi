#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

#Depending on the operating system of the host machines(s) that will build or run the containers, the image specified in the FROM statement may need to be changed.
#For more information, please see https://aka.ms/containercompat

FROM mcr.microsoft.com/dotnet/runtime:6.0 AS base
RUN apt-get update \
&& apt-get install -y --no-install-recommends libfontconfig1 \
&& rm -rf /var/lib/apt/lists/*

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build

# Set current directory to /src
WORKDIR /src

# Copy all to /src folder
COPY . .

# Set current directory to src/source/InvariantRepresentationLearning/InvariantLearning_Cloud
WORKDIR source/InvariantRepresentationLearning/InvariantLearning_Cloud/
RUN dotnet restore "InvariantLearning_Cloud.csproj"
RUN dotnet build "InvariantLearning_Cloud.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "InvariantLearning_Cloud.csproj" -c Release -o /app/publish

FROM base AS final
# Set current directory to /app
WORKDIR /app
# Copy all from /app/publish to /app
COPY --from=publish /app/publish .
# Run program with root permission
USER root
ENTRYPOINT ["dotnet", "InvariantLearning_Cloud.dll"]