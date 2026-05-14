# Plexus_DICOM_Enabler

Application was built on Visual Studio 2019

Open Plexus_DICOM_Enabler.sln and build each of the project. 

# Nu-Get Packages to be Installed are listed below 

fo-dicom 5.0.2
fo-dicom.Codecs 5.1.0
Microsoft.Bcl.AsyncInterfaces 6.0.0
Serilog 2.11.0
Serilog.Sinks.File 5.0.0
System.Buffers 4.5.1
System.Numerics.Vectors 4.5.0
System.Runtime.CompilerServices.Unsafe 6.0.0
System.Text.Encodings.Web 6.0.0
System.Text.Json 6.0.5
System.Threading.Tasks.Extension 4.5.4
System.ValueTuple 4.5.0

# Project and Descriptions


# GenerateConnectionString 

This project is used to generate conneciton String. The conneciton string will be generated and saved to a common xml file. It also has provision to test the DB conneciton before processing the connection string and saving to common configuration file. 

It also provision to encrypt and decrypt data.

# Plexus.Common

This is a library project which is used under different other projects. This project has class and function which are common for other project functionality

1. Save and Read Configuraiton Details
2. Validate User
3. Encrypt and Decryption String 

# Plexus_Auth_Service

This is a Service Project which will authenticate the user Credentials stored in the common configuration file. If the authentication then it would stop the services based on the deployement configuration. Following are the services that the Auth Service will stop based on the validation done 


Plexus Store SCP Service
Plexus MWL SCP Service
Plexus StoreSCU Service

# Plexus_DICOM_Enabler

This is a WinForm project which acts as UI which will be accessed by the end user to perform different operations. Pellucid DICOM Enabler has following screens

Server Manager : Screen where the user will be able to Install/Uninstall , Start and Stop Backend Services
SCP Settings : Screen where user will be able to configure Modality SCP Settings and Store SCP Settings 
SCU Settings  : Screen where user will be able to configure Store SCU Details 
Server List  : Screen where use will be able to add/edit/delete server to which the DICOM Nodes should communicate 
View Patient List :Screen that would display list of patient for which different DICOM inteface has happen ( MWL/SCP/Upload of images)
View Logs : Screen where user will be able view logs of different DICOM Communication ( MWL / STORESCP / STORE SCU)
About Us : Screen user will be able to view based details on Plexus

# Plexus_FileDeleteApp

This is a common line project which will delete archive folder and files that was recieved via Store SCP

# Plexus_MWL_Service

This is a windows service project acts as Modality Worklist SCP. This acts as a interface engine which will provide list patient to the respective modality based on the filter request from the modalities 

# Plexus_StoreSCP_Service
This is a windows service project which will recieve images from Modality and store the images locally. 


# Plexus_StoreSCU_Service
This is a windows service project which will upload images from archive folder to the StoreSCP Node that was configured 

# Unit Test Applications

The solution also has unit test application's project which can be tested individually. 

Sample_ModalitySCP : Unit test application to test the Modality SCP
Sample_Store_SCP : Unit test application to test both StoreSCP and StoreSCU
Test_SeriLog : Unit test application to test the SeriLog Library






