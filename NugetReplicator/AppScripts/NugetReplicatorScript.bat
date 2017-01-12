@ECHO OFF
SET /P start_date=Please enter start date: 
SET /P end_date=Please enter end date: 

..\NugetReplicator.exe %start_date% %end_date%