cd MyPlanner.API
dotnet publish -c Release -o ./bin/Publish
cd ./bin/Publish
zip -r Publish.zip .
az webapp deployment source config-zip --resource-group MyApps --name myplannerapi --src Publish.zip