@echo off
set ASPNETCORE_ENVIRONMENT=Development
set ConnectionStrings__Default=Host=10.10.100.22;Port=5432;Database=sgiform;Username=sgiform;Password=SgiForm2024!;Search Path=sf,public;SSL Mode=Disable
set Jwt__Key=SgiFormJwtKeyForLocalTestingOnly1234567890AbCdEfGhIjKlMnOpQrStUvWxYzZ123456
set Storage__UploadPath=C:\Temp\sgiform_uploads
set PathBase=
cd /d C:\Users\hecto\TRABAJO\dev_ia\kobotoolbox\_publish\api
dotnet SgiForm.Api.dll --urls http://localhost:7777
