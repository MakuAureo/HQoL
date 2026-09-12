#!/bin/bash

dotnet build -c Release
mv bin/Release/netstandard2.1/OreoM.HQoL.72.dll TSPackage/
sed -i '/<LCVersion>72<\/LCVersion>/ s/72/73/' HQoL.csproj
dotnet build -c Release
mv bin/Release/netstandard2.1/OreoM.HQoL.73.dll TSPackage/
sed -i '/<LCVersion>73<\/LCVersion>/ s/73/72/' HQoL.csproj
