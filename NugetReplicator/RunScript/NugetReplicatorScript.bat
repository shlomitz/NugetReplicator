@ECHO OFF
SET /P start_date=Please enter start date: 
SET /P end_date=Please enter end date: 

rem C:\Projects\NugetReplicator\NugetReplicator\bin\Debug\NugetReplicator.exe %start_date% %end_date%
NugetReplicator.exe %start_date% %end_date%

rem IF "%uname%"=="" GOTO Error
rem ECHO Hello %uname%, Welcome to DOS inputs!
rem GOTO End
rem :Error
rem ECHO You did not enter your name! Bye bye!!
rem :End