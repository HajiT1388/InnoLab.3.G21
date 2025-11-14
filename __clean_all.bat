@echo off
setlocal

for /f "tokens=*" %%C in ('docker ps -q') do docker stop %%C

for /f "tokens=*" %%C in ('docker ps -aq') do docker rm -f %%C

for /f "tokens=*" %%V in ('docker volume ls -q') do docker volume rm -f %%V

for /f "tokens=*" %%I in ('docker images -q') do docker rmi -f %%I

endlocal