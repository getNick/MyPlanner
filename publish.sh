docker build --platform linux/amd64 -t myPlanner/api .
docker tag  myPlanner/api akostiuk/myplanner-api:latest
docker push akostiuk/myplanner-api:latest