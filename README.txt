BCETL Customer Extractor

Workstation build:
  dotnet restore
  dotnet build -c Release
  dotnet publish -c Release -r win-x64 --self-contained true -o .\publish

Required runtime environment variables:
  BCETL_CLIENT_SECRET
  BCETL_SQL_CONNECTION

DWH-LW initial SQL connection example:
  Server=localhost;Database=bc;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;

Run on DWH-LW:
  BCETL.exe customers
