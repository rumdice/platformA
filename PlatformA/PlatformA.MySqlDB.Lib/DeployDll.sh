# 코어 프로젝트가 빌드되면 DLL을 참조하는 프로젝트에 배포 시킨다.
#cd bin/
#ls
cp bin/Debug/net8.0/CoreDB.dll ../PushApp/Dll/
cp bin/Debug/net8.0/CoreDB.dll ../WebApp/Dll/
cp bin/Debug/net8.0/CoreDB.dll ../BackOfficeApp/Dll/
cp bin/Debug/net8.0/CoreDB.dll ../LogApp/Dll/
