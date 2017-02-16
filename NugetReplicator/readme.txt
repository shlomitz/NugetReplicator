NugetReplicator App

App for replicate nuget repository from the internet

How to build
Clone the code repository
Start VS
Open the solution and build

How to run
Config ReplicatorSettings.cfg in the RELEASE folder
	set DOWNLOAD folder
	set DATE range
	set which type of download (files+metadata or only metadata)
	set Min download limit for nuget
Run

2 logs files will be generated in each run
	General log - app status and errors
	Metadata log